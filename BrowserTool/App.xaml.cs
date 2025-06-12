using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CefSharp;
using Hardcodet.Wpf.TaskbarNotification;
using System.Runtime.InteropServices;
using BrowserTool.Database;
using BrowserTool.Utils;
using System.Windows.Controls;
using CefSharp.Wpf;
using FontStyle = System.Drawing.FontStyle;

namespace BrowserTool
{
    /// <summary>
    /// App.xaml 的交互逻辑，提供应用程序的生命周期管理和单例模式支持
    /// </summary>
    public partial class App : Application
    {
        #region 常量定义

        private const string MUTEX_NAME = "BrowserTool_SingleInstance";
        private const string PIPE_NAME = "BrowserToolUrlPipe";
        private const string WINDOW_MESSAGE_NAME = "BrowserTool_ShowMainWindow";

        #endregion

        #region Win32 API 导入

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region 字段和属性

        /// <summary>
        /// CEF初始化锁对象
        /// </summary>
        private static readonly object _cefInitLock = new();

        /// <summary>
        /// 主窗口实例
        /// </summary>
        private MainWindow _mainWindow;

        /// <summary>
        /// 单例应用程序互斥锁
        /// </summary>
        private static readonly Mutex _mutex = new(true, MUTEX_NAME);

        /// <summary>
        /// 是否拥有互斥锁
        /// </summary>
        private static bool _mutexOwned;

        /// <summary>
        /// 命名管道服务器线程
        /// </summary>
        private Thread _pipeServerThread;

        /// <summary>
        /// 命名管道服务器取消令牌源
        /// </summary>
        private CancellationTokenSource _pipeServerCancellation;

        /// <summary>
        /// 鼠标活动模拟器
        /// </summary>
        private static MouseActivitySimulator _mouseActivitySimulator;

        /// <summary>
        /// 自动签到模拟器
        /// </summary>
        private static AutoCheckInSimulator _checkInSimulator;

        /// <summary>
        /// 自定义窗口消息ID
        /// </summary>
        private static readonly int WM_SHOWMAINWINDOW_CUSTOM = RegisterWindowMessage(WINDOW_MESSAGE_NAME);

        /// <summary>
        /// 日志记录器
        /// </summary>
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        #endregion

        #region 托盘菜单事件处理

        /// <summary>
        /// 处理设置菜单点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void TrayMenu_Settings_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginManager.IsLoggedIn) return;

            try
            {
                var settingsWindow = new SettingsWindow
                {
                    Owner = _mainWindow
                };
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogError("打开设置窗口时发生错误", ex);
                ShowErrorMessage("无法打开设置窗口，请重试。");
            }
        }

        /// <summary>
        /// 处理退出菜单点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e) => Shutdown();

        /// <summary>
        /// 处理修改密码菜单点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void TrayMenu_ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (!Utils.LoginManager.IsLoggedIn) return;

            try
            {
                var changePasswordWindow = new ChangePasswordWindow
                {
                    Owner = _mainWindow
                };
                changePasswordWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogError("打开修改密码窗口时发生错误", ex);
                ShowErrorMessage("无法打开修改密码窗口，请重试。");
            }
        }

        /// <summary>
        /// 处理显示主窗口菜单点击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void TrayMenu_Show_Click(object sender, RoutedEventArgs e)
        {
            if (Utils.LoginManager.IsLoggedIn)
            {
                ShowMainWindow();
            }
        }

        /// <summary>
        /// 处理托盘图标双击事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => ShowMainWindow();

        #endregion

        #region 公共方法

        /// <summary>
        /// 显示主窗口并激活
        /// </summary>
        public void ShowMainWindow()
        {
            if (_mainWindow == null) return;

            try
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();

                // 确保窗口在最前面，然后取消置顶
                _mainWindow.Topmost = true;
                _mainWindow.Topmost = false;
            }
            catch (Exception ex)
            {
                LogError("显示主窗口时发生错误", ex);
            }
        }

        /// <summary>
        /// 获取当前应用程序实例
        /// </summary>
        /// <returns>当前应用程序实例</returns>
        public static App GetCurrentApp() => Current as App;

        /// <summary>
        /// 获取AutoCheckInSimulator实例
        /// </summary>
        /// <returns>AutoCheckInSimulator实例</returns>
        public static AutoCheckInSimulator GetAutoCheckInSimulator() => _checkInSimulator;

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化CEF浏览器引擎
        /// </summary>
        /// <exception cref="InvalidOperationException">CEF初始化失败时抛出</exception>
        private void InitializeCef()
        {
            if (Cef.IsInitialized == true) return;

            lock (_cefInitLock)
            {
                if (Cef.IsInitialized == true) return;

                try
                {
                    var settings = CreateCefSettings();
                    Cef.Initialize(settings);
                }
                catch (Exception ex)
                {
                    LogError("CEF初始化失败", ex);
                    ShowErrorMessage($"浏览器引擎初始化失败: {ex.Message}");
                    throw new InvalidOperationException("CEF初始化失败", ex);
                }
            }
        }

        /// <summary>
        /// 创建CEF设置配置
        /// </summary>
        /// <returns>配置好的CEF设置</returns>
        private static CefSettings CreateCefSettings()
        {
            var settings = new CefSettings();
            string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? throw new InvalidOperationException("无法获取应用程序路径");

            // 设置缓存路径
            string cachePath = Path.Combine(exePath, "CEF");
            Directory.CreateDirectory(cachePath);
            settings.CachePath = cachePath;

            // 设置日志文件路径
            string logPath = Path.Combine(exePath, @"CEF\Log");
            Directory.CreateDirectory(logPath);
            settings.LogFile = Path.Combine(logPath, "cef.log");

            // 基本设置
            settings.Locale = "zh-CN";
            settings.PersistSessionCookies = true;

            // 性能优化设置
            ConfigureCefPerformance(settings);

            return settings;
        }

        /// <summary>
        /// 配置CEF性能优化参数
        /// </summary>
        /// <param name="settings">CEF设置对象</param>
        private static void ConfigureCefPerformance(CefSettings settings)
        {
            var args = settings.CefCommandLineArgs;

            // 启用硬件加速
            args.Add("enable-gpu", "1");
            args.Add("enable-gpu-compositing", "1");
            args.Add("enable-gpu-rasterization", "1");

            // 内存优化
            args.Add("disable-gpu-shader-disk-cache", "1");
            args.Add("renderer-process-limit", "1");
            args.Add("disable-extensions", "1");
            args.Add("disable-component-update", "1");
        }

        /// <summary>
        /// 根据登录状态更新托盘菜单
        /// </summary>
        /// <param name="isLoggedIn">是否已登录</param>
        private void UpdateTrayMenuState(bool isLoggedIn)
        {
            try
            {
                if (Current.Resources["TrayIcon"] is not TaskbarIcon trayIcon ||
                    trayIcon.ContextMenu?.Items == null) return;

                foreach (var item in trayIcon.ContextMenu.Items)
                {
                    if (item is not MenuItem menuItem) continue;

                    switch (menuItem.Header?.ToString())
                    {
                        case "设置":
                            UpdateMenuItem(menuItem, isLoggedIn, "请先登录后再使用设置功能");
                            break;
                        case "修改密码":
                            UpdateMenuItem(menuItem, isLoggedIn, "请先登录后再修改密码");
                            break;
                    }
                }

                Debug.WriteLine($"[托盘菜单状态更新] 登录状态: {isLoggedIn}");
            }
            catch (Exception ex)
            {
                LogError("更新托盘菜单状态时发生错误", ex);
            }
        }

        /// <summary>
        /// 更新菜单项状态
        /// </summary>
        /// <param name="menuItem">菜单项</param>
        /// <param name="isEnabled">是否启用</param>
        /// <param name="disabledTooltip">禁用时的提示文本</param>
        private static void UpdateMenuItem(MenuItem menuItem, bool isEnabled, string disabledTooltip)
        {
            menuItem.IsEnabled = isEnabled;
            menuItem.ToolTip = isEnabled ? null : disabledTooltip;
        }

        /// <summary>
        /// 启动命名管道服务器，用于接收其他实例发送的URL
        /// </summary>
        private void StartPipeServer()
        {
            _pipeServerCancellation = new CancellationTokenSource();
            var cancellationToken = _pipeServerCancellation.Token;

            _pipeServerThread = new Thread(() => RunPipeServer(cancellationToken))
            {
                IsBackground = true,
                Name = "PipeServerThread"
            };
            _pipeServerThread.Start();
        }

        /// <summary>
        /// 运行命名管道服务器循环
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private void RunPipeServer(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var pipeServer = new NamedPipeServerStream(PIPE_NAME, PipeDirection.In);

                    // 使用异步等待连接，支持取消
                    var connectTask = Task.Run(() => pipeServer.WaitForConnection(), cancellationToken);
                    connectTask.Wait(cancellationToken);

                    using var reader = new StreamReader(pipeServer, Encoding.UTF8);
                    string url = reader.ReadLine();

                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        ProcessReceivedUrl(url);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，退出循环
                    break;
                }
                catch (Exception ex)
                {
                    LogError("命名管道服务器运行时发生错误", ex);

                    // 发生错误时短暂延迟后重试
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        /// <summary>
        /// 处理从命名管道接收到的URL
        /// </summary>
        /// <param name="url">接收到的URL</param>
        private void ProcessReceivedUrl(string url)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_mainWindow != null && Utils.LoginManager.IsLoggedIn)
                    {
                        _mainWindow.OpenUrlInTab("Loading...", url, false);
                        ShowMainWindow();
                    }
                }));
            }
            catch (Exception ex)
            {
                LogError("处理接收到的URL时发生错误", ex);
            }
        }

        /// <summary>
        /// 通过命名管道发送URL到主实例
        /// </summary>
        /// <param name="url">要发送的URL</param>
        /// <returns>是否发送成功</returns>
        private static bool SendUrlToPipe(string url)
        {
            try
            {
                using var pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out);
                pipeClient.Connect(1000); // 1秒超时

                using var writer = new StreamWriter(pipeClient, Encoding.UTF8);
                writer.WriteLine(url);
                writer.Flush();

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[发送URL到管道失败] {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 初始化托盘图标
        /// </summary>
        /// <returns>是否初始化成功</returns>
        private bool InitializeTrayIcon()
        {
            try
            {
                if (Current.Resources["TrayIcon"] is not TaskbarIcon trayIcon) return false;

                var iconStream = GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico"))?.Stream;
                if (iconStream != null)
                {
                    trayIcon.Icon = new Icon(iconStream);
                }

                trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;
                return true;
            }
            catch (Exception ex)
            {
                LogError("初始化托盘图标时发生错误", ex);
                return false;
            }
        }

        /// <summary>
        /// 处理命令行参数中的URL
        /// </summary>
        /// <param name="args">命令行参数</param>
        private void ProcessCommandLineUrl(string[] args)
        {
            if (args?.Length == 0) return;

            string url = args[0];
            if (string.IsNullOrWhiteSpace(url) || !IsValidUrl(url)) return;

            try
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    _mainWindow.OpenUrlInTab("Loading...", url, false);
                });
            }
            catch (Exception ex)
            {
                LogError("处理命令行URL时发生错误", ex);
            }
        }

        /// <summary>
        /// 验证URL格式是否有效
        /// </summary>
        /// <param name="url">要验证的URL</param>
        /// <returns>是否为有效URL</returns>
        private static bool IsValidUrl(string url) =>
            url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("file://", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 显示登录窗口并处理登录结果
        /// </summary>
        /// <returns>是否登录成功</returns>
        private bool ShowLoginWindow()
        {
            try
            {
                var loginWindow = new LoginWindow();
                if (loginWindow.ShowDialog() != true) return false;

                // 设置登录状态并订阅状态变化事件
                Utils.LoginManager.SetLoggedIn();
                Utils.LoginManager.OnLoginStatusChanged += UpdateTrayMenuState;

                // 初始化托盘菜单状态
                UpdateTrayMenuState(true);

                return true;
            }
            catch (Exception ex)
            {
                LogError("显示登录窗口时发生错误", ex);
                ShowErrorMessage("登录窗口初始化失败，程序即将退出。");
                return false;
            }
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="exception">异常对象</param>
        private static void LogError(string message, Exception exception)
        {
            _logger.Error($"[错误] {message}:", exception);
           
        }

        /// <summary>
        /// 显示错误消息框
        /// </summary>
        /// <param name="message">错误消息</param>
        private static void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion

        #region 应用程序生命周期

        /// <summary>
        /// 应用程序启动时的处理逻辑
        /// </summary>
        /// <param name="e">启动事件参数</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 初始化模拟器
                InitializeSimulators();

                // 检查单例应用程序
                if (!CheckSingleInstance(e)) return;

                // 初始化应用程序核心组件
                if (!InitializeApplication(e)) return;

                // 显示登录窗口并处理结果
                if (ShowLoginWindow())
                {
                    ShowMainWindowAndActivate();
                }
                else
                {
                    Shutdown();
                }
            }
            catch (Exception ex)
            {
                LogError("程序启动时发生未处理的错误", ex);
                ShowErrorMessage($"程序启动失败：{ex.Message}");
                Shutdown();
            }
        }

        /// <summary>
        /// 初始化模拟器组件
        /// </summary>
        private static void InitializeSimulators()
        {
            _checkInSimulator = new AutoCheckInSimulator();
            _checkInSimulator.Start();

            _mouseActivitySimulator = new MouseActivitySimulator();
            _mouseActivitySimulator.Start();
        }

        /// <summary>
        /// 检查单例应用程序实例
        /// </summary>
        /// <param name="e">启动参数</param>
        /// <returns>是否应该继续启动</returns>
        private bool CheckSingleInstance(StartupEventArgs e)
        {
            if (_mutex.WaitOne(TimeSpan.Zero, false))
            {
                _mutexOwned = true;
                StartPipeServer();
                return true;
            }

            // 已有实例在运行，发送URL到主实例
            HandleExistingInstance(e.Args);
            return false;
        }

        /// <summary>
        /// 处理已存在实例的情况
        /// </summary>
        /// <param name="args">命令行参数</param>
        private void HandleExistingInstance(string[] args)
        {
            // 发送URL到主实例
            if (args?.Length > 0)
            {
                string url = args[0];
                if (IsValidUrl(url))
                {
                    SendUrlToPipe(url);
                }
            }

            // 激活已存在的窗口
            IntPtr hWnd = FindWindow(null, "Browser Tool");
            if (hWnd != IntPtr.Zero)
            {
                PostMessage(hWnd, WM_SHOWMAINWINDOW_CUSTOM, IntPtr.Zero, IntPtr.Zero);
            }

            Shutdown();
        }

        /// <summary>
        /// 初始化应用程序核心组件
        /// </summary>
        /// <param name="e">启动参数</param>
        /// <returns>是否初始化成功</returns>
        private bool InitializeApplication(StartupEventArgs e)
        {
            try
            {
                // 初始化CEF
                InitializeCef();

                // 初始化数据库
                //DatabaseInitializer.Initialize();

                // 初始化托盘图标
                if (!InitializeTrayIcon()) return false;

                // 创建主窗口
                CreateMainWindow(e.Args);

                return true;
            }
            catch (Exception ex)
            {
                LogError("初始化应用程序组件时发生错误", ex);
                return false;
            }
        }

        /// <summary>
        /// 创建并配置主窗口
        /// </summary>
        /// <param name="args">命令行参数</param>
        private void CreateMainWindow(string[] args)
        {
            _mainWindow = new MainWindow();
            MainWindow = _mainWindow;

            // 处理命令行参数
            ProcessCommandLineUrl(args);

            // 监听主窗口关闭事件
            _mainWindow.Closing += (_, e) =>
            {
                e.Cancel = true;
                _mainWindow.Hide();
            };
        }

        /// <summary>
        /// 显示并激活主窗口
        /// </summary>
        private void ShowMainWindowAndActivate()
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        /// <summary>
        /// 应用程序退出时的清理逻辑
        /// </summary>
        /// <param name="e">退出事件参数</param>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                CleanupResources();
            }
            catch (Exception ex)
            {
                LogError("应用程序退出清理时发生错误", ex);
            }
            finally
            {
                base.OnExit(e);
            }
        }

        /// <summary>
        /// 清理应用程序资源
        /// </summary>
        private void CleanupResources()
        {
            // 释放互斥锁
            if (_mutexOwned)
            {
                _mutex.ReleaseMutex();
                _mutexOwned = false;
            }

            // 停止模拟器
            _checkInSimulator?.Stop();
            _mouseActivitySimulator?.Stop();

            // 停止命名管道服务器
            _pipeServerCancellation?.Cancel();
            _pipeServerThread?.Join(TimeSpan.FromSeconds(2));

            // 清理浏览器相关资源
            CleanupBrowserResources();
        }

        /// <summary>
        /// 清理浏览器相关资源
        /// </summary>
        private static void CleanupBrowserResources()
        {
            try
            {
                Browser.BrowserInstanceManager.Instance?.CleanupAllBrowsers();
                ImageCache.ClearCache();

                if (Cef.IsInitialized == true)
                {
                    Cef.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清理浏览器资源时发生错误: {ex.Message}");
            }
        }

        #endregion
    }
}