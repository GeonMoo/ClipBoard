using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClipBoard
{
    class CsvPersistenceController : IPersistenceController
    {
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

        public Dictionary<string, List<ClipBoardRecord>> LoadFromFile(string FileName)
        {
            char[] delimiterChars = { ',' };
            string[] fileFields;
            string[] lines = File.Exists(FileName) ? File.ReadAllLines(FileName) : new string[] { };
            string type;
            List<ClipBoardRecord> savedItems = new List<ClipBoardRecord>();
            List<ClipBoardRecord> recentItems = new List<ClipBoardRecord>();

            POINTAPI point = new POINTAPI();// 必须用与之相兼容的结构体，类也可以                      
            Thread.Sleep(8000);  //add some wait time
            GetCursorPos(ref point);// 获取当前鼠标坐标
            int handle = WindowFromPoint(point.X, point.Y);// 获取指定坐标处窗口的句柄
            

            foreach (string s in lines)
            {
                // Record the resource from the acitivated window title
                ClipBoardRecord rec = new ClipBoardRecord();
                StringBuilder name = new StringBuilder(256);
                // int handle = GetForegroundWindow();
                GetWindowText(handle, name, 256);
                // MessageBox.Show(name.ToString());

                // Find out if we have a saved file containing counts
                if (s.StartsWith("|")) // then new file format 
                {
                    fileFields = s.Split(delimiterChars, 5);
                    rec.CoppiedCount = int.Parse(fileFields[1]);
                    rec.PastedCount = int.Parse(fileFields[2]);
                    rec.Content = Regex.Unescape(fileFields[3].Substring(7));
                    rec.Resource = Regex.Unescape(fileFields[4].Substring(5));
                    type = fileFields[3].Substring(0, 7);
                }
                else // handle previous file format
                {
                    rec.Content = Regex.Unescape(s.Substring(7));
                    rec.Resource = Regex.Unescape(s.Substring(5));
                    rec.CoppiedCount = 0;
                    rec.PastedCount = 0;
                    type = s.Substring(0, 7);
                }

                // now have have the data add it to the relevent list
                if (type.StartsWith("saved:"))
                {
                    savedItems.Add(rec);
                }
                else if (type.StartsWith("recent:"))
                {
                    recentItems.Add(rec);
                }
            }

            var items = new Dictionary<string, List<ClipBoardRecord>>();
            items.Add("saved", savedItems);
            items.Add("recent", recentItems);

            return items;
        }

        public void SaveToFile(string FileName, List<ClipBoardRecord> SavedItems, List<ClipBoardRecord> RecentItems)
        {
            // this function is a candadiate for error handling
            string[] lines = new string[SavedItems.Count + Math.Min(RecentItems.Count, 30)];
            int i = 0;
            foreach (ClipBoardRecord s in SavedItems)
            {
                lines[i++] = "|," + s.CoppiedCount + "," + s.PastedCount + "," + "saved: " + Regex.Escape(s.Content) + "," + "from: " + s.Resource;
            }
            foreach (ClipBoardRecord s in RecentItems)
            {
                lines[i++] = "|," + s.CoppiedCount + "," + s.PastedCount + "," + "recent:" + Regex.Escape(s.Content) + "," + "from: " + s.Resource;
                if (i >= SavedItems.Count + 30)
                {
                    break;
                }
            }

            if (!Directory.Exists(Path.GetDirectoryName(FileName)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FileName));
            }

            File.WriteAllLines(FileName, lines);
        }
    }
}
