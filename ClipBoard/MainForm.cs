using FMUtils.KeyboardHook;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Native;
using Dapplo.Log;

namespace ClipBoard
{
    public partial class MainForm : Form
    {
        public ListView list;
        private static string _settingsFile = Program.SettingsFileName;
        private static int maxCopyTextLength;
        private bool allowSaveAsNowLoaded = false;
        private ClipBoardUserSettings _settings;
        IntPtr _ClipboardViewerNext;
        IPersistenceController _persistenceController;
        ClipBoardListController _listController;
        private static readonly LogSource Log = new LogSource();
        Hook keyboardHook;

        public MainForm()
        {
            InitializeComponent();
            list = this.listView;
            this.FormBorderStyle = FormBorderStyle.None;
            _persistenceController = new CsvPersistenceController();
            _settings = new ClipBoardUserSettings(_settingsFile);
            _settings.SettingsSaving += SettingsSaving;
            _listController = new ClipBoardListController(_settings);
            this.MouseDown += new MouseEventHandler(Form_MouseDown);
            this.labelCopyCollector.MouseDown += new MouseEventHandler(Form_MouseDown);
            _ClipboardViewerNext = ClipBoard.Win32Hooks.SetClipboardViewer(this.Handle);
            maxCopyTextLength = _settings.MaxCopyTextLength;
            HandleStartupSetting(_settings.RunOnStartup);
            keyboardHook = new Hook("Global Action Hook");
            keyboardHook.KeyDownEvent += KeyDownHandler;
            // Always the top most
            this.TopMost = true;

            if (_settings.StartMinimized)
            {
                hideScreen();
            }
        }

        private void KeyDownHandler(KeyboardHookEventArgs e)
        {            
            Log.Verbose().Write("KeyDownEvent Recognized.");
            // handle keydown event here
            // Such as by checking if e (KeyboardHookEventArgs) matches the key you're interested in
            Keys userHotKey;
            Enum.TryParse<Keys>(_settings.HotKey, out userHotKey);

            // helper variables:
            // get the modifier settings from settings class and evaluate if they match the
            // current pressed buttons
            var isCtrlModifierValid = _settings.UseCtrlKey == e.isCtrlPressed ? true : false;
            var isShiftModifierValid = _settings.UseShiftKey == e.isShiftPressed ? true : false;
            var isAltModifierValid = _settings.UseAltKey == e.isAltPressed ? true : false;
            var isWindModifierVaild = _settings.UseWindowsKey == e.isWinPressed ? true : false;
            
            // if all conditions are true and align with the settings show the Clipboard screen
            if (e.Key == userHotKey && isCtrlModifierValid && isShiftModifierValid && isAltModifierValid && isWindModifierVaild)
            {
                Log.Debug().Write("Conditions for show ClipBoard screen met.");
                showScreen();
            }

            //control-v pressed
            if (e.Key == Keys.V && e.isCtrlPressed)
            {
                Log.Verbose().Write("Paste value triggered by CTRL + V.");
                recordPaste();
            }

        }

        private void HandleStartupSetting(bool RunOnStartup)
        {
            if (RunOnStartup)
            {
                var path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                RegistryKey key = Registry.CurrentUser.OpenSubKey(path, true);
                key.SetValue("ClipBoard", Application.ExecutablePath.ToString());
            }
            else
            {
                var path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                RegistryKey key = Registry.CurrentUser.OpenSubKey(path, true);
                key.DeleteValue("Clipboard", false);
            }
        }
        private void SettingsSaving(object sender, System.ComponentModel.CancelEventArgs e)
        {
            HandleStartupSetting(_settings.RunOnStartup);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            list.TileSize = System.Drawing.Size.Empty;
            loadContent(_settings.ContentFile);
            updateList();
            allowSaveAsNowLoaded = true;
        }

        private void MainForm_FormClosing(Object sender, FormClosingEventArgs e)
        {
            Log.Verbose().Write("FormClosing");
            ClipBoard.Win32Hooks.ChangeClipboardChain(this.Handle, _ClipboardViewerNext);
        }

        private void updateList()
        {
            list.Items.Clear();

            int i = 1;

            // Add in saved records
            foreach (ClipBoardRecord s in _listController.SavedItems)
            {
                ListViewItem lvi =
                    new ListViewItem(new string[] { (i++).ToString(),
                                                    s.CoppiedCount.ToString(),
                                                    s.PastedCount.ToString(),
                                                    s.Content,
                                                    s.Resource
                                                  });
                list.Items.Add(lvi);
                list.Groups[0].Items.Add(lvi);
            }

            _listController.FrequentItems.Clear();

            foreach (ClipBoardRecord s in _listController.FrequentItems)
            {
                ListViewItem lvi =
                    new ListViewItem(new string[] { (i++).ToString(),
                                                    s.CoppiedCount.ToString(),
                                                    s.PastedCount.ToString(),
                                                    s.Content,
                                                    s.Resource
                                                  });

                list.Items.Add(lvi);
                list.Groups[1].Items.Add(lvi);
            }

            // finally populate the recent list
            foreach (ClipBoardRecord s in _listController.RecentItems)
            {
                ListViewItem lvi =
                    new ListViewItem(new string[] { (i++).ToString(),
                                                    s.CoppiedCount.ToString(),
                                                    s.PastedCount.ToString(),
                                                    s.Content,
                                                    s.Resource
                                                  });
                list.Items.Add(lvi);
                list.Groups[2].Items.Add(lvi);
            }

            //save to csv
            if (allowSaveAsNowLoaded)
            {
                _persistenceController.SaveToFile(_settings.ContentFile, _listController.SavedItems, _listController.RecentItems);
            }

            //resize the form to fit the number of items
            resizeForm();
        }

        /// <summary>
        ///  Function to resize the heigh of this WinForm based on the number of itmes in the lists
        /// </summary>
        private void resizeForm()
        {
            var listViewItemsCount = listView.Items.Count;
            if (listViewItemsCount > 0)
            {
                this.Height = (listViewItemsCount * listView.Items[0].Bounds.Height)
                                    + (listView.Groups.Count * listView.GetItemRect(0).Height)
                                    + ((listView.Items.Count) + 100);
            }

            Region = System.Drawing.Region.FromHrgn(Win32Hooks.CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
            // linkLabelGitHub.Top = listView.Top + listView.Height + 5;

        }

        private void loadContent(string contentFileName)
        {
            var items = _persistenceController.LoadFromFile(contentFileName);
            _listController.RecentItems = items.Where(i => i.Key == "recent").Select(i => i.Value).FirstOrDefault();
            _listController.SavedItems = items.Where(i => i.Key == "saved").Select(i => i.Value).FirstOrDefault();
        }

        // code so show list screen
        private void showScreen()
        {
            notifyIcon.Visible = false;
            this.Show();
            this.WindowState = FormWindowState.Normal;

            //bring to front if not
            this.TopMost = true;
            // always the Topmost
            // this.TopMost = false;

            resizeForm();
        }

        private void hideScreen()
        {
            this.WindowState = FormWindowState.Minimized;
        }

        // increment the pasted counter for current clipboard content
        private void recordPaste()
        {
            ClipBoardRecord clipBoardRecord;
            if (Clipboard.ContainsText() && Clipboard.GetText().Length < maxCopyTextLength)
            {
                clipBoardRecord = _listController.GetClipBoardRecordViaContent(Clipboard.GetText());
                if (clipBoardRecord != null)
                {
                    _listController.IncrementPasted(clipBoardRecord.Content);
                    updateList();
                }
            }
        }

        private void handleClipboardChanged()
        {

            _listController.AddClipBoardRecord(Clipboard.GetText());
            updateList();
        }

        private void listView_DoubleClick(object sender, EventArgs e)
        {
            copyTextToClipboardAndPaste();
        }

        private void listView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return)
            {
                copyTextToClipboardAndPaste();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                //hide after text copied to clipboard
                hideScreen();
            }
        }

        private async void copyTextToClipboardAndPaste()
        {
            copyTextToClipBoard();

            //hide after text copied to clipboard
            //hideScreen();

            // paste to curreMonkey talk font cursor
            await Task.Delay(500);
            Log.Verbose().Write("Trigger CTRL + V to paste from inside ClipBoard");
            var inputSimulator = new InputSimulator();
            inputSimulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);
        }

        private void copyTextToClipBoard()
        {
            if (this.list.SelectedIndices.Count > 0)
            {
                Log.Verbose().Write("Copy text to clipboard");
                int index = this.list.SelectedIndices[0];
                string content = this.list.Items[index].SubItems[3].Text;
                // updating causes the lost of focus
                // updateList();
                
                Clipboard.SetText(content);
            }
        }

        private void listView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                contextMenuStrip.Show(list, e.X, e.Y);

                if (list.SelectedItems[0].Group.Equals(list.Groups[0])) // in Saved Group
                {
                    saveToolStripMenuItem.Enabled = false;
                }
                else // in Recent group
                {
                    saveToolStripMenuItem.Enabled = true;
                }
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                // copyTextToClipboardAndPaste();
                copyTextToClipBoard();
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClipBoardRecord rec;
            rec = _listController.GetClipBoardRecordViaContent(list.Items[list.SelectedIndices[0]].SubItems[3].Text);
            _listController.SavedItems.Add(rec);
            _listController.RecentItems.Remove(rec);
            updateList();
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _listController.RemoveClipBoardRecordViaContent(list.SelectedItems[0].SubItems[3].Text);
            updateList();
        }


        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (FormWindowState.Minimized == this.WindowState)
            {
                notifyIcon.Visible = true;
                this.Hide();
            }
            else if (FormWindowState.Normal == this.WindowState)
            {
                notifyIcon.Visible = false;
            }
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            notifyIcon.Visible = false;
            this.WindowState = FormWindowState.Normal;
            this.Show();
            resizeForm();
        }

        private void notifyIcon_Click(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            this.WindowState = FormWindowState.Normal;
            this.Show();
        }


        private void Form_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                /*
                Constants in Windows API
                0x2 = HTCAPTION - Application Title Bar
                */
                ClipBoard.Win32Hooks.ReleaseCapture();
                ClipBoard.Win32Hooks.SendMessage(Handle, (int)ClipBoard.Msgs.WM_NCLBUTTONDOWN, (IntPtr)0x2, (IntPtr)0);
            }
        }
        protected override void WndProc(ref Message m)
        {           
            switch ((ClipBoard.Msgs)m.Msg)
            {
                //
                // The WM_DRAWCLIPBOARD message is sent to the first window 
                // in the clipboard viewer chain when the content of the 
                // clipboard changes. This enables a clipboard viewer 
                // window to display the new content of the clipboard. 
                //
                case ClipBoard.Msgs.WM_DRAWCLIPBOARD:
                    Log.Verbose().Write("WM_DRAWCLIPBOARD");
                    handleClipboardChanged();

                    //
                    // Each window that receives the WM_DRAWCLIPBOARD message 
                    // must call the SendMessage function to pass the message 
                    // on to the next window in the clipboard viewer chain.
                    //
                    ClipBoard.Win32Hooks.SendMessage(_ClipboardViewerNext, m.Msg, m.WParam, m.LParam);
                    break;


                //
                // The WM_CHANGECBCHAIN message is sent to the first window 
                // in the clipboard viewer chain when a window is being 
                // removed from the chain. 
                //
                case ClipBoard.Msgs.WM_CHANGECBCHAIN:
                    Log.Verbose().Write("WM_CHANGECBCHAIN");
                    // When a clipboard viewer window receives the WM_CHANGECBCHAIN message, 
                    // it should call the SendMessage function to pass the message to the 
                    // next window in the chain, unless the next window is the window 
                    // being removed. In this case, the clipboard viewer should save 
                    // the handle specified by the lParam parameter as the next window in the chain. 

                    //
                    // wParam is the Handle to the window being removed from 
                    // the clipboard viewer chain 
                    // lParam is the Handle to the next window in the chain 
                    // following the window being removed. 
                    if (m.WParam == _ClipboardViewerNext)
                    {
                        //
                        // If wParam is the next clipboard viewer then it
                        // is being removed so update pointer to the next
                        // window in the clipboard chain
                        //
                        _ClipboardViewerNext = m.LParam;
                    }
                    else
                    {
                        ClipBoard.Win32Hooks.SendMessage(_ClipboardViewerNext, m.Msg, m.WParam, m.LParam);
                    }
                    break;

                default:
                    //
                    // Let the form process the messages that we are
                    // not interested in
                    //
                    base.WndProc(ref m);
                    break;
            }
        }

        private void linkLabelGitHub_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // System.Diagnostics.Process.Start(linkLabelGitHub.Text);
        }

        private void labelMinimize_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                hideScreen();
            }
            else if (e.Button == MouseButtons.Right)
            {
                var configurator = new ClipBoardConfigurator(_settings, true);
                configurator.ShowDialog();
            }
        }

        private void LabelClipBoardManager_Click(object sender, EventArgs e)
        {

        }

        private void LabelMinimize_Click(object sender, EventArgs e)
        {

        }
    }
}
