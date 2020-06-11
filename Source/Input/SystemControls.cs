using System.Runtime.InteropServices;

namespace Lockey.Input
{
    public class SystemControls
    {
        [DllImport("user32.dll")]
        private static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);

        public static void TurnOffScreen()
        {
            SendMessage(0xFFFF, 0x112, 0xF170, 2);
        }
    }
}
