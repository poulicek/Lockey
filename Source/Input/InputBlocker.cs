using System;
using System.Threading;

namespace Lockey.Input
{
    public class InputBlocker : IDisposable
    {
        public event Action ScreenOffRequested;
        public event Action ScreenTurnedOff;
        public event Action<bool> BlockingStateChanged;

        public bool Enabled { get; set; }

        public bool IsBlocking { get { return HooksManager.BlockInput; } }

        public ActionKey BlockingKey { get; private set; }
        public ActionKey UnblockingKey { get; private set; }


        public InputBlocker(ActionKey blockingKey, ActionKey unblockingKey)
        {
            this.Enabled = true;
            this.BlockingKey = blockingKey;
            this.BlockingKey.Pressed += this.onBlockingKeyPressed;
            this.BlockingKey.Released += this.onBlockingKeyReleased;
            this.BlockingKey.LongPress += this.onBlockingKeyLongPress;

            this.UnblockingKey = unblockingKey;
            this.UnblockingKey.Pressed += this.onUnblockingKeyPressed;

            this.setBlockingState(false);   
        }

        #region Action Keys Event Handlers

        private bool onBlockingKeyPressed()
        {
            return this.StartBlocking();
        }

        private bool onBlockingKeyReleased()
        {
            if (this.BlockingKey?.WasLongPressed != true)
                return false;

            this.StartBlockingScreenOff();
            return true;
        }

        private void onBlockingKeyLongPress()
        {
            if (this.Enabled)
                this.ScreenOffRequested?.Invoke();
        }

        private bool onUnblockingKeyPressed()
        {
            return this.StopBlocking();
        }

        #endregion

        #region Interface

        public void StartBlockingScreenOff()
        {
            if (!this.Enabled)
                return;

            this.StartBlocking();
#if !DEBUG
            this.turnScreenOff(500);
#endif
            this.ScreenTurnedOff?.Invoke();
        }


        public bool StartBlocking()
        {
            if (!this.Enabled || HooksManager.BlockInput)
                return false;

            this.setBlockingState(true);
            this.BlockingStateChanged?.Invoke(HooksManager.BlockInput);
            return true;
        }


        public bool StopBlocking()
        {
            if (!this.Enabled || !HooksManager.BlockInput)
                return false;

            this.setBlockingState(false);
            this.BlockingStateChanged?.Invoke(HooksManager.BlockInput);
            return true;
        }


        public void Dispose()
        {
            this.setBlockingState(false);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Sets the blocking state
        /// </summary>
        /// <param name="blockInput"></param>

        private void setBlockingState(bool blockInput)
        {
            HooksManager.SetHooks(blockInput, this.BlockingKey, this.UnblockingKey);
        }


        /// <summary>
        /// Turns off the screen
        /// </summary>
        private void turnScreenOff(int timeLimitMs)
        {
            try
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        SystemControls.TurnOffScreen();
                    }
                    catch { }
                });
                t.Start();
                Thread.Sleep(timeLimitMs);
                t.Abort();
            }
            catch { }
        }

        #endregion
    }
}
