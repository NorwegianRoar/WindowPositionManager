using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Newtonsoft.Json;

namespace WindowPositionSaver
{
    public partial class MainWindow : Window
    {
        private readonly string _configFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowPositionSaver");
        private List<WindowInfo> _windowList;
        private string _selectedLayoutFile;
        private Dictionary<string, WindowLayout> _windowLayouts;

        public MainWindow()
        {
            InitializeComponent();
            RefreshWindowList();
            Directory.CreateDirectory(_configFolder);
            LoadLayouts();
        }

        private void RefreshWindowList()
        {
            _windowList = GetOpenWindows();
            WindowListView.ItemsSource = _windowList;
        }

        private List<WindowInfo> GetOpenWindows()
        {
            var windows = new List<WindowInfo>();

            EnumDelegate filter = delegate (IntPtr hWnd, int lParam)
            {
                StringBuilder strbTitle = new StringBuilder(255);
                int nLength = GetWindowText(hWnd, strbTitle, strbTitle.Capacity + 1);
                string strTitle = strbTitle.ToString();

                if (IsWindowVisible(hWnd) && string.IsNullOrEmpty(strTitle) == false)
                {
                    uint windowProcessId;
                    GetWindowThreadProcessId(hWnd, out windowProcessId);
                    var process = Process.GetProcessById((int)windowProcessId);

                    windows.Add(new WindowInfo { Handle = hWnd, Title = strTitle, ProcessName = process.ProcessName });
                }
                return true;
            };

            EnumDesktopWindows(IntPtr.Zero, filter, IntPtr.Zero);
            return windows;
        }

        private void LoadLayouts()
        {
            var layoutFiles = Directory.GetFiles(_configFolder, "layout_*.json");
            LayoutListBox.ItemsSource = layoutFiles.Select(Path.GetFileName);
        }

        private void SaveLayout(string filename)
        {
            var layout = new Dictionary<string, WindowPosition>();

            foreach (var window in _windowList)
            {
                RECT rect;
                GetWindowRect(window.Handle, out rect);
                layout.Add($"{window.Handle}|{window.Title}|{window.ProcessName}",
                    new WindowPosition { TopLeft = new Point(rect.Left, rect.Top), Size = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top) });
            }

            File.WriteAllText(Path.Combine(_configFolder, filename), JsonConvert.SerializeObject(layout));
            LoadLayouts();
        }

        private void RestoreLayout(string filename)
        {
            var layoutData = JsonConvert.DeserializeObject<Dictionary<string, WindowLayout>>(File.ReadAllText(filename));

            foreach (var entry in layoutData)
            {
                var windowHandle = FindWindowByTitleAndProcess(entry.Key);

                if (windowHandle != IntPtr.Zero)
                {
                    MoveAndResizeWindow(windowHandle, entry.Value);
                }
            }
        }

        private IntPtr FindWindowByTitleAndProcess(string key)
        {
            string[] parts = key.Split('|');
            IntPtr targetHandle = new IntPtr(int.Parse(parts[0]));
            string targetTitle = parts[1];
            string targetProcess = parts[2];

            foreach (var window in _windowList)
            {
                if (window.Handle == targetHandle || (window.Title == targetTitle && window.ProcessName == targetProcess))
                {
                    return window.Handle;
                }
            }

            return IntPtr.Zero;
        }

        private void MoveAndResizeWindow(IntPtr hWnd, WindowLayout layout)
        {
            SetWindowPos(hWnd, IntPtr.Zero, (int)layout.TopLeft.X, (int)layout.TopLeft.Y, (int)layout.Size.Width, (int)layout.Size.Height, 0);
        }

        private void SaveLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            SaveLayout($"layout_{timestamp}.json");
        }

        private void LoadLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutListBox.SelectedItem != null)
            {
                _selectedLayoutFile = Path.Combine(_configFolder, (string)LayoutListBox.SelectedItem);
                _windowLayouts = JsonConvert.DeserializeObject<Dictionary<string, WindowLayout>>(File.ReadAllText(_selectedLayoutFile));
            }
        }

        private void RestoreLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLayoutFile != null)
            {
                RestoreLayout(_selectedLayoutFile);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshWindowList();
            LoadLayouts();
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumDelegate lpEnumCallbackFunction, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        delegate bool EnumDelegate(IntPtr hWnd, int lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }

    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        public string ProcessName { get; set; }
    }

    public class WindowPosition
    {
        public Point TopLeft { get; set; }
        public Size Size { get; set; }
    }

    public class WindowLayout
    {
        public Point TopLeft { get; set; }
        public Size Size { get; set; }
    }
}