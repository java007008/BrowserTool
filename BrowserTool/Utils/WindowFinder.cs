using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace BrowserTool.Utils
{

    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    public class WindowFinder
    {
        // 委托定义
        public delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lParam);

        // Windows API 声明
        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        // 窗口显示状态常量
        const int SW_RESTORE = 9;
        const int SW_SHOW = 5;
        const int SW_SHOWNORMAL = 1;

        // GetWindow 常量
        const uint GW_OWNER = 4;

        // 窗口放置结构
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

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private readonly ILogger _logger;

        // 构造函数 - 传入日志记录器
        public WindowFinder(ILogger logger)
        {
            _logger = logger;
        }

        // 简化的日志方法
        private void Log(string message)
        {
            _logger?.Debug(message);
        }

        /// <summary>
        /// 方案1：遍历所有窗口查找（包括最小化和托盘窗口）
        /// </summary>
        /// <param name="targetWindowTitle"></param>
        /// <returns></returns>
        public async Task<IntPtr> FindAndActivateImWindow(string targetWindowTitle)
        {
            Log($"开始查找窗口: 【{targetWindowTitle}】");

            IntPtr windowHandle = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                // 不再检查 IsWindowVisible，因为最小化窗口会返回 false
                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(hWnd, windowTitle, 256);

                string title = windowTitle.ToString();

                // 使用Contains进行模糊匹配
                if (!string.IsNullOrEmpty(title) && title.Contains(targetWindowTitle))
                {
                    // 检查是否为有效的顶级窗口（排除子窗口和工具窗口）
                    if (IsValidWindow(hWnd))
                    {
                        windowHandle = hWnd;

                        // 检查窗口状态
                        string windowState = GetWindowState(hWnd);
                        Log($"找到目标窗口: {title} (句柄: {windowHandle}) 状态: {windowState}");
                        return false; // 停止枚举
                    }
                }
                return true; // 继续枚举
            }, IntPtr.Zero);

            if (windowHandle == IntPtr.Zero)
            {
                Log("未找到目标窗口");
                return IntPtr.Zero;
            }

            // 恢复并激活窗口
            await RestoreAndActivateWindow(windowHandle);
            return windowHandle;
        }

        /// <summary>
        /// 检查是否为有效的目标窗口
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        private bool IsValidWindow(IntPtr hWnd)
        {
            // 排除没有所有者且不可见的窗口（可能是隐藏的系统窗口）
            IntPtr owner = GetWindow(hWnd, GW_OWNER);

            // 获取窗口类名，排除一些系统窗口
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, 256);
            string classNameStr = className.ToString();

            // 排除一些系统窗口类名
            string[] excludeClasses = { "Shell_TrayWnd", "Progman", "WorkerW", "DV2ControlHost" };
            if (excludeClasses.Contains(classNameStr))
                return false;

            return true;
        }

        /// <summary>
        /// 获取窗口状态描述
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        private string GetWindowState(IntPtr hWnd)
        {
            if (IsIconic(hWnd))
                return "最小化";

            if (IsWindowVisible(hWnd))
                return "可见";

            return "隐藏/托盘";
        }

        /// <summary>
        /// 恢复并激活窗口
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        private async Task RestoreAndActivateWindow(IntPtr hWnd)
        {
            try
            {
                if (IsIconic(hWnd))
                {
                    Log("窗口已最小化，正在恢复...");
                    ShowWindow(hWnd, SW_RESTORE);
                }
                else if (!IsWindowVisible(hWnd))
                {
                    Log("窗口隐藏在托盘，正在显示...");
                    ShowWindow(hWnd, SW_SHOW);
                }

                // 等待窗口恢复
                await Task.Delay(200);

                // 置前台
                SetForegroundWindow(hWnd);
                Log("窗口已激活");
            }
            catch (Exception ex)
            {
                Log($"恢复窗口时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 方案2：增强版 - 包含进程信息和窗口类名（支持最小化和托盘）
        /// </summary>
        /// <param name="targetWindowTitle"></param>
        /// <returns></returns>
        public async Task<IntPtr> FindAndActivateImWindowEnhanced(string targetWindowTitle)
        {
            Log($"开始查找窗口: 【{targetWindowTitle}】");

            IntPtr windowHandle = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                // 不再检查 IsWindowVisible
                StringBuilder windowTitle = new StringBuilder(256);
                StringBuilder className = new StringBuilder(256);

                GetWindowText(hWnd, windowTitle, 256);
                GetClassName(hWnd, className, 256);

                string title = windowTitle.ToString();
                string classNameStr = className.ToString();

                // 获取进程信息
                GetWindowThreadProcessId(hWnd, out uint processId);
                try
                {
                    Process process = Process.GetProcessById((int)processId);
                    string processName = process.ProcessName;
                    string windowState = GetWindowState(hWnd);

                    Log($"检查窗口: '{title}' | 类名: {classNameStr} | 进程: {processName} | 状态: {windowState}");

                    // 使用Contains进行模糊匹配
                    if (!string.IsNullOrEmpty(title) && title.Contains(targetWindowTitle))
                    {
                        if (IsValidWindow(hWnd))
                        {
                            windowHandle = hWnd;
                            Log($"找到目标窗口: {title} (句柄: {windowHandle}) | 进程: {processName} | 状态: {windowState}");
                            return false; // 停止枚举
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 进程可能已经退出，忽略异常
                }
                return true; // 继续枚举
            }, IntPtr.Zero);

            if (windowHandle == IntPtr.Zero)
            {
                Log("未找到目标窗口");
                return IntPtr.Zero;
            }

            // 恢复并激活窗口
            await RestoreAndActivateWindow(windowHandle);
            return windowHandle;
        }

        /// <summary>
        /// 调试方法：列出所有窗口（包括最小化和托盘）
        /// </summary>
        public void ListAllWindows()
        {
            Log("列出所有窗口（包括最小化和托盘）:");

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder windowTitle = new StringBuilder(256);
                StringBuilder className = new StringBuilder(256);

                GetWindowText(hWnd, windowTitle, 256);
                GetClassName(hWnd, className, 256);

                string title = windowTitle.ToString();
                string classNameStr = className.ToString();

                // 只显示有标题的窗口
                if (!string.IsNullOrEmpty(title))
                {
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    try
                    {
                        Process process = Process.GetProcessById((int)processId);
                        string windowState = GetWindowState(hWnd);
                        bool isValid = IsValidWindow(hWnd);

                        Log($"窗口: '{title}' | 类名: '{classNameStr}' | 进程: {process.ProcessName} | 状态: {windowState} | 有效: {isValid} | 句柄: {hWnd}");
                    }
                    catch
                    {
                        string windowState = GetWindowState(hWnd);
                        Log($"窗口: '{title}' | 类名: '{classNameStr}' | 状态: {windowState} | 句柄: {hWnd}");
                    }
                }
                return true;
            }, IntPtr.Zero);
        }

        /// <summary>
        /// 方案3：按进程名查找窗口（支持最小化和托盘）
        /// </summary>
        /// <param name="processName"></param>
        /// <param name="windowTitlePart"></param>
        /// <returns></returns>
        public async Task<IntPtr> FindWindowByProcessName(string processName, string windowTitlePart)
        {
            Log($"按进程名查找窗口: 进程={processName}, 窗口包含={windowTitlePart}");

            IntPtr windowHandle = IntPtr.Zero;

            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process process in processes)
            {
                try
                {
                    // 枚举该进程的所有窗口
                    EnumWindows((hWnd, lParam) =>
                    {
                        GetWindowThreadProcessId(hWnd, out uint processId);

                        if (processId == process.Id)
                        {
                            StringBuilder windowTitle = new StringBuilder(256);
                            GetWindowText(hWnd, windowTitle, 256);

                            string title = windowTitle.ToString();
                            if (!string.IsNullOrEmpty(title) && title.Contains(windowTitlePart))
                            {
                                if (IsValidWindow(hWnd))
                                {
                                    windowHandle = hWnd;
                                    string windowState = GetWindowState(hWnd);
                                    Log($"找到目标窗口: {title} (句柄: {windowHandle}) 状态: {windowState}");
                                    return false; // 停止枚举
                                }
                            }
                        }
                        return true;
                    }, IntPtr.Zero);

                    if (windowHandle != IntPtr.Zero)
                        break;
                }
                catch (Exception ex)
                {
                    Log($"检查进程 {process.ProcessName} 时出错: {ex.Message}");
                }
            }

            if (windowHandle != IntPtr.Zero)
            {
                await RestoreAndActivateWindow(windowHandle);
            }

            return windowHandle;
        }

        /// <summary>
        /// 方案4：按窗口类名查找（适用于CEFSharp等应用）
        /// </summary>
        /// <param name="className"></param>
        /// <param name="windowTitlePart"></param>
        /// <returns></returns>
        public async Task<IntPtr> FindWindowByClassName(string className, string windowTitlePart = null)
        {
            Log($"按窗口类名查找: 类名={className}, 窗口包含={windowTitlePart}");

            IntPtr windowHandle = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder classNameBuilder = new StringBuilder(256);
                GetClassName(hWnd, classNameBuilder, 256);

                string actualClassName = classNameBuilder.ToString();

                // 类名匹配（支持模糊匹配）
                if (actualClassName.Contains(className))
                {
                    StringBuilder windowTitle = new StringBuilder(256);
                    GetWindowText(hWnd, windowTitle, 256);
                    string title = windowTitle.ToString();

                    // 如果指定了窗口标题，则需要标题也匹配
                    bool titleMatches = string.IsNullOrEmpty(windowTitlePart) ||
                                       (!string.IsNullOrEmpty(title) && title.Contains(windowTitlePart));

                    if (titleMatches && IsValidWindow(hWnd))
                    {
                        windowHandle = hWnd;
                        string windowState = GetWindowState(hWnd);
                        GetWindowThreadProcessId(hWnd, out uint processId);

                        try
                        {
                            Process process = Process.GetProcessById((int)processId);
                            Log($"找到目标窗口: '{title}' | 类名: {actualClassName} | 进程: {process.ProcessName} | 状态: {windowState}");
                        }
                        catch
                        {
                            Log($"找到目标窗口: '{title}' | 类名: {actualClassName} | 状态: {windowState}");
                        }
                        return false; // 停止枚举
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (windowHandle != IntPtr.Zero)
            {
                await RestoreAndActivateWindow(windowHandle);
            }
            else
            {
                Log("未找到匹配的窗口");
            }

            return windowHandle;
        }

        /// <summary>
        /// 方案5：组合条件查找（类名 + 窗口标题 + 进程名）
        /// </summary>
        /// <param name="className"></param>
        /// <param name="windowTitlePart"></param>
        /// <param name="processName"></param>
        /// <returns></returns>
        public async Task<IntPtr> FindWindowByMultipleConditions(
            string className = null,
            string windowTitlePart = null,
            string processName = null)
        {
            Log($"多条件查找窗口: 类名={className}, 标题包含={windowTitlePart}, 进程={processName}");

            IntPtr windowHandle = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                bool matches = true;

                // 检查类名
                if (!string.IsNullOrEmpty(className))
                {
                    StringBuilder classNameBuilder = new StringBuilder(256);
                    GetClassName(hWnd, classNameBuilder, 256);
                    string actualClassName = classNameBuilder.ToString();

                    if (!actualClassName.Contains(className))
                    {
                        matches = false;
                    }
                }

                // 检查窗口标题
                if (matches && !string.IsNullOrEmpty(windowTitlePart))
                {
                    StringBuilder windowTitle = new StringBuilder(256);
                    GetWindowText(hWnd, windowTitle, 256);
                    string title = windowTitle.ToString();

                    if (string.IsNullOrEmpty(title) || !title.Contains(windowTitlePart))
                    {
                        matches = false;
                    }
                }

                // 检查进程名
                if (matches && !string.IsNullOrEmpty(processName))
                {
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    try
                    {
                        Process process = Process.GetProcessById((int)processId);
                        if (!process.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                        {
                            matches = false;
                        }
                    }
                    catch
                    {
                        matches = false;
                    }
                }

                if (matches && IsValidWindow(hWnd))
                {
                    windowHandle = hWnd;

                    // 记录找到的窗口信息
                    StringBuilder titleBuilder = new StringBuilder(256);
                    StringBuilder classBuilder = new StringBuilder(256);
                    GetWindowText(hWnd, titleBuilder, 256);
                    GetClassName(hWnd, classBuilder, 256);

                    string windowState = GetWindowState(hWnd);
                    GetWindowThreadProcessId(hWnd, out uint processId);

                    try
                    {
                        Process process = Process.GetProcessById((int)processId);
                        Log($"找到目标窗口: '{titleBuilder}' | 类名: {classBuilder} | 进程: {process.ProcessName} | 状态: {windowState}");
                    }
                    catch
                    {
                        Log($"找到目标窗口: '{titleBuilder}' | 类名: {classBuilder} | 状态: {windowState}");
                    }

                    return false; // 停止枚举
                }

                return true;
            }, IntPtr.Zero);

            if (windowHandle != IntPtr.Zero)
            {
                await RestoreAndActivateWindow(windowHandle);
            }
            else
            {
                Log("未找到匹配的窗口");
            }

            return windowHandle;
        }

        /// <summary>
        /// CEF/CEFSharp 常见窗口类名查找
        /// </summary>
        /// <param name="windowTitlePart"></param>
        /// <returns></returns>
        public async Task<IntPtr> FindCefSharpWindow(string windowTitlePart = null)
        {
            Log($"查找CEFSharp窗口，标题包含: {windowTitlePart}");

            // CEFSharp/CEF常见的窗口类名
            string[] cefClassNames = {
            "CefBrowserWindow",
            "Chrome_WidgetWin_1",
            "Chrome_WidgetWin_0",
            "CefSharpBrowserHost",
            "ChromiumWebBrowser"
        };

            foreach (string className in cefClassNames)
            {
                Log($"尝试查找类名: {className}");
                IntPtr handle = await FindWindowByClassName(className, windowTitlePart);
                if (handle != IntPtr.Zero)
                {
                    return handle;
                }
            }

            Log("未找到CEFSharp窗口");
            return IntPtr.Zero;
        }

        /// <summary>
        /// 额外方法：强制查找特定进程的所有窗口
        /// </summary>
        /// <param name="processName"></param>
        public void ListWindowsByProcess(string processName)
        {
            Log($"列出进程 {processName} 的所有窗口:");

            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process process in processes)
            {
                Log($"进程ID: {process.Id}, 主窗口句柄: {process.MainWindowHandle}");

                EnumWindows((hWnd, lParam) =>
                {
                    GetWindowThreadProcessId(hWnd, out uint processId);

                    if (processId == process.Id)
                    {
                        StringBuilder windowTitle = new StringBuilder(256);
                        StringBuilder className = new StringBuilder(256);

                        GetWindowText(hWnd, windowTitle, 256);
                        GetClassName(hWnd, className, 256);

                        string title = windowTitle.ToString();
                        string classNameStr = className.ToString();
                        string windowState = GetWindowState(hWnd);
                        bool isValid = IsValidWindow(hWnd);

                        Log($"  窗口: '{title}' | 类名: '{classNameStr}' | 状态: {windowState} | 有效: {isValid} | 句柄: {hWnd}");
                    }
                    return true;
                }, IntPtr.Zero);
            }
        }

        /// <summary>
        /// 列出特定类名的所有窗口
        /// </summary>
        /// <param name="className"></param>
        public void ListWindowsByClassName(string className)
        {
            Log($"列出类名包含 '{className}' 的所有窗口:");

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder classNameBuilder = new StringBuilder(256);
                GetClassName(hWnd, classNameBuilder, 256);

                string actualClassName = classNameBuilder.ToString();

                if (actualClassName.Contains(className))
                {
                    StringBuilder windowTitle = new StringBuilder(256);
                    GetWindowText(hWnd, windowTitle, 256);

                    string title = windowTitle.ToString();
                    string windowState = GetWindowState(hWnd);
                    bool isValid = IsValidWindow(hWnd);

                    GetWindowThreadProcessId(hWnd, out uint processId);
                    try
                    {
                        Process process = Process.GetProcessById((int)processId);
                        Log($"窗口: '{title}' | 完整类名: '{actualClassName}' | 进程: {process.ProcessName} | 状态: {windowState} | 有效: {isValid} | 句柄: {hWnd}");
                    }
                    catch
                    {
                        Log($"窗口: '{title}' | 完整类名: '{actualClassName}' | 状态: {windowState} | 有效: {isValid} | 句柄: {hWnd}");
                    }
                }
                return true;
            }, IntPtr.Zero);
        }

        /// <summary>
        /// 专门用于调试CEF窗口
        /// </summary>
        public void ListAllCefWindows()
        {
            Log("列出所有可能的CEF/CEFSharp窗口:");

            string[] cefKeywords = { "Cef", "Chrome", "Chromium", "Browser" };

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder windowTitle = new StringBuilder(256);
                StringBuilder className = new StringBuilder(256);

                GetWindowText(hWnd, windowTitle, 256);
                GetClassName(hWnd, className, 256);

                string title = windowTitle.ToString();
                string classNameStr = className.ToString();

                // 检查类名或标题是否包含CEF相关关键词
                bool isCefRelated = cefKeywords.Any(keyword =>
                    classNameStr.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    title.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                if (isCefRelated && !string.IsNullOrEmpty(title))
                {
                    string windowState = GetWindowState(hWnd);
                    bool isValid = IsValidWindow(hWnd);

                    GetWindowThreadProcessId(hWnd, out uint processId);
                    try
                    {
                        Process process = Process.GetProcessById((int)processId);
                        Log($"CEF窗口: '{title}' | 类名: '{classNameStr}' | 进程: {process.ProcessName} | 状态: {windowState} | 有效: {isValid} | 句柄: {hWnd}");
                    }
                    catch
                    {
                        Log($"CEF窗口: '{title}' | 类名: '{classNameStr}' | 状态: {windowState} | 有效: {isValid} | 句柄: {hWnd}");
                    }
                }
                return true;
            }, IntPtr.Zero);
        }
    }

    // 使用示例
    /*
    public class WindowFinderExample
    {
        public static async Task Main()
        {
            // 假设这些已经初始化
            ILogger logger = new YourLoggerImplementation();

            var windowFinder = new WindowFinder(logger);

            // 针对CEFSharp应用的调试步骤：

            // 1. 首先列出所有CEF相关窗口
            Console.WriteLine("=== 列出所有CEF窗口 ===");
            windowFinder.ListAllCefWindows();

            // 2. 如果知道具体的类名关键词，可以精确查找
            Console.WriteLine("\n=== 列出包含'Chrome'的窗口类名 ===");
            windowFinder.ListWindowsByClassName("Chrome");

            // 3. 如果知道进程名，列出该进程的所有窗口
            Console.WriteLine("\n=== 列出特定进程的窗口 ===");
            windowFinder.ListWindowsByProcess("YourAppName"); // 替换为实际进程名

            // 查找方法示例：

            // 方法1：按CEF窗口类名查找
            IntPtr handle1 = await windowFinder.FindCefSharpWindow("Talk");

            // 方法2：按具体类名查找
            IntPtr handle2 = await windowFinder.FindWindowByClassName("Chrome_WidgetWin_1", "Talk");

            // 方法3：多条件组合查找（最精确）
            IntPtr handle3 = await windowFinder.FindWindowByMultipleConditions(
                className: "Chrome_WidgetWin_1",
                windowTitlePart: "Talk", 
                processName: "YourAppName"
            );

            // 方法4：如果原有方法失效，回退到传统方法
            IntPtr handle4 = await windowFinder.FindAndActivateImWindow("Talk");
        }
    }
    */
}
