using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Lockey.Input
{
    public static class HooksManager
    {
        #region P/Invoke

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        public enum MouseMessages
        {
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_MOUSEMOVE = 0x0200,
            WM_MOUSEWHEEL = 0x020A,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardHookStruct
        {
            /// <summary>
            /// Specifies a virtual-key code. The code must be a value in the range 1 to 254. 
            /// </summary>
            public int VirtualKeyCode;
            /// <summary>
            /// Specifies a hardware scan code for the key. 
            /// </summary>
            public int ScanCode;
            /// <summary>
            /// Specifies the extended-key flag, event-injected flag, context code, and transition-state flag.
            /// </summary>
            public int Flags;
            /// <summary>
            /// Specifies the Time stamp for this message.
            /// </summary>
            public int Time;
            /// <summary>
            /// Specifies extra information associated with the message. 
            /// </summary>
            public int ExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelCallbackProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern short GetKeyState(int vKey);

        private delegate IntPtr LowLevelCallbackProc(int nCode, IntPtr wParam, IntPtr lParam);

        #endregion

        private static Keys lastKeyDown;

        private static IntPtr mouseHookId = IntPtr.Zero;
        private static IntPtr keyboardHookId = IntPtr.Zero;

        private static ActionKey[] actionKeys;
        private static LowLevelCallbackProc mouseCallback;
        private static LowLevelCallbackProc keyboardCallback;

        public static event Action KeyBlocked;
        public static event Action<Keys> KeyPressed;
        public static event Action<Keys> KeyReleased;

        public static bool BlockInput { get; private set; }


        /// <summary>
        /// Sets the hooks for keyboard and mouse (depending on if input blocking is requested)
        /// </summary>
        public static void SetHooks(bool blockInput, params ActionKey[] actionKeys)
        {
            UnHook();

            HooksManager.BlockInput = blockInput;
            HooksManager.actionKeys = actionKeys;
            var hModule = Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]);

            // callbacks have their instance variables to prevent their destrcution by garbage collector
            keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardCallback = new LowLevelCallbackProc(keyboardHookCallback), hModule, 0);            
            if (blockInput)
                mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, mouseCallback = new LowLevelCallbackProc(mouseBlockingHookCallback), hModule, 0);
        }


        /// <summary>
        /// Clears the hooks
        /// </summary>
        public static void UnHook()
        {
            if (mouseHookId != IntPtr.Zero)
                UnhookWindowsHookEx(mouseHookId);

            if (keyboardHookId != IntPtr.Zero)
                UnhookWindowsHookEx(keyboardHookId);

            mouseHookId = IntPtr.Zero;
            keyboardHookId = IntPtr.Zero;

            mouseCallback = null;
            keyboardCallback = null;
        }


        /// <summary>
        /// Process the mouse events and always blocks them
        /// </summary>
        private static IntPtr mouseBlockingHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (wParam == (IntPtr)MouseMessages.WM_LBUTTONDOWN || wParam == (IntPtr)MouseMessages.WM_RBUTTONDOWN)
                    KeyBlocked?.Invoke();                
            }
            catch { }

            return new IntPtr(-1);
        }


        /// <summary>
        /// Processes the keyboard events and blocks them if blocking is on
        /// </summary>
        private static IntPtr keyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            var key = getKey(lParam);
            var isDown = (int)wParam == WM_KEYDOWN || (int)wParam == WM_SYSKEYDOWN;

            try
            {
                // ingore repeating messages
                if (lastKeyDown != key || !isDown)
                {
                    // finding the special key
                    if (tryInvokeActionKey(key, isDown))
                        return new IntPtr(-1);

                    // invoking key pressed event
                    if (isDown)
                        KeyPressed?.Invoke(key);
                    else
                        KeyReleased?.Invoke(key);
                }

                // propagate event if input is not blocked or is a modifier
                if (!BlockInput || isKeyModifier(key))
                    return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);

                // inform about the blocked key
                if (isDown && lastKeyDown != key)
                    KeyBlocked?.Invoke();
            }
            catch { }
            finally
            {
                lastKeyDown = isDown ? key : Keys.None;
            }

            // blocking the input;
            return new IntPtr(-1);
        }

        #region Helpers

        /// <summary>
        /// Returns true of the key is pressed
        /// </summary>
        private static bool isKeyPressed(Keys key)
        {
            return (GetKeyState((int)key) & 0x80) == 0x80;
        }


        /// <summary>
        /// Converts the pointer into structure
        /// </summary>
        private static Keys getKey(IntPtr ptr)
        {
            var key = (Keys)((KeyboardHookStruct)Marshal.PtrToStructure(ptr, typeof(KeyboardHookStruct))).VirtualKeyCode;
            var res = key;

            if (isKeyModifier(key))
                res = Keys.None;

            if (isKeyPressed(Keys.ControlKey) || key == Keys.LControlKey || key == Keys.RControlKey)
                res |= Keys.Control;

            if (isKeyPressed(Keys.ShiftKey) || key == Keys.LShiftKey || key == Keys.RShiftKey)
                res |= Keys.Shift;

            if (isKeyPressed(Keys.Menu) || key == Keys.LMenu || key == Keys.RMenu)
                res |= Keys.Alt;

            return res;
        }


        /// <summary>
        /// Checks if the key combination was triggered
        /// </summary>
        private static bool tryInvokeActionKey(Keys key, bool isDown)
        {
            foreach (var k in actionKeys)
                if (k.Key == key && k.SetPressed(isDown) == true)
                    return true;

            return false;
        }


        /// <summary>
        /// Returns true if the key is a modifier
        /// </summary>
        private static bool isKeyModifier(Keys key)
        {
            return key >= Keys.LShiftKey && key <= Keys.RMenu;
        }

        #endregion
    }
}
