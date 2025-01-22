using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Linq;
using System.Management;
using System.Drawing;

namespace WpfApp
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        public MainWindow()
        {
            InitializeComponent();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
            // Topmost = true;
        }
        private void Timer_Tick(object? sender, EventArgs e)
        {
            // 获取屏幕信息并更新UI
            /*
            var screenInfo = GetScreenResolution();
            screenSizeLabel.Content = $"屏幕宽高: {screenInfo.Width}x{screenInfo.Height}";
            */
            // 获取全屏窗口信息并更新UI
            var (fullScreenCount, fullScreenInfo) = GetFullScreenWindowCountAndInfo();
            fullScreenWindowInfoLabel.Content = $"全屏窗口数量: {fullScreenCount}\n{fullScreenInfo}";
        }

        // WinAPI 函数
        [DllImport("user32.dll")]
        private static extern int EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);



        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        // 判断前台窗口所在的显示器
        public static (int monitorWidth, int monitorHeight) GetForegroundWindowMonitor()
        {
            IntPtr hwnd = GetForegroundWindow();
            RECT windowRect;
            if (GetWindowRect(hwnd, out windowRect))
            {
                // 获取窗口左上角的坐标
                POINT windowPoint = new POINT { X = windowRect.Left, Y = windowRect.Top };
                fullScreenInfo.Append($"窗口左上角坐标: {windowRect.Left}, {windowRect.Top}\r\n");
                // 使用 MonitorFromPoint 来获取显示器句柄
                IntPtr hMonitor = MonitorFromPoint(windowPoint, 2);
                if (hMonitor != IntPtr.Zero)
                {
                    // 获取显示器信息
                    MONITORINFO monitorInfo = new MONITORINFO();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                    if (GetMonitorInfo(hMonitor, ref monitorInfo))
                    {
                        var monitorWidth = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                        var monitorHeight = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;
                        fullScreenInfo.Append($"显示器大小: {monitorWidth}, {monitorHeight}\r\n");
                        return (monitorWidth, monitorHeight);
                    }
                }
                // 如果是单个显示器的情况下, 最大化但非全屏窗口的左上角坐标值会是负数, 进而导致返回(0, 0), 或许需要通过真正的多显示器来进行测试
            }
            return (0, 0);
        }

        // 定义 DEVMODE 结构
        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;

            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] dmFormName;
            public short dmLogPixels;
            public short dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmPelsWidthInMeters;
            public int dmPelsHeightInMeters;
            public int dmDisplayFlags;
            public int dmNup;
        }

        public static StringBuilder fullScreenInfo = new StringBuilder();
        public static (int fullScreenCount, string fullScreenInfo) GetFullScreenWindowCountAndInfo()
        {
            int fullScreenCount = 0;
            fullScreenInfo.Clear();

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);

                if (string.IsNullOrWhiteSpace(title.ToString()))
                {
                    return true;
                }

                RECT windowRect;
                RECT windowFactRect;
                if (IsWindowFullScreen(hWnd, out windowRect, out windowFactRect))
                {
                    fullScreenCount++;
                    fullScreenInfo.AppendLine($"全屏窗口: {title}\r\n宽: {windowRect.Right - windowRect.Left}, 高: {windowRect.Bottom - windowRect.Top}\r\n实际宽高: {windowFactRect.Right - windowFactRect.Left}*{windowFactRect.Bottom - windowFactRect.Top}");
                }
                
                return true;
            }, IntPtr.Zero);

            return (fullScreenCount, fullScreenInfo.ToString());
        }

        public static bool IsWindowFullScreen(IntPtr hWnd, out RECT windowRect, out RECT windowFactRect)
        {
            windowRect = new RECT();
            windowFactRect = new RECT();
            IntPtr foregroundHwnd = GetForegroundWindow();
            
            if (hWnd != foregroundHwnd)
            {
                return false; // 当前窗口是后台窗口
            }

            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            if (className.ToString() == "XamlExplorerHostIslandWindow")
            {
                return false; // 当前窗口是任务切换窗口, 不予理睬
            }
            // fullScreenInfo.Append($"\r\n {className.ToString()}\r\n");
            if (GetWindowRect(hWnd, out windowRect) && GetClientRect(hWnd, out windowFactRect))
            {
                // 获取物理屏幕分辨率
                var (screenWidth, screenHeight) = GetForegroundWindowMonitor();

                int windowWidth = windowRect.Right - windowRect.Left;
                int windowHeight = windowRect.Bottom - windowRect.Top;

                int windowFactWidth = windowFactRect.Right - windowFactRect.Left;
                int windowFactHeight = windowFactRect.Bottom - windowFactRect.Top;

                const int tolerance = 10;

                if (Math.Abs(windowWidth - screenWidth) <= tolerance &&
                    Math.Abs(windowHeight - screenHeight) == 0)
                {
                    return true;
                }
                StringBuilder title = new StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);
                fullScreenInfo.AppendLine($"非全屏窗口: {title}\r\n宽: {windowRect.Right - windowRect.Left}, 高: {windowRect.Bottom - windowRect.Top}\r\n实际宽高: {windowFactWidth}*{windowFactHeight}");
            }
            return false;
        }
    }

    // RECT结构体
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
