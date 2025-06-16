using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System.Configuration;
using System.Linq;
using NLog;
using System.Collections.Generic;
using System.Text.Json;

namespace BrowserTool.Utils 
{

    /// <summary>
    /// 自动模拟器
    /// 根据配置定时执行自动流程
    /// </summary>
    public class AutoCheckInSimulator
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        #region Win32 API 声明

        /// <summary>
        /// 查找指定窗口的Win32 API函数
        /// </summary>
        /// <param name="lpClassName">窗口类名</param>
        /// <param name="lpWindowName">窗口标题</param>
        /// <returns>窗口句柄，找不到返回IntPtr.Zero</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        /// <summary>
        /// 根据进程ID查找窗口
        /// </summary>
        /// <param name="hWndParent">父窗口句柄</param>
        /// <param name="hWndChildAfter">子窗口句柄</param>
        /// <param name="lpszClass">窗口类名</param>
        /// <param name="lpszWindow">窗口标题</param>
        /// <returns>窗口句柄</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

        /// <summary>
        /// 将指定窗口设置为前台窗口
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>成功返回true</returns>
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// 显示指定窗口
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="nCmdShow">显示状态</param>
        /// <returns>成功返回true</returns>
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// 获取窗口标题文本
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpString">输出缓冲区</param>
        /// <param name="nMaxCount">缓冲区大小</param>
        /// <returns>复制的字符数</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        // 委托定义
        public delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool EnumThreadWindows(int dwThreadId, EnumWindowsDelegate lpfn, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>
        /// 模拟鼠标点击事件
        /// </summary>
        /// <param name="dwFlags">鼠标事件标志</param>
        /// <param name="dx">X坐标</param>
        /// <param name="dy">Y坐标</param>
        /// <param name="dwData">滚轮数据</param>
        /// <param name="dwExtraInfo">额外信息</param>
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        /// <summary>
        /// 设置鼠标光标位置
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>成功返回true</returns>
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        /// <summary>
        /// 获取鼠标光标位置
        /// </summary>
        /// <param name="lpPoint">输出参数，返回鼠标当前位置的坐标</param>
        /// <returns>成功返回true，失败返回false</returns>
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>
        /// 向指定窗口发送消息
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="msg">消息</param>
        /// <param name="wParam">参数1</param>
        /// <param name="lParam">参数2</param>
        /// <returns>消息处理结果</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 后台点击消息
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="msg">消息</param>
        /// <param name="wParam">参数1</param>
        /// <param name="lParam">参数2</param>
        /// <returns>消息处理结果</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 屏幕坐标转换为客户区坐标
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpPoint">坐标点</param>
        /// <returns>成功返回true</returns>
        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        /// <summary>
        /// 发送键盘输入
        /// </summary>
        /// <param name="nInputs">输入数量</param>
        /// <param name="pInputs">输入数组</param>
        /// <param name="cbSize">结构体大小</param>
        /// <returns>成功处理的输入数量</returns>
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        #endregion

        #region 结构体定义

        /// <summary>
        /// 表示屏幕坐标点的结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            /// <summary>X坐标</summary>
            public int X;
            /// <summary>Y坐标</summary>
            public int Y;
        }

        /// <summary>
        /// 输入结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION ui;
        }

        /// <summary>
        /// 输入联合体
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        /// <summary>
        /// 鼠标输入结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// 键盘输入结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        /// <summary>
        /// 打卡步骤执行结果
        /// </summary>
        private class CheckInStepResult
        {
            /// <summary>是否成功</summary>
            public bool IsSuccess { get; set; }
            /// <summary>失败步骤编号</summary>
            public int FailedStep { get; set; }
            /// <summary>失败原因</summary>
            public string FailureReason { get; set; }
            /// <summary>异常信息（如果有）</summary>
            public Exception Exception { get; set; }

            /// <summary>
            /// 创建成功结果
            /// </summary>
            public static CheckInStepResult Success()
            {
                return new CheckInStepResult { IsSuccess = true };
            }

            /// <summary>
            /// 创建失败结果
            /// </summary>
            public static CheckInStepResult Failure(int step, string reason, Exception ex = null)
            {
                return new CheckInStepResult
                {
                    IsSuccess = false,
                    FailedStep = step,
                    FailureReason = reason,
                    Exception = ex
                };
            }

            /// <summary>
            /// 获取详细的失败信息
            /// </summary>
            public string GetDetailedFailureInfo()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"步骤 {FailedStep} 失败");
                sb.AppendLine($"原因: {FailureReason}");
                if (Exception != null)
                {
                    sb.AppendLine($"异常: {Exception.Message}");
                    if (Exception.InnerException != null)
                    {
                        sb.AppendLine($"内部异常: {Exception.InnerException.Message}");
                    }
                }
                return sb.ToString();
            }
        }

        #endregion

        #region 枚举定义

        /// <summary>
        /// 点击方法枚举
        /// </summary>
        public enum ClickMethod
        {
            /// <summary>自动模式（依次尝试多种方法）</summary>
            Auto,
            /// <summary>仅前台点击</summary>
            ForegroundOnly,
            /// <summary>仅后台点击</summary>
            BackgroundOnly,
            /// <summary>使用SendInput</summary>
            SendInput,
            /// <summary>双击</summary>
            DoubleClick
        }

        #endregion

        #region 常量定义

        /// <summary>鼠标左键按下事件</summary>
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        /// <summary>鼠标左键抬起事件</summary>
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        /// <summary>窗口正常显示</summary>
        private const int SW_SHOWNORMAL = 1;
        /// <summary>窗口最大化显示</summary>
        private const int SW_SHOWMAXIMIZED = 3;

        /// <summary>鼠标左键按下消息</summary>
        private const uint WM_LBUTTONDOWN = 0x0201;
        /// <summary>鼠标左键抬起消息</summary>
        private const uint WM_LBUTTONUP = 0x0202;

        /// <summary>输入类型 - 鼠标</summary>
        private const uint INPUT_MOUSE = 0;
        /// <summary>输入类型 - 键盘</summary>
        private const uint INPUT_KEYBOARD = 1;
        /// <summary>按键抬起标志</summary>
        private const uint KEYEVENTF_KEYUP = 0x0002;
        /// <summary>Ctrl键虚拟键码</summary>
        private const ushort VK_CONTROL = 0x11;
        /// <summary>W键虚拟键码</summary>
        private const ushort VK_W = 0x57;

        /// <summary>默认IM窗口标题关键字</summary>
        private const string DEFAULT_IM_WINDOW_TITLE = "Talk";
        /// <summary>默认弹窗标题关键字</summary>
        private const string DEFAULT_POPUP_WINDOW_TITLE = "考勤";

        #endregion

        #region 私有字段

        /// <summary>取消令牌源</summary>
        private CancellationTokenSource _cancellationTokenSource;
        /// <summary>是否正在运行</summary>
        private bool _isRunning;
        /// <summary>配置信息</summary>
        private CheckInConfig _config;
        /// <summary>随机数生成器</summary>
        private Random _random;
        private WindowFinder windowFinder;
        /// <summary>打卡记录</summary>
        private CheckInRecord _checkInRecord;
        /// <summary>打卡记录文件路径</summary>
        private readonly string _checkInRecordPath = @"CEF\checkin_record.json";

        #endregion

        #region 配置类定义

        /// <summary>
        /// 配置信息
        /// </summary>
        public class CheckInConfig
        {
            /// <summary>功能开关</summary>
            public bool IsEnabled { get; set; } = false;
            /// <summary>早上时间</summary>
            public TimeSpan MorningTime { get; set; } = new TimeSpan(9, 0, 0);
            /// <summary>晚上时间</summary>
            public TimeSpan EveningTime { get; set; } = new TimeSpan(18, 0, 0);
            /// <summary>随机时间范围（分钟）</summary>
            public int RandomMinutes { get; set; } = 10;

            // 以下为默认值配置
            /// <summary>IM窗口标题关键字</summary>
            public string ImWindowTitle { get; set; } = DEFAULT_IM_WINDOW_TITLE;
            /// <summary>弹窗标题关键字</summary>
            public string PopupWindowTitle { get; set; } = DEFAULT_POPUP_WINDOW_TITLE;
            /// <summary>第一个图片路径</summary>
            public string FirstImagePath { get; set; } = "Resources/checkin_icon1.png";
            /// <summary>第二个图片路径</summary>
            public string SecondImagePath { get; set; } = "Resources/checkin_icon2.png";
            /// <summary>Python脚本路径</summary>
            public string PythonScriptPath { get; set; } = "Resources/image_matcher.py";
            /// <summary>操作超时时间（秒）</summary>
            public int OperationTimeoutSeconds { get; set; } = 30;
            /// <summary>浏览器进程名称列表</summary>
            public string[] BrowserProcessNames { get; set; } = { "chrome", "msedge", "firefox" };
            /// <summary>成功标识关键字</summary>
            public string[] SuccessKeywords { get; set; } = { "成功", "成功" };
            /// <summary>默认点击方法</summary>
            public ClickMethod DefaultClickMethod { get; set; } = ClickMethod.Auto;
        }

        /// <summary>
        /// 打卡记录
        /// </summary>
        private class CheckInRecord
        {
            /// <summary>早上打卡时间</summary>
            public DateTime? MorningCheckIn { get; set; }
            /// <summary>晚上打卡时间</summary>
            public DateTime? EveningCheckIn { get; set; }
            /// <summary>记录日期</summary>
            public DateTime RecordDate { get; set; }

            /// <summary>
            /// 检查是否已经完成指定类型的打卡
            /// </summary>
            /// <param name="isMorning">是否是早上打卡</param>
            /// <returns>是否已完成打卡</returns>
            public bool HasCheckedIn(bool isMorning)
            {
                // 检查记录是否是今天的
                if (RecordDate.Date != DateTime.Now.Date)
                {
                    return false;
                }

                return isMorning ? MorningCheckIn.HasValue : EveningCheckIn.HasValue;
            }

            /// <summary>
            /// 记录打卡时间
            /// </summary>
            /// <param name="isMorning">是否是早上打卡</param>
            public void RecordCheckIn(bool isMorning)
            {
                if (RecordDate.Date != DateTime.Now.Date)
                {
                    // 如果是新的一天，重置记录
                    RecordDate = DateTime.Now.Date;
                    MorningCheckIn = null;
                    EveningCheckIn = null;
                }

                if (isMorning)
                {
                    MorningCheckIn = DateTime.Now;
                }
                else
                {
                    EveningCheckIn = DateTime.Now;
                }
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化自动模拟器
        /// </summary>
        public AutoCheckInSimulator()
        {
            _random = new Random();
            LoadConfiguration();
            windowFinder = new WindowFinder(_logger);
            LoadCheckInRecord();
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取模拟器是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 获取或设置配置信息
        /// </summary>
        public CheckInConfig Config
        {
            get => _config;
            set => _config = value;
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 从配置文件加载配置
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                _config = new CheckInConfig();

                // 从app.config读取配置 - 只读取指定的4个配置项
                var appSettings = ConfigurationManager.AppSettings;

                _logger.Debug("开始加载配置...");
                _logger.Debug($"CheckInEnabled原始值: {appSettings["CheckInEnabled"]}");
                _logger.Debug($"MorningCheckInTime原始值: {appSettings["MorningCheckInTime"]}");
                _logger.Debug($"EveningCheckInTime原始值: {appSettings["EveningCheckInTime"]}");
                _logger.Debug($"RandomMinutes原始值: {appSettings["RandomMinutes"]}");

                if (bool.TryParse(appSettings["CheckInEnabled"], out bool enabled))
                    _config.IsEnabled = enabled;
                else
                    _logger.Warn($"无法解析CheckInEnabled值: {appSettings["CheckInEnabled"]}");

                if (TimeSpan.TryParse(appSettings["MorningCheckInTime"], out TimeSpan morningTime))
                    _config.MorningTime = morningTime;
                else
                    _logger.Warn($"无法解析MorningCheckInTime值: {appSettings["MorningCheckInTime"]}");

                if (TimeSpan.TryParse(appSettings["EveningCheckInTime"], out TimeSpan eveningTime))
                    _config.EveningTime = eveningTime;
                else
                    _logger.Warn($"无法解析EveningCheckInTime值: {appSettings["EveningCheckInTime"]}");

                if (int.TryParse(appSettings["RandomMinutes"], out int randomMinutes))
                    _config.RandomMinutes = randomMinutes;
                else
                    _logger.Warn($"无法解析RandomMinutes值: {appSettings["RandomMinutes"]}");

                // 其他配置使用默认值，不从配置文件读取

                _logger.Debug($"配置加载完成: 功能开关={_config.IsEnabled}, 早上={_config.MorningTime}, 晚上={_config.EveningTime}, 随机范围={_config.RandomMinutes}分钟");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"加载配置时发生异常: {ex.Message}");
                _logger.Error($"异常堆栈: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.Error($"内部异常: {ex.InnerException.Message}");
                    _logger.Error($"内部异常堆栈: {ex.InnerException.StackTrace}");
                }
                _config = new CheckInConfig(); // 使用默认配置
            }
        }

        #endregion

        #region 时间调度

        /// <summary>
        /// 计算下次执行时间
        /// </summary>
        /// <returns>下次执行的DateTime</returns>
        private DateTime CalculateNextExecutionTime()
        {
            var now = DateTime.Now;
            var today = now.Date;

            // 计算今天的早上和晚上时间（加入随机偏移）
            var morningTarget = today.Add(_config.MorningTime).AddMinutes(-_random.Next(0, _config.RandomMinutes + 1));
            var eveningTarget = today.Add(_config.EveningTime).AddMinutes(_random.Next(0, _config.RandomMinutes + 1));

            // 判断下次执行时间
            if (now < morningTarget)
            {
                _logger.Debug($"下次执行时间: {morningTarget:yyyy-MM-dd HH:mm:ss} (早上)");
                return morningTarget;
            }
            else if (now < eveningTarget)
            {
                _logger.Debug($"下次执行时间: {eveningTarget:yyyy-MM-dd HH:mm:ss} (晚上)");
                return eveningTarget;
            }
            else
            {
                // 今天的时间都过了，计算明天早上的时间
                var tomorrowMorning = today.AddDays(1).Add(_config.MorningTime).AddMinutes(-_random.Next(0, _config.RandomMinutes + 1));
                _logger.Debug($"下次执行时间: {tomorrowMorning:yyyy-MM-dd HH:mm:ss} (明天早上)");
                return tomorrowMorning;
            }
        }

        #endregion

        #region 窗口操作

        /// <summary>
        /// 查找并置顶IM窗口
        /// </summary>
        /// <returns>窗口句柄，找不到返回IntPtr.Zero</returns>
        //private async Task<IntPtr> FindAndActivateImWindow()
        //{
        //    _logger.Debug($"开始查找窗口: {_config.ImWindowTitle}");

        //    // 查找包含指定标题的窗口
        //    IntPtr windowHandle = IntPtr.Zero;

        //    // 枚举所有窗口查找匹配的窗口
        //    Process[] processes = Process.GetProcesses();
        //    foreach (Process process in processes)
        //    {
        //        if (process.MainWindowHandle != IntPtr.Zero)
        //        {
        //            StringBuilder windowTitle = new StringBuilder(256);
        //            GetWindowText(process.MainWindowHandle, windowTitle, 256);

        //            if (windowTitle.ToString().Contains(_config.ImWindowTitle))
        //            {
        //                windowHandle = process.MainWindowHandle;
        //                _logger.Debug($"找到IM窗口: {windowTitle} (句柄: {windowHandle})");
        //                break;
        //            }
        //        }
        //    }

        //    if (windowHandle == IntPtr.Zero)
        //    {
        //        _logger.Debug("未找到IM窗口");
        //        return IntPtr.Zero;
        //    }

        //    // 置顶窗口
        //    if (!SetForegroundWindow(windowHandle))
        //    {
        //        _logger.Debug("置顶IM窗口失败");
        //        return IntPtr.Zero;
        //    }

        //    ShowWindow(windowHandle, SW_SHOWNORMAL);
        //    await Task.Delay(1000); // 等待窗口激活

        //    _logger.Debug("IM窗口置顶成功");
        //    return windowHandle;
        //}
        private async Task<IntPtr> FindAndActivateImWindow()
        {
            _logger.Debug($"开始查找窗口: 【{_config.ImWindowTitle}】");

            IntPtr windowHandle = IntPtr.Zero;

            // 查找包含指定标题的窗口
            Process[] processes = Process.GetProcesses();
            foreach (Process process in processes)
            {
                try
                {
                    // 检查主窗口
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        StringBuilder windowTitle = new StringBuilder(256);
                        GetWindowText(process.MainWindowHandle, windowTitle, 256);

                        if (windowTitle.ToString().Contains(_config.ImWindowTitle))
                        {
                            windowHandle = process.MainWindowHandle;
                            _logger.Debug($"找到目标窗口: {windowTitle} (句柄: {windowHandle})");
                            break;
                        }
                    }

                    // 如果主窗口没找到，遍历该进程的所有窗口
                    if (windowHandle == IntPtr.Zero)
                    {
                        EnumThreadWindows(process.Id, (hWnd, lParam) =>
                        {
                            if (IsWindowVisible(hWnd))
                            {
                                StringBuilder title = new StringBuilder(256);
                                GetWindowText(hWnd, title, 256);

                                if (title.ToString().Contains(_config.ImWindowTitle))
                                {
                                    windowHandle = hWnd;
                                    _logger.Debug($"在进程 {process.ProcessName} 中找到窗口: {title}");
                                    return false;
                                }
                            }
                            return true;
                        }, IntPtr.Zero);

                        if (windowHandle != IntPtr.Zero) break;
                    }
                }
                catch (Exception ex)
                {
                    // 忽略无法访问的进程
                }
            }

            if (windowHandle == IntPtr.Zero)
            {
                _logger.Debug("未找到目标窗口");
                return IntPtr.Zero;
            }

            SetForegroundWindow(windowHandle);
            return windowHandle;
        }

        /// <summary>
        /// 查找并置顶弹窗
        /// </summary>
        /// <returns>窗口句柄，找不到返回IntPtr.Zero</returns>
        private async Task<IntPtr> FindAndActivatePopupWindow()
        {
            _logger.Debug($"开始查找弹窗: {_config.PopupWindowTitle}");

            int attempts = 0;
            const int maxAttempts = 10;

            while (attempts < maxAttempts)
            {
                Process[] processes = Process.GetProcesses();
                foreach (Process process in processes)
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        StringBuilder windowTitle = new StringBuilder(256);
                        GetWindowText(process.MainWindowHandle, windowTitle, 256);

                        if (windowTitle.ToString().Contains(_config.PopupWindowTitle))
                        {
                            IntPtr windowHandle = process.MainWindowHandle;
                            _logger.Debug($"找到弹窗: {windowTitle} (句柄: {windowHandle})");

                            // 置顶窗口
                            SetForegroundWindow(windowHandle);
                            ShowWindow(windowHandle, SW_SHOWNORMAL);
                            await Task.Delay(1000);

                            _logger.Debug("弹窗置顶成功");
                            return windowHandle;
                        }
                    }
                }

                attempts++;
                _logger.Debug($"未找到弹窗，重试 {attempts}/{maxAttempts}");
                await Task.Delay(2000); // 等待2秒后重试
            }

            _logger.Debug("查找弹窗超时");
            return IntPtr.Zero;
        }

        #endregion

        #region Python脚本调用

        /// <summary>
        /// 调用Python脚本进行图像匹配
        /// </summary>
        /// <param name="imagePath">要匹配的图片路径</param>
        /// <returns>匹配到的坐标，格式: "x,y"，失败返回null</returns>
        private async Task<string> CallPythonImageMatcher(string imagePath)
        {
            try
            {
                string portablePythonPath = @"D:\Software\Dev\python-3.13.5-embed-amd64\python.exe";
                string pythonExe = File.Exists(portablePythonPath) ? portablePythonPath : "py";

                _logger.Debug($"调用Python脚本匹配图片: {imagePath}, pythonPath:{pythonExe}");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{_config.PythonScriptPath}\" \"{imagePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();

                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        string coordinates = output.Trim();
                        _logger.Debug($"图片匹配成功，坐标: {coordinates}");
                        return coordinates;
                    }
                    else
                    {
                        _logger.Debug($"图片匹配失败. ExitCode: {process.ExitCode}, Error: {error}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"调用Python脚本时发生异常: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region 鼠标和键盘操作

        /// <summary>
        /// 模拟鼠标点击（增强版，支持多种点击方式）
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="windowHandle">目标窗口句柄（可选，用于后台点击）</param>
        /// <param name="clickMethod">点击方法</param>
        /// <returns>点击是否成功</returns>
        private async Task<bool> SimulateMouseClick(int x, int y, IntPtr windowHandle = default, ClickMethod clickMethod = ClickMethod.Auto)
        {
            try
            {
                _logger.Debug($"模拟鼠标点击: ({x}, {y}), 方法: {clickMethod}");

                bool success = false;

                switch (clickMethod)
                {
                    case ClickMethod.ForegroundOnly:
                        success = await PerformForegroundClick(x, y);
                        break;

                    case ClickMethod.BackgroundOnly:
                        success = await PerformBackgroundClick(x, y, windowHandle);
                        break;

                    case ClickMethod.SendInput:
                        success = await PerformSendInputClick(x, y);
                        break;

                    case ClickMethod.DoubleClick:
                        success = await PerformDoubleClick(x, y);
                        break;

                    case ClickMethod.Auto:
                    default:
                        // 自动模式：依次尝试不同方法直到成功
                        success = await TryMultipleClickMethods(x, y, windowHandle);
                        break;
                }

                if (success)
                {
                    _logger.Debug($"鼠标点击成功: ({x}, {y})");
                }
                else
                {
                    _logger.Debug($"鼠标点击失败: ({x}, {y})");
                }

                await Task.Delay(1000); // 等待点击响应
                return success;
            }
            catch (Exception ex)
            {
                _logger.Debug($"模拟鼠标点击时发生异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 尝试多种点击方法
        /// </summary>
        private async Task<bool> TryMultipleClickMethods(int x, int y, IntPtr windowHandle)
        {
            _logger.Debug("开始尝试多种点击方法");

            // 方法1: 前台点击
            if (await PerformForegroundClick(x, y))
            {
                _logger.Debug("前台点击成功");
                return true;
            }

            await Task.Delay(500);

            // 方法2: SendInput点击
            if (await PerformSendInputClick(x, y))
            {
                _logger.Debug("SendInput点击成功");
                return true;
            }

            await Task.Delay(500);

            // 方法3: 后台点击（如果有窗口句柄）
            if (windowHandle != IntPtr.Zero)
            {
                if (await PerformBackgroundClick(x, y, windowHandle))
                {
                    _logger.Debug("后台点击成功");
                    return true;
                }
            }

            await Task.Delay(500);

            // 方法4: 双击
            if (await PerformDoubleClick(x, y))
            {
                _logger.Debug("双击成功");
                return true;
            }

            _logger.Debug("所有点击方法都失败了");
            return false;
        }

        /// <summary>
        /// 前台点击
        /// </summary>
        private async Task<bool> PerformForegroundClick(int x, int y)
        {
            try
            {
                // 移动鼠标到目标位置
                SetCursorPos(x, y);
                await Task.Delay(200);

                // 执行点击
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                await Task.Delay(100);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                _logger.Debug("前台点击执行完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Debug($"前台点击异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 使用SendInput进行点击
        /// </summary>
        private async Task<bool> PerformSendInputClick(int x, int y)
        {
            try
            {
                // 移动鼠标
                SetCursorPos(x, y);
                await Task.Delay(200);

                // 使用SendInput发送鼠标事件
                INPUT[] inputs = new INPUT[2];

                // 鼠标按下
                inputs[0].type = INPUT_MOUSE;
                inputs[0].ui.mi.dx = 0;
                inputs[0].ui.mi.dy = 0;
                inputs[0].ui.mi.mouseData = 0;
                inputs[0].ui.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
                inputs[0].ui.mi.time = 0;
                inputs[0].ui.mi.dwExtraInfo = IntPtr.Zero;

                // 鼠标抬起
                inputs[1].type = INPUT_MOUSE;
                inputs[1].ui.mi.dx = 0;
                inputs[1].ui.mi.dy = 0;
                inputs[1].ui.mi.mouseData = 0;
                inputs[1].ui.mi.dwFlags = MOUSEEVENTF_LEFTUP;
                inputs[1].ui.mi.time = 0;
                inputs[1].ui.mi.dwExtraInfo = IntPtr.Zero;

                uint result = SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));

                _logger.Debug($"SendInput点击完成，发送事件数: {result}");
                return result == 2;
            }
            catch (Exception ex)
            {
                _logger.Debug($"SendInput点击异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取指定坐标处的子窗口
        /// </summary>
        /// <param name="hWndParent">父窗口句柄</param>
        /// <param name="point">坐标点（相对于父窗口的客户区）</param>
        /// <returns>子窗口句柄，如果没有子窗口则返回父窗口句柄</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr ChildWindowFromPoint(IntPtr hWndParent, POINT point);

        /// <summary>
        /// 获取控件ID
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>控件ID</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgCtrlID(IntPtr hWnd);

        /// <summary>
        /// 简单的后台点击（鼠标不移动）
        /// </summary>
        private async Task<bool> PerformBackgroundClick(int x, int y, IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    return false;

                _logger.Debug($"执行后台点击: ({x}, {y})");

                // 转换坐标（不移动鼠标）
                POINT point = new POINT { X = x, Y = y };
                ScreenToClient(windowHandle, ref point);

                // 发送消息（不移动实际鼠标）
                IntPtr lParam = (IntPtr)((point.Y << 16) | (point.X & 0xFFFF));

                PostMessage(windowHandle, WM_LBUTTONDOWN, IntPtr.Zero, lParam);
                await Task.Delay(50);
                PostMessage(windowHandle, WM_LBUTTONUP, IntPtr.Zero, lParam);

                _logger.Debug($"后台点击完成: ({point.X}, {point.Y})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Debug($"后台点击异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 双击
        /// </summary>
        private async Task<bool> PerformDoubleClick(int x, int y)
        {
            try
            {
                SetCursorPos(x, y);
                await Task.Delay(200);

                // 第一次点击
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                await Task.Delay(50);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                await Task.Delay(100);

                // 第二次点击
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                await Task.Delay(50);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                _logger.Debug("双击执行完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Debug($"双击异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 屏幕坐标转换为客户区坐标
        /// </summary>
        private (int X, int Y) ScreenToClient(IntPtr windowHandle, int screenX, int screenY)
        {
            try
            {
                POINT point = new POINT { X = screenX, Y = screenY };
                ScreenToClient(windowHandle, ref point);
                return (point.X, point.Y);
            }
            catch
            {
                return (screenX, screenY);
            }
        }

        /// <summary>
        /// 发送Ctrl+W组合键关闭当前标签页
        /// </summary>
        private async Task SendCtrlW()
        {
            try
            {
                _logger.Debug("发送Ctrl+W关闭标签页");

                INPUT[] inputs = new INPUT[4];

                // Ctrl按下
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].ui.ki.wVk = VK_CONTROL;
                inputs[0].ui.ki.dwFlags = 0;

                // W按下
                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].ui.ki.wVk = VK_W;
                inputs[1].ui.ki.dwFlags = 0;

                // W抬起
                inputs[2].type = INPUT_KEYBOARD;
                inputs[2].ui.ki.wVk = VK_W;
                inputs[2].ui.ki.dwFlags = KEYEVENTF_KEYUP;

                // Ctrl抬起
                inputs[3].type = INPUT_KEYBOARD;
                inputs[3].ui.ki.wVk = VK_CONTROL;
                inputs[3].ui.ki.dwFlags = KEYEVENTF_KEYUP;

                SendInput(4, inputs, Marshal.SizeOf(typeof(INPUT)));
                await Task.Delay(500);

                _logger.Debug("Ctrl+W发送完成");
            }
            catch (Exception ex)
            {
                _logger.Debug($"发送Ctrl+W时发生异常: {ex.Message}");
            }
        }

        #endregion

        #region 流程

        /// <summary>
        /// 执行完整的流程
        /// </summary>
        private async Task ExecuteCheckInProcess()
        {
            _logger.Debug("开始执行流程");

            int retryCount = 0;
            const int maxRetries = 5;
            bool success = false;
            CheckInStepResult lastResult = null;

            while (!success && retryCount < maxRetries)
            {
                try
                {
                    if (retryCount > 0)
                    {
                        _logger.Debug($"第 {retryCount} 次重试");
                        if (lastResult != null)
                        {
                            _logger.Debug($"上次失败原因:\n{lastResult.GetDetailedFailureInfo()}");
                        }
                        await Task.Delay(5000); // 重试前等待2秒
                    }

                    lastResult = await ExecuteCheckInSteps();
                    success = lastResult.IsSuccess;
                    
                    if (!success)
                    {
                        retryCount++;
                    }
                }
                catch (Exception ex)
                {
                    lastResult = CheckInStepResult.Failure(0, "执行流程时发生异常", ex);
                    _logger.Debug($"执行流程时发生异常: {ex.Message}");
                    retryCount++;
                    continue;
                }
            }

            if (!success)
            {
                _logger.Debug($"流程执行失败，已重试 {retryCount} 次");
                if (lastResult != null)
                {
                    _logger.Debug($"最终失败原因:\n{lastResult.GetDetailedFailureInfo()}");
                }
                return;
            }

            // 步骤5: 等待浏览器打开并从队列获取结果
            _logger.Debug("步骤5: 等待打卡结果队列中的结果");
            
            var result = await CheckInResultQueue.Instance.DequeueResultAsync(2); // 最多等待2分钟
            
            if (result != null)
            {
                if (result.IsSuccess)
                {
                    _logger.Debug($"流程成功完成: {result.Message}");
                    // 清空队列中的其他结果
                    CheckInResultQueue.Instance.Clear();
                    //await CloseCheckInTab();
                }
                else
                {
                    _logger.Debug($"流程完成，但打卡失败: {result.Message}");
                }
            }
            else
            {
                _logger.Debug("流程完成，但等待打卡结果超时（2分钟）");
            }
        }

        /// <summary>
        /// 执行打卡步骤1-4
        /// </summary>
        /// <returns>执行结果</returns>
        private async Task<CheckInStepResult> ExecuteCheckInSteps()
        {
            try
            {
                // 步骤1: 查找并置顶IM窗口
                IntPtr imWindow = await windowFinder.FindAndActivateImWindowEnhanced(DEFAULT_IM_WINDOW_TITLE);
                if (imWindow == IntPtr.Zero)
                {
                    return CheckInStepResult.Failure(1, "无法找到IM窗口");
                }

                // 步骤2: 匹配第一个图片
                string firstCoordinates = await CallPythonImageMatcher(_config.FirstImagePath);
                if (string.IsNullOrEmpty(firstCoordinates))
                {
                    return CheckInStepResult.Failure(2, "无法匹配第一个图片");
                }

                // 解析坐标并点击
                if (!TryParseCoordinates(firstCoordinates, out int x1, out int y1))
                {
                    return CheckInStepResult.Failure(2, $"无法解析第一个图片坐标: {firstCoordinates}");
                }

                if (!await SimulateMouseClick(x1, y1, imWindow, _config.DefaultClickMethod))
                {
                    return CheckInStepResult.Failure(2, "第一次点击失败");
                }

                // 步骤3: 等待并查找弹窗
                IntPtr popupWindow = await windowFinder.FindAndActivateImWindowEnhanced(DEFAULT_POPUP_WINDOW_TITLE);
                if (popupWindow == IntPtr.Zero)
                {
                    return CheckInStepResult.Failure(3, "无法找到弹窗");
                }

                // 步骤4: 匹配第二个图片
                string secondCoordinates = await CallPythonImageMatcher(_config.SecondImagePath);
                if (string.IsNullOrEmpty(secondCoordinates))
                {
                    return CheckInStepResult.Failure(4, "无法匹配第二个图片");
                }

                // 解析坐标并点击
                if (!TryParseCoordinates(secondCoordinates, out int x2, out int y2))
                {
                    return CheckInStepResult.Failure(4, $"无法解析第二个图片坐标: {secondCoordinates}");
                }

                if (!await SimulateMouseClick(x2, y2, popupWindow, _config.DefaultClickMethod))
                {
                    return CheckInStepResult.Failure(4, "第二次点击失败");
                }

                return CheckInStepResult.Success();
            }
            catch (Exception ex)
            {
                return CheckInStepResult.Failure(0, "执行过程中发生异常", ex);
            }
        }

        /// <summary>
        /// 解析坐标字符串
        /// </summary>
        private bool TryParseCoordinates(string coordinates, out int x, out int y)
        {
            x = y = 0;

            try
            {
                if (string.IsNullOrWhiteSpace(coordinates))
                {
                    _logger.Warn("坐标字符串为空");
                    return false;
                }

                _logger.Debug($"开始解析坐标: {coordinates}");
                string[] parts = coordinates.Split(',');
                
                if (parts.Length != 2)
                {
                    _logger.Warn($"坐标格式不正确，期望格式为'x,y'，实际值: {coordinates}");
                    return false;
                }

                string xStr = parts[0].Trim();
                string yStr = parts[1].Trim();

                _logger.Debug($"解析X坐标: {xStr}");
                _logger.Debug($"解析Y坐标: {yStr}");

                bool xSuccess = int.TryParse(xStr, out x);
                bool ySuccess = int.TryParse(yStr, out y);

                if (!xSuccess)
                    _logger.Warn($"无法解析X坐标: {xStr}");
                if (!ySuccess)
                    _logger.Warn($"无法解析Y坐标: {yStr}");

                return xSuccess && ySuccess;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"解析坐标时发生异常: {ex.Message}");
                _logger.Error($"异常堆栈: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    _logger.Error($"内部异常: {ex.InnerException.Message}");
                    _logger.Error($"内部异常堆栈: {ex.InnerException.StackTrace}");
                }
                return false;
            }
        }

        /// <summary>
        /// 检查是否成功
        /// </summary>
        /// <returns>是否检测到成功标识</returns>
        private async Task<bool> CheckCheckInSuccess()
        {
            try
            {
                _logger.Debug("检查结果");

                foreach (string browserName in _config.BrowserProcessNames)
                {
                    Process[] browsers = Process.GetProcessesByName(browserName);

                    foreach (Process browser in browsers)
                    {
                        if (browser.MainWindowHandle != IntPtr.Zero)
                        {
                            StringBuilder title = new StringBuilder(256);
                            GetWindowText(browser.MainWindowHandle, title, 256);

                            string windowTitle = title.ToString();

                            foreach (string keyword in _config.SuccessKeywords)
                            {
                                if (windowTitle.Contains(keyword))
                                {
                                    _logger.Debug($"检测到成功标识: {windowTitle} (关键字: {keyword})");
                                    return true;
                                }
                            }
                        }
                    }
                }

                _logger.Debug("未检测到成功标识");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Debug($"检查结果时发生异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 关闭页面Tab
        /// </summary>
        private async Task CloseCheckInTab()
        {
            try
            {
                _logger.Debug("关闭页面");

                // 发送Ctrl+W快捷键关闭当前Tab
                await SendCtrlW();

                _logger.Debug("页面已关闭");
            }
            catch (Exception ex)
            {
                _logger.Debug($"关闭页面时发生异常: {ex.Message}");
            }
        }

        #endregion

        #region 打卡记录管理

        /// <summary>
        /// 加载打卡记录
        /// </summary>
        private void LoadCheckInRecord()
        {
            try
            {
                if (File.Exists(_checkInRecordPath))
                {
                    string json = File.ReadAllText(_checkInRecordPath);
                    _checkInRecord = JsonSerializer.Deserialize<CheckInRecord>(json);
                    
                    // 检查是否是今天的记录
                    if (_checkInRecord.RecordDate.Date != DateTime.Now.Date)
                    {
                        _logger.Debug("检测到新的一天，重置打卡记录");
                        _checkInRecord = new CheckInRecord { RecordDate = DateTime.Now.Date };
                        SaveCheckInRecord();
                    }
                }
                else
                {
                    _checkInRecord = new CheckInRecord { RecordDate = DateTime.Now.Date };
                    SaveCheckInRecord();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "加载打卡记录时发生异常，创建新的记录");
                _checkInRecord = new CheckInRecord { RecordDate = DateTime.Now.Date };
                SaveCheckInRecord();
            }
        }

        /// <summary>
        /// 保存打卡记录
        /// </summary>
        private void SaveCheckInRecord()
        {
            try
            {
                string json = JsonSerializer.Serialize(_checkInRecord);
                File.WriteAllText(_checkInRecordPath, json);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "保存打卡记录时发生异常");
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 启动自动服务
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                _logger.Debug("自动服务已经在运行中");
                return;
            }

            if (!_config.IsEnabled)
            {
                _logger.Debug("自动功能已禁用");
                return;
            }

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            _logger.Debug("启动自动服务");

            Task.Run(async () =>
            {
                try
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        DateTime nextExecution = CalculateNextExecutionTime();
                        TimeSpan delay = nextExecution - DateTime.Now;

                        if (delay.TotalMilliseconds > 0)
                        {
                            _logger.Debug($"等待下次执行，剩余时间: {delay.TotalMinutes:F1} 分钟");
                            await Task.Delay(delay, _cancellationTokenSource.Token);
                        }

                        if (!_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            // 检查是否需要执行打卡
                            bool isMorningCheckIn = DateTime.Now.TimeOfDay < _config.MorningTime.Add(TimeSpan.FromMinutes(_config.RandomMinutes));
                            
                            if (!_checkInRecord.HasCheckedIn(isMorningCheckIn))
                            {
                                _logger.Debug($"执行{(isMorningCheckIn ? "早上" : "晚上")}打卡");
                                await ExecuteCheckInProcess();
                                _checkInRecord.RecordCheckIn(isMorningCheckIn);
                                SaveCheckInRecord(); // 保存打卡记录
                            }
                            else
                            {
                                _logger.Debug($"今日{(isMorningCheckIn ? "早上" : "晚上")}已打卡，跳过执行");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Debug("自动服务已取消");
                }
                catch (Exception ex)
                {
                    _logger.Debug($"自动服务异常: {ex.Message}");
                }
                finally
                {
                    _isRunning = false;
                }
            }, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// 停止自动服务
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                _logger.Debug("自动服务未在运行");
                return;
            }

            _logger.Debug("停止自动服务");
            _cancellationTokenSource?.Cancel();
            _isRunning = false;
        }

        /// <summary>
        /// 手动执行一次
        /// </summary>
        public async Task ExecuteManualCheckIn()
        {
            _logger.Debug("手动执行");
            await ExecuteCheckInProcess();
        }

        /// <summary>
        /// 测试图像匹配功能  "Resources/checkin_icon1.png"
        /// </summary>
        /// <param name="imagePath">要测试的图片路径</param>
        /// <returns>测试是否成功</returns>
        public async Task<bool> TestImageMatching(string imagePath)
        {
            try
            {
                _logger.Debug($"开始测试图像匹配: {imagePath}");

                if (!File.Exists(imagePath))
                {
                    _logger.Debug($"测试图片文件不存在: {imagePath}");
                    return false;
                }

                string coordinates = await CallPythonImageMatcher(imagePath);

                if (string.IsNullOrEmpty(coordinates))
                {
                    _logger.Debug("图像匹配测试失败：未找到匹配图像");
                    return false;
                }

                if (TryParseCoordinates(coordinates, out int x, out int y))
                {
                    _logger.Debug($"图像匹配测试成功：坐标 ({x}, {y})");
                    return true;
                }
                else
                {
                    _logger.Debug($"图像匹配测试失败：无法解析坐标 {coordinates}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"测试图像匹配时发生异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 测试指定坐标的点击效果
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="clickMethod">点击方法</param>
        /// <returns>测试是否成功</returns>
        public async Task<bool> TestClickCoordinates(int x, int y, ClickMethod clickMethod = ClickMethod.Auto)
        {
            try
            {
                _logger.Debug($"开始测试坐标点击: ({x}, {y}), 方法: {clickMethod}");

                bool success = await SimulateMouseClick(x, y, IntPtr.Zero, clickMethod);

                if (success)
                {
                    _logger.Debug("坐标点击测试成功");
                }
                else
                {
                    _logger.Debug("坐标点击测试失败");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Debug($"测试坐标点击时发生异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前配置信息的字符串表示
        /// </summary>
        /// <returns>配置信息字符串</returns>
        public string GetConfigurationInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 自动配置信息 ===");
            sb.AppendLine($"功能状态: {(_config.IsEnabled ? "已启用" : "已禁用")}");
            sb.AppendLine($"早上时间: {_config.MorningTime:hh\\:mm\\:ss}");
            sb.AppendLine($"晚上时间: {_config.EveningTime:hh\\:mm\\:ss}");
            sb.AppendLine($"随机时间范围: ±{_config.RandomMinutes} 分钟");
            sb.AppendLine($"默认点击方法: {_config.DefaultClickMethod}");
            sb.AppendLine($"运行状态: {(IsRunning ? "运行中" : "已停止")}");

            if (IsRunning)
            {
                var nextTime = CalculateNextExecutionTime();
                sb.AppendLine($"下次执行时间: {nextTime:yyyy-MM-dd HH:mm:ss}");
            }

            return sb.ToString();
        }


        #endregion
    }

}
