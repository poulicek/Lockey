﻿using Lockey.Input;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Windows.Forms;
using TrayToolkit.Helpers;
using TrayToolkit.OS.Input;
using TrayToolkit.UI;

namespace Lockey.UI
{
    public class TrayIcon : TrayIconBase
    {
        private bool randomizeUnlockKey;
        private bool shortcutSettingMode;

        private readonly Bitmap iconLock;
        private readonly Bitmap iconScreen;
        private readonly SoundPlayer soundBlock;
        private readonly SoundPlayer soundUnblock;
        private readonly SoundPlayer soundLongPress;
        private readonly InputBlocker inputBlocker;


        public TrayIcon() : base("Lockey", "https://github.com/poulicek/lockey")
        {
            var lockKey = AppConfigHelper.Get<Keys>("LockKey", Keys.Pause);
            this.randomizeUnlockKey = AppConfigHelper.Get<bool>("RandomUnlockKey");

            this.inputBlocker = new InputBlocker(lockKey, lockKey);
            this.inputBlocker.ScreenTurnedOff += this.onScreenTurnedOff;
            this.inputBlocker.ScreenOffRequested += this.onScreenOffRequested;
            this.inputBlocker.BlockingStateChanged += this.onBlockingStateChanged;

            this.inputBlocker.KeyPressed += this.onKeyPressed;
            this.inputBlocker.KeyReleased += this.onKeyReleased;
            this.inputBlocker.KeyBlocked += this.onKeyBlocked;

            this.soundBlock = this.getSound("Lock.wav");
            this.soundUnblock = this.getSound("Unlock.wav");
            this.soundLongPress = this.getSound("LongPress.wav");

            this.iconLock = ResourceHelper.GetResourceImage("Resources.IconLocked.png");
            this.iconScreen = ResourceHelper.GetResourceImage("Resources.IconScreenOff.png");
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
            var locked = this.inputBlocker.BlockInput;
            return $"Resources.Icon{(locked ? "Locked" : "Unlocked")}{(lightMode ? "Light" : "Dark")}.png";
        }


        /// <summary>
        /// Shows the tooltip
        /// </summary>
        private void showToolTip(bool blockingState)
        {
            try
            {
                if (blockingState != this.inputBlocker.BlockInput)
                    return;

                this.trayIcon.Visible = true;
                if (blockingState)
                    BalloonTooltip.Show(
                        $"Your keyboard and mouse is locked.{Environment.NewLine}Press \"{this.inputBlocker.UnblockingKey}\" to unlock.",
                        this.iconLock,
                        $"Hold \"{this.inputBlocker.UnblockingKey}\" to turn off the screen.",
                        5000);
                else
                    BalloonTooltip.Hide();
            }
            catch { }
        }

        protected override List<MenuItem> getContextMenuItems()
        {
            this.setTitle($"Lockey - press \"{this.inputBlocker.BlockingKey}\" to lock your keyboard and mouse");

            var items = base.getContextMenuItems();
            items.Insert(0, new MenuItem("-"));
            items.Insert(0, new MenuItem("Unlock by random number", this.onRandomKeyClick) { Checked = this.randomizeUnlockKey });
            items.Insert(0, new MenuItem("Set lock key...", this.onSetShortcutClick));
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
            return new SoundPlayer(ResourceHelper.GetResourceStream($"Resources.{fileName}"));
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


        /// <summary>
        /// Sets the key for blocking and unblocking
        /// </summary>
        private void setActionKey(Keys key)
        {
            this.inputBlocker.BlockingKey.Key = key;
            this.inputBlocker.UnblockingKey.Key = key;
            this.createContextMenu();
        }


        /// <summary>
        /// Returns a random numeric key
        /// </summary>
        private Keys getRandomNumericKey()
        {
            var num = new Random().Next(0, 9);
            return (Keys)((int)Keys.D0 + num);
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
            if (this.randomizeUnlockKey)
                this.inputBlocker.UnblockingKey.Key = this.getRandomNumericKey();

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
            BalloonTooltip.Show("Turning off the screen...", this.iconScreen);
        }

        private void onKeyPressed(Keys key)
        {
            if (this.shortcutSettingMode)
                BalloonTooltip.Show($"Press desired key combination{Environment.NewLine}{(ActionKey)key}", this.iconLock);
        }


        private void onKeyReleased(Keys key)
        {
            if (!this.shortcutSettingMode)
                return;

            if (key.GetUnmodifiedKey() == Keys.None)
                return;

            try
            {
                BalloonTooltip.Hide();
                this.shortcutSettingMode = false;
                if (MessageBox.Show($"Do you wish to set this shortcut? {(ActionKey)key}", "Set shortcut", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.No)
                    return;

                this.setActionKey(key);
                AppConfigHelper.Set("LockKey", key);
            }
            finally
            {
                this.inputBlocker.Enabled = true;
            }
        }

        private void onScreenTurnedOff()
        {
            BalloonTooltip.Show("..zzZ", this.iconScreen);
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
            BalloonTooltip.Show($"Press desired key combination...", this.iconLock);
        }

        private void onRandomKeyClick(object sender, EventArgs e)
        {
            this.randomizeUnlockKey = !(sender as MenuItem).Checked;
            if (!this.randomizeUnlockKey)
                this.setActionKey(this.inputBlocker.BlockingKey.Key);

            AppConfigHelper.Set("RandomUnlockKey", this.randomizeUnlockKey);
            this.createContextMenu();
        }

        #endregion
    }
}
