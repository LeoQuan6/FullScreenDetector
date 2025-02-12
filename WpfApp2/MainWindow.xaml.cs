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
using static WpfApp.MainWindow;
using static System.Net.Mime.MediaTypeNames;
using System.Threading;

namespace WpfApp
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        public MainWindow()
        {
            InitializeComponent();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(0.1);
            timer.Tick += Timer_Tick;
            timer.Start();
            // Topmost = true;
        }
        private void Timer_Tick(object? sender, EventArgs e)
        {
            // 获取全屏窗口信息并更新UI
            var (fullScreenCount, fullScreenInfo) = GetFullScreenWindowCountAndInfo();
            fullScreenWindowInfoLabel.Content = $"全屏窗口数量: {fullScreenCount}\n{fullScreenInfo}";
        }

        // WinAPI 函数
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

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

        // P/Invoke 获取父窗口句柄
        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        // P/Invoke 获取进程ID
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        // P/Invoke 获取窗口显示状态
        [DllImport("user32.dll")]
        public static extern int GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);


        // 判断前台窗口所在的显示器
        public HeightAndWidth GetForegroundWindowMonitor(RECT windowRect, out POINT WindowPoint)
        {
            HeightAndWidth monitorhw = new();
            
            // 获取窗口左上角的坐标
            WindowPoint = new POINT { X = windowRect.Left, Y = windowRect.Top };
            fullScreenInfo.AppendLine($"窗口左上角坐标: {WindowPoint.X}, {WindowPoint.Y}");
            // 使用 MonitorFromPoint 来获取显示器句柄
            IntPtr hMonitor = MonitorFromPoint(WindowPoint, 2);
            if (hMonitor != IntPtr.Zero)
            {
                // 获取显示器信息
                MONITORINFO monitorInfo = new();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                if (GetMonitorInfo(hMonitor, ref monitorInfo))
                {
                    monitorhw.Width = monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left;
                    monitorhw.Height = monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top;
                    fullScreenInfo.AppendLine($"显示器大小: {monitorhw.Width}, {monitorhw.Height}");
                    return monitorhw;
                }
            }
            // 如果是单个显示器的情况下, 最大化但非全屏窗口的左上角坐标值会是负数, 进而导致返回(0, 0), 或许需要通过真正的多显示器来进行测试
            return monitorhw;
        }

        public static StringBuilder fullScreenInfo = new StringBuilder();
        public (int fullScreenCount, string fullScreenInfo) GetFullScreenWindowCountAndInfo()
        {
            int fullScreenCount = 0;
            fullScreenInfo.Clear();
            IntPtr foreground_hWnd = GetForegroundWindow();
            WindowInfo windowinfo = GetWindowInfo(foreground_hWnd); ;
            if (
                IsWindowFullScreen(windowinfo) &&
                !IsSpecialProgramWindow(windowinfo) &&
                IsWinEtoExplorer(windowinfo)
                )
            {
                fullScreenCount++;
                fullScreenInfo.AppendLine($"全屏窗口: {windowinfo.Title}\r\n宽: {windowinfo.WindowRectHeightAndWidth.Width}, 高: {windowinfo.WindowRectHeightAndWidth.Height}");
                fullScreenInfo.AppendLine($"实际宽高: {windowinfo.WindowFactHeightAndWidth.Width}*{windowinfo.WindowFactHeightAndWidth.Height}");
            }

            return (fullScreenCount, fullScreenInfo.ToString());
        }

        public bool IsWindowFullScreen(WindowInfo windowInfo)
        {
            fullScreenInfo.AppendLine($"窗口类名: {windowInfo.ClassName}");
            // 获取父级窗口标题
            var ownerWindowTitle = GetOwnerWindowTitle(windowInfo.hWnd);
            if (ownerWindowTitle != null)
            {
                fullScreenInfo.AppendLine($"父级窗口标题: {ownerWindowTitle}");
            }
            else
            {
                fullScreenInfo.AppendLine($"父级窗口标题为空");
            }

            fullScreenInfo.AppendLine($"窗口进程名称: {windowInfo.ProcessName}");

            fullScreenInfo.AppendLine($"窗口显示状态: {windowInfo.WindowState}");

            const int tolerance = 10;

            if (Math.Abs(windowInfo.WindowFactHeightAndWidth.Width - windowInfo.ScreenHeightAndWidth.Width) <= tolerance &&
                Math.Abs(windowInfo.WindowFactHeightAndWidth.Height - windowInfo.ScreenHeightAndWidth.Height) == 0)
            {
                return true;
            }
            else if (IsSteamBigPictureMode(windowInfo))
            {
                return true;
            }
            fullScreenInfo.AppendLine($"非全屏窗口: {windowInfo.Title}\r\n宽: {windowInfo.WindowRectHeightAndWidth.Width}, 高: {windowInfo.WindowRectHeightAndWidth.Height}");
            fullScreenInfo.AppendLine($"实际宽高: {windowInfo.WindowFactHeightAndWidth.Width}*{windowInfo.WindowFactHeightAndWidth.Height}");
            return false;
        }

        // 获取父级窗口的标题及进程
        private string? GetOwnerWindowTitle(IntPtr hWnd)
        {
            // 使用 P/Invoke 获取父窗口句柄
            IntPtr ownerHandle = GetParent(hWnd);
            if (ownerHandle != IntPtr.Zero)
            {
                // 获取父级窗口的标题
                StringBuilder title = new StringBuilder(256);
                GetWindowText(ownerHandle, title, title.Capacity);
                return title.ToString();
            }
            return null;
        }

        private bool IsSpecialProgramWindow(WindowInfo windowInfo)
        {
            if (windowInfo.ClassName == "XamlExplorerHostIslandWindow" && windowInfo.ProcessName == "explorer")
            {
                return true; // 当前窗口是任务切换窗口, 不予理睬
            }
            else if (windowInfo.ClassName == "HRSWORD" && windowInfo.ProcessName == "SecAnalysis")
            {
                return true; // 当前窗口是火绒的"安全分析工具", 即使处于最大化, 也会被误识别为全屏, 以此方式暂时解决问题
            }
            return false;
        }

        private bool IsWinEtoExplorer(WindowInfo windowInfo)
        {
            if (windowInfo.ProcessName == "explorer")
            {
                if (windowInfo.ClassName == "CabinetWClass")
                {
                    return true; // 当前窗口是Win + E呼出的资源管理器窗口, 该窗口可以全屏, 应当正常识别
                }
                else if (windowInfo.WindowState == 1)
                {
                    return false;  // 该窗口应该是独显直连情况下的桌面虚拟窗口, 不应被识别为全屏
                }
            }
            return true;  // 应该是其他进程的窗口, 为了不影响正常判断, 默认返回true
        }

        private bool IsSteamBigPictureMode(WindowInfo windowInfo)
        {
            if (
                windowInfo.ClassName == "SDL_app" &&
                windowInfo.ProcessName == "steamwebhelper" &&
                windowInfo.WindowState == 1 &&
                Math.Abs(windowInfo.WindowFactHeightAndWidth.Width - windowInfo.ScreenHeightAndWidth.Width) == 0 &&
                Math.Abs(windowInfo.WindowFactHeightAndWidth.Height - windowInfo.ScreenHeightAndWidth.Height) <= 2 &&
                // 其实以目前我的测试来看高度应该只比正常显示器大小小1像素, 但可惜我没有其他比例的屏幕以及4k的屏幕, 因此放宽到2像素的误差
                (windowInfo.WindowFactHeightAndWidth.Height < windowInfo.ScreenHeightAndWidth.Height)
                // 如此多的条件是为了保证是大屏幕模式而不是非最大化状态下的普通窗口
                )
            {
                return true; // 当前窗口是steam大屏幕模式, 由于steam自身原因造成大屏模式的窗口大小会比全屏小, 以此方式暂时解决问题
            }

            return false;
        }

        public WindowInfo GetWindowInfo(IntPtr hWnd)
        {
            WindowInfo windowInfo = new();

            windowInfo.hWnd = hWnd;

            GetWindowRect(hWnd, out RECT windowRect);
            windowInfo.WindowRect = windowRect;
            windowInfo.WindowRectHeightAndWidth.Width = windowRect.Right - windowRect.Left;
            windowInfo.WindowRectHeightAndWidth.Height = windowRect.Bottom - windowRect.Top;

            GetClientRect(hWnd, out RECT WindowFactRect);
            windowInfo.WindowFactRect = WindowFactRect;
            windowInfo.WindowFactHeightAndWidth.Width = WindowFactRect.Right - WindowFactRect.Left;
            windowInfo.WindowFactHeightAndWidth.Height = WindowFactRect.Bottom - WindowFactRect.Top;

            windowInfo.ScreenHeightAndWidth = GetForegroundWindowMonitor(windowRect, out windowInfo.WindowPoint);

            windowInfo.ClassName = GetWindowClassName(hWnd);

            windowInfo.ProcessName = GetWindowProcessInfo(hWnd);

            windowInfo.WindowState = GetWindowState(hWnd);

            windowInfo.Title = GetWindowTitle(hWnd);

            return windowInfo;
        }

        // 获取窗口所属进程的辅助方法
        private string GetWindowProcessInfo(IntPtr hWnd)
        {
            int processId;
            GetWindowThreadProcessId(hWnd, out processId);
            Process process = Process.GetProcessById(processId);
            return process.ProcessName;
        }

        private string GetWindowClassName(IntPtr hWnd)
        {
            StringBuilder className = new StringBuilder(256);
            _ = GetClassName(hWnd, className, className.Capacity);
            return className.ToString();
        }

        private string GetWindowTitle(IntPtr hWnd)
        { 
            StringBuilder title = new StringBuilder(256);
            _ = GetWindowText(hWnd, title, title.Capacity);
            return title.ToString();
        }
        /// <summary>
        /// 获取窗口显示状态的辅助方法
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        private static int GetWindowState(IntPtr hWnd)
        {
            WINDOWPLACEMENT placement = new();
            _ = GetWindowPlacement(hWnd, ref placement);
            switch (placement.showCmd)
            {
                case 1:
                    return 1;  // "正常显示"
                case 3:
                    return 3;  // "最大化"
                case 2:
                    return 2;  // "最小化"
                default:
                    return 0; //  "其他异常可能性"
            }
        }
    }

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

    /// <summary>
    /// 通用窗口或屏幕 RECT结构体
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// WINDOWPLACEMENT 结构体
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    /// <summary>
    /// 用于定义窗口或显示器的宽高
    /// </summary>
    public struct HeightAndWidth
    {
        public int Height;
        public int Width;
    }

    /// <summary>
    /// 要用到的关于窗口的全部信息
    /// </summary>
    public struct WindowInfo
    {
        /// <summary>
        /// 窗口句柄
        /// </summary>
        public IntPtr hWnd;

        /// <summary>
        /// 窗口实际宽高
        /// </summary>
        public HeightAndWidth WindowFactHeightAndWidth;

        /// <summary>
        /// 窗口工作区域信息
        /// </summary>
        public RECT WindowFactRect;

        /// <summary>
        /// 窗口宽高
        /// </summary>
        public HeightAndWidth WindowRectHeightAndWidth;

        /// <summary>
        /// 窗口完整区域信息
        /// </summary>
        public RECT WindowRect;

        /// <summary>
        /// 窗口左上角坐标
        /// </summary>
        public POINT WindowPoint;

        /// <summary>
        /// 窗口所在屏幕的分辨率
        /// </summary>
        public HeightAndWidth ScreenHeightAndWidth;

        /// <summary>
        /// 窗口标题
        /// </summary>
        public string Title;

        /// <summary>
        /// 窗口类名
        /// </summary>
        public string ClassName;

        /// <summary>
        /// 窗口对应进程名称
        /// </summary>
        public string ProcessName;

        /// <summary>
        /// 窗口显示状态
        /// </summary>
        public int WindowState;
    }
}
