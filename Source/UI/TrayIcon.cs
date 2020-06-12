using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Windows.Forms;
using Lockey.Input;
using TrayToolkit;

namespace Lockey.UI
{
    public class TrayIcon : TrayIconBase
    {
        private bool shortcutSettingMode;

        private readonly Bitmap iconLock;
        private readonly Bitmap iconScreen;
        private readonly SoundPlayer soundBlock;
        private readonly SoundPlayer soundUnblock;
        private readonly SoundPlayer soundLongPress;
        private readonly InputBlocker inputBlocker;


        public TrayIcon() : base("Lockey")
        {
            HooksManager.KeyPressed += this.onKeyPressed;
            HooksManager.KeyReleased += this.onKeyReleased;
            HooksManager.KeyBlocked += this.onKeyBlocked;

            BalloonTooltip.InitActivation();

            this.inputBlocker = new InputBlocker(Keys.Pause, Keys.Pause);
            this.inputBlocker.ScreenTurnedOff += this.onScreenTurnedOff;
            this.inputBlocker.ScreenOffRequested += this.onScreenOffRequested;
            this.inputBlocker.BlockingStateChanged += this.onBlockingStateChanged;

            this.soundBlock = this.getSound("Lock.wav");
            this.soundUnblock = this.getSound("Unlock.wav");
            this.soundLongPress = this.getSound("LongPress.wav");

            this.iconLock = this.getResourceImage("Resources.IconLocked.png");
            this.iconScreen = this.getResourceImage("Resources.IconScreenOff.png");
        }

        #region UI

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this.soundBlock.Dispose();
            this.soundUnblock.Dispose();
            this.inputBlocker.Dispose();
            this.iconLock.Dispose();
        }


        /// <summary>
        /// Returns the name of the icon
        /// </summary>
        protected override string getIconName(bool lightMode)
        {
            var locked = this.inputBlocker.IsBlocking;
            return $"Resources.Icon{(locked ? "Locked" : "Unlocked")}{(lightMode ? "Light" : "Dark")}.png";
        }


        /// <summary>
        /// Shows the tooltip
        /// </summary>
        private void showToolTip(bool blockingState)
        {
            try
            {
                if (blockingState != this.inputBlocker.IsBlocking)
                    return;

                this.trayIcon.Visible = true;
                if (blockingState)
                    BalloonTooltip.Show(
                        this.iconLock,
                        $"Your keyboard and mouse is locked.{Environment.NewLine}Press \"{this.inputBlocker.UnblockingKey}\" to unlock.",
                        $"Hold \"{this.inputBlocker.UnblockingKey}\" to turn off the screen.",
                        5000);
                else
                    BalloonTooltip.Hide();
            }
            catch { }
        }

        protected override List<MenuItem> getMenuItems()
        {
            this.setTitle($"Lockey - press \"{this.inputBlocker.BlockingKey}\" to lock your keyboard and mouse");

            var items = base.getMenuItems();
            items.Insert(0, new MenuItem("Set shortcut...", this.onSetShortcutClick));
            items.Insert(0, new MenuItem("-"));
            items.Insert(0, new MenuItem("Turn off screen", this.onScreenTurnOffClick));
            items.Insert(0, new MenuItem("Lock", this.onLockClick, (Shortcut)this.inputBlocker.BlockingKey.Key));

            return items;
        }

        /// <summary>
        /// Returns the sound object
        /// </summary>
        private SoundPlayer getSound(string fileName)
        {
            return new SoundPlayer(this.getResourceStream($"Resources.{fileName}"));
        }

        
        /// <summary>
        /// Plays the notification
        /// </summary>
        private void playNotificationSound(bool block)
        {
            if (block)
                this.soundBlock?.Play();
            else
                this.soundUnblock?.Play();
        }


        private void setActionKey(Keys key)
        {
            this.inputBlocker.BlockingKey.Key = key;
            this.inputBlocker.UnblockingKey.Key = key;
            this.createContextMenu();
        }

        #endregion

        #region Event Handlers

        protected override void onTrayIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                this.inputBlocker.StartBlocking();
        }

        private void onBlockingStateChanged(bool state)
        {
            this.updateLook();
            this.showToolTip(state);
            this.playNotificationSound(state);
        }

        private void onKeyBlocked()
        {
            this.showToolTip(true);
        }

        private void onScreenOffRequested()
        {
            BalloonTooltip.Show(this.iconScreen, "Turning the screen off...");
        }

        private void onKeyPressed(Keys key)
        {
            if (this.shortcutSettingMode)
                BalloonTooltip.Show(this.iconLock, $"Press desired key combination{Environment.NewLine}{(ActionKey)key}");
        }


        private void onKeyReleased(Keys key)
        {
            if (!this.shortcutSettingMode)
                return;

            if (ActionKey.GetUnmodifiedKey(key) == Keys.None)
                return;

            try
            {
                BalloonTooltip.Hide();
                this.shortcutSettingMode = false;
                if (MessageBox.Show($"Do you wish to set this shortcut? {(ActionKey)key}", "Set shortcut", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
                    return;

                this.setActionKey(key);
            }
            finally
            {
                this.inputBlocker.Enabled = true;
            }
        }

        private void onScreenTurnedOff()
        {
            BalloonTooltip.Show(this.iconScreen, "..zzZ");
            BalloonTooltip.Activate();
        }

        private void onScreenTurnOffClick(object sender, EventArgs e)
        {
            this.inputBlocker.StartBlockingScreenOff();
        }

        private void onLockClick(object sender, EventArgs e)
        {
            this.inputBlocker.StartBlocking();
        }

        private void onSetShortcutClick(object sender, EventArgs e)
        {
            this.inputBlocker.Enabled = false;
            this.shortcutSettingMode = true;
            BalloonTooltip.Show(this.iconLock, $"Press desired key combination...");
        }

#endregion
    }
}
