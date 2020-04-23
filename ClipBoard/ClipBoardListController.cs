using Dapplo.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClipBoard
{
    class ClipBoardListController
    {
        private List<ClipBoardRecord> _savedItems;
        private List<ClipBoardRecord> _recentItems;
        private ClipBoardUserSettings _settings;
        private static int _maxCopyTextLength;
        private static readonly LogSource Log = new LogSource();

        // Add resorce listener
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern int GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential)]//定义与API相兼容结构体，实际上是一种内存转换
        public struct POINTAPI
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", EntryPoint = "GetCursorPos")]//获取鼠标坐标
        public static extern int GetCursorPos(
            ref POINTAPI lpPoint
        );

        [DllImport("user32.dll", EntryPoint = "WindowFromPoint")]//指定坐标处窗体句柄
        public static extern int WindowFromPoint(
            int xPoint,
            int yPoint
        );

        [DllImport("user32.dll", EntryPoint = "GetWindowText")]
        public static extern int GetWindowText(
            int hWnd,
            StringBuilder lpString,
            int nMaxCount
        );

        [DllImport("user32.dll", EntryPoint = "GetClassName")]
        public static extern int GetClassName(
            int hWnd,
            StringBuilder lpString,
            int nMaxCont
        );

        public ClipBoardListController(ClipBoardUserSettings SettingsProvider)
        {
            _savedItems = new List<ClipBoardRecord>();
            _recentItems = new List<ClipBoardRecord>();
            _settings = SettingsProvider;
            _maxCopyTextLength = _settings.MaxCopyTextLength;
        }

        public List<ClipBoardRecord> SavedItems
        {
            get { return _savedItems; }
            set { _savedItems = value; }
        }

        public List<ClipBoardRecord> FrequentItems
        {
            get
            {
                var frequentItems = _recentItems
                    .Where(rec => rec.PastedCount > 0)
                    .OrderByDescending(rec => rec.PastedCount).Take(_settings.MaxItemsInFrequentList);
                return frequentItems.ToList();
            }
        }
        public List<ClipBoardRecord> RecentItems
        {
            get { return _recentItems; }
            set { _recentItems = value; }
        }

        // Add either new record or increment existing record counter
        public void AddClipBoardRecord(string content)
        {
            POINTAPI point = new POINTAPI();// 必须用与之相兼容的结构体，类也可以                      
            Thread.Sleep(8000);  //add some wait time
            GetCursorPos(ref point);// 获取当前鼠标坐标
            int handle = WindowFromPoint(point.X, point.Y);// 获取指定坐标处窗口的句柄

            // Record the resource from the acitivated window title
            StringBuilder rsc = new StringBuilder(256);
            // int handle = GetForegroundWindow();
            GetWindowText(handle, rsc, 256);
            string resource = rsc.ToString();
            // MessageBox.Show(name.ToString());

            Log.Verbose().Write("Add content to clipboard.");
            ClipBoardRecord rec;

            //accept content only of not empty and not too big
            if (content.Length != 0 && content.Length < _maxCopyTextLength)
            {
                rec = GetClipBoardRecordViaContent(content);

                if (rec == null) // this is a new content
                {
                    // add a new record to the list
                    rec = new ClipBoardRecord(content, resource, 1, 0);
                    _recentItems.Insert(0, rec);
                }
                else
                {
                    // increment the existing matching record
                    rec.CoppiedCount++;
                }

                //limit number of recent items
                if (_recentItems.Count > 25)
                {
                    Log.Debug().Write("Recent items exceeded max size. Remove last item.");
                    _recentItems.RemoveAt(_recentItems.Count - 1);
                }
            }
            else Log.Warn().Write("Content emtpy or longer than defined max length.");
        }

        // Given a content this function will remove a clipboard 
        // record if it exists in either the saved or recent list
        public void RemoveClipBoardRecordViaContent(string content)
        {
            // if ti exists it will only be in one list. 
            // so its safe to try and remove from both.
            _savedItems.Remove(GetClipBoardRecordViaContent(content));
            _recentItems.Remove(GetClipBoardRecordViaContent(content));
        }

        public ClipBoardRecord GetClipBoardRecordViaContent(string content)
        {
            ClipBoardRecord foundRecord = null;
            foreach (ClipBoardRecord rec in _savedItems)
            {
                if (rec.Content == content)
                    foundRecord = rec;
            }
            foreach (ClipBoardRecord rec in _recentItems)
            {
                if (rec.Content == content)
                    foundRecord = rec;
            }
            return foundRecord;
        }

        public void IncrementPasted(string content)
        {
            foreach (ClipBoardRecord s in _savedItems)
            {
                if (s.Content == content)
                    s.PastedCount++;
            }
            foreach (ClipBoardRecord s in _recentItems)
            {
                if (s.Content == content)
                    s.PastedCount++;
            }
        }


    }
}
