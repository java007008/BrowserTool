using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;
using Hardcodet.Wpf.TaskbarNotification;
using BrowserTool.Database;
using CefSharp;
using CefSharp.Wpf;
using System.IO.Pipes;
using System.Text;

namespace BrowserTool
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static readonly object _cefInitLock = new object();
        private MainWindow mainWindow;
        private static Mutex mutex = new Mutex(true, "BrowserTool_SingleInstance");
        private static bool mutexOwned = false; // 跟踪是否拥有Mutex
        private Thread pipeServerThread; // 命名管道服务器线程
        
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        private const int SW_RESTORE = 9;
        private const int HWND_BROADCAST = 0xFFFF;
        private static readonly int WM_SHOWMAINWINDOW_CUSTOM = RegisterWindowMessage("BrowserTool_ShowMainWindow");

        // Win32 API 导入
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 检查是否已经有实例在运行
                if (mutex.WaitOne(TimeSpan.Zero, false))
                {
                    mutexOwned = true;
                    
                    // 启动命名管道服务器，用于接收其他实例发送的URL
                    StartPipeServer();
                }
                else
                {
                    // 已有实例在运行，发送URL到主实例
                    if (e.Args != null && e.Args.Length > 0)
                    {
                        string url = e.Args[0];
                        if (!string.IsNullOrWhiteSpace(url) && 
                            (url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("file://")))
                        {
                            // 通过命名管道发送URL
                            SendUrlToPipe(url);
                        }
                    }
                    
                    // 向已存在的窗口发送消息
                    IntPtr hWnd = FindWindow(null, "Browser Tool");
                    if (hWnd != IntPtr.Zero)
                    {
                        PostMessage(hWnd, WM_SHOWMAINWINDOW_CUSTOM, IntPtr.Zero, IntPtr.Zero);
                    }
                    Shutdown();
                    return;
                }

                // 初始化CEF
                InitializeCef();

                // 初始化数据库
                DatabaseInitializer.Initialize();

                // 托盘图标初始化
                var trayIcon = (TaskbarIcon)Current.Resources["TrayIcon"];
                // 使用项目中的 .ico 文件作为托盘图标
                //string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                trayIcon.Icon = new Icon(Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico")).Stream);
                //if (File.Exists(iconPath))
                //{
                //    trayIcon.Icon = new System.Drawing.Icon(iconPath);
                //}
                //else
                //{
                //    // 如果找不到图标文件，则使用动态创建的图标作为备选
                //    trayIcon.Icon = CreateDynamicIcon();
                //}
                trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;

                // 创建主窗口
                mainWindow = new MainWindow();
                this.MainWindow = mainWindow;

                // 处理命令行参数（当作为默认浏览器启动时）
                if (e.Args != null && e.Args.Length > 0)
                {
                    string url = e.Args[0];
                    if (!string.IsNullOrWhiteSpace(url) && 
                        (url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("file://")))
                    {
                        // 在主窗口中打开URL
                        mainWindow.Dispatcher.Invoke(() => 
                        {
                            mainWindow.OpenUrlInTab("Loading...", url, false);
                           
                        });
                    }
                }

                // 监听主窗口关闭事件
                mainWindow.Closing += (s, args) =>
                {
                    args.Cancel = true;
                    mainWindow.Hide();
                };

                // 显示登录窗口
                var loginWindow = new LoginWindow();
                if (loginWindow.ShowDialog() == true)
                {
                    // 设置登录状态
                    Utils.LoginManager.SetLoggedIn();
                    
                    // 订阅登录状态改变事件，用于更新托盘菜单
                    Utils.LoginManager.OnLoginStatusChanged += UpdateTrayMenuState;
                    
                    // 初始化托盘菜单状态
                    UpdateTrayMenuState(true);
                    
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
                else
                {
                    Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"程序启动时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        // 托盘菜单事件
        private void TrayMenu_Settings_Click(object sender, RoutedEventArgs e)
        {
            // 检查登录状态
            if (Utils.LoginManager.IsLoggedIn)
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.Owner = mainWindow;
                settingsWindow.ShowDialog();
            }
            
           
        }

        private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e)
        {
            Shutdown();
        }

        private void TrayMenu_ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            // 检查登录状态
            if (Utils.LoginManager.IsLoggedIn)
            {
                var win = new ChangePasswordWindow();
                win.Owner = mainWindow;
                win.ShowDialog();
            }
           
        }

        private void TrayMenu_Show_Click(object sender, RoutedEventArgs e)
        {
            // 检查登录状态
            if (Utils.LoginManager.IsLoggedIn)
            {
                ShowMainWindow();
            }
            
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }

        public void ShowMainWindow()
        {
            if (mainWindow != null)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                mainWindow.Topmost = true;  // 确保窗口在最前面
                mainWindow.Topmost = false; // 然后取消置顶
            }
        }

        public static App GetCurrentApp()
        {
            return Current as App;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (mutexOwned)
            {
                mutex.ReleaseMutex();
            }
            
            try
            {
                // 清理浏览器实例管理器
                if (BrowserTool.Browser.BrowserInstanceManager.Instance != null)
                {
                    BrowserTool.Browser.BrowserInstanceManager.Instance.CleanupAllBrowsers();
                }
                
                // 清理图像缓存
                BrowserTool.Utils.ImageCache.ClearCache();
                
                // 关闭CEF
                if (Cef.IsInitialized == true)
                {
                    Cef.Shutdown();
                }
            }
            catch (Exception ex)
            {
                // 记录异常但不阻止应用程序退出
                System.Diagnostics.Debug.WriteLine($"应用程序退出清理异常: {ex.Message}");
            }
            
            // 停止命名管道服务器
            try
            {
                pipeServerThread?.Abort();
            }
            catch { }
            
            base.OnExit(e);
        }
        
        /// <summary>
        /// 初始化CEF浏览器引擎
        /// </summary>
        private void InitializeCef()
        {
            if (Cef.IsInitialized != true)
            {
                lock (_cefInitLock)
                {
                    if (Cef.IsInitialized != true)
                    {
                        try
                        {
                            var settings = new CefSettings();

                            // 设置缓存路径为exe所在目录下的CEF文件夹
                            string exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                            string cachePath = Path.Combine(exePath, "CEF");
                            if (!Directory.Exists(cachePath))
                            {
                                Directory.CreateDirectory(cachePath);
                            }
                            settings.CachePath = cachePath;

                            // 设置用户数据路径也在exe所在目录下
                            string userDataPath = Path.Combine(exePath, @"CEF\UserData");
                            if (!Directory.Exists(userDataPath))
                            {
                                Directory.CreateDirectory(userDataPath);
                            }
                            // UserDataPath不是CefSettings的属性，使用RootCachePath代替
                            // 在新版本中，用户数据会存储在缓存目录下的User Data文件夹中

                            // 设置日志文件路径
                            string logPath = Path.Combine(exePath, @"CEF\Log");
                            if (!Directory.Exists(logPath))
                            {
                                Directory.CreateDirectory(logPath);
                            }
                            settings.LogFile = Path.Combine(logPath, "cef.log");

                            // 性能优化设置

                            // 本地语言为中文
                            settings.Locale = "zh-CN";

                            // 自动保存用户数据（默认就在 CachePath 下）
                            settings.PersistSessionCookies = true;

                            // 启用硬件加速
                            settings.CefCommandLineArgs.Add("enable-gpu", "1");
                            settings.CefCommandLineArgs.Add("enable-gpu-compositing", "1");
                            settings.CefCommandLineArgs.Add("enable-gpu-rasterization", "1");

                            // 内存优化
                            settings.CefCommandLineArgs.Add("disable-gpu-shader-disk-cache", "1");
                            settings.CefCommandLineArgs.Add("renderer-process-limit", "1");
                            settings.CefCommandLineArgs.Add("disable-extensions", "1");

                            // 禁用自动更新检查
                            settings.CefCommandLineArgs.Add("disable-component-update", "1");

                            // 初始化CEF
                            Cef.Initialize(settings);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"CEF初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            Shutdown();
                        }
                    }
                }
            }
        }

        private Icon CreateDynamicIcon()
        {
            // 创建32x32蓝底白字B的图标
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(32, 32);
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.RoyalBlue);
                using (System.Drawing.Font font = new System.Drawing.Font("Arial", 18, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel))
                using (System.Drawing.Brush brush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                {
                    g.DrawString("B", font, brush, 6, 2);
                }
            }
            // 保存到内存流为icon
            using (var ms = new System.IO.MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                using (var iconBmp = new System.Drawing.Bitmap(ms))
                {
                    IntPtr hIcon = iconBmp.GetHicon();
                    System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(hIcon);
                    return icon;
                }
            }
        }
        
        private void UpdateTrayMenuState(bool isLoggedIn)
        {
            // 根据登录状态更新托盘菜单
            var trayIcon = (TaskbarIcon)Current.Resources["TrayIcon"];
            if (trayIcon?.ContextMenu != null)
            {
                foreach (var item in trayIcon.ContextMenu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        if (menuItem.Header?.ToString() == "设置")
                        {
                            menuItem.IsEnabled = isLoggedIn;
                            menuItem.ToolTip = isLoggedIn ? null : "请先登录后再使用设置功能";
                        }
                        else if (menuItem.Header?.ToString() == "修改密码")
                        {
                            menuItem.IsEnabled = isLoggedIn;
                            menuItem.ToolTip = isLoggedIn ? null : "请先登录后再修改密码";
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"[托盘菜单状态更新] 登录状态: {isLoggedIn}");
            }
        }
        
        /// <summary>
        /// 启动命名管道服务器，用于接收其他实例发送的URL
        /// </summary>
        private void StartPipeServer()
        {
            pipeServerThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var pipeServer = new NamedPipeServerStream("BrowserToolUrlPipe", PipeDirection.In))
                        {
                            pipeServer.WaitForConnection();
                            
                            using (var reader = new StreamReader(pipeServer, Encoding.UTF8))
                            {
                                string url = reader.ReadLine();
                                if (!string.IsNullOrWhiteSpace(url))
                                {
                                    // 在UI线程中打开URL
                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        if (mainWindow != null)
                                        {
                                            mainWindow.OpenUrlInTab("Loading...", url, false);
                                            ShowMainWindow();
                                        }
                                    }));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[命名管道服务器错误] {ex.Message}");
                    }
                }
            })
            {
                IsBackground = true
            };
            pipeServerThread.Start();
        }
        
        /// <summary>
        /// 通过命名管道发送URL到主实例
        /// </summary>
        private static void SendUrlToPipe(string url)
        {
            try
            {
                using (var pipeClient = new NamedPipeClientStream(".", "BrowserToolUrlPipe", PipeDirection.Out))
                {
                    pipeClient.Connect(1000); // 1秒超时
                    
                    using (var writer = new StreamWriter(pipeClient, Encoding.UTF8))
                    {
                        writer.WriteLine(url);
                        writer.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[发送URL到管道失败] {ex.Message}");
            }
        }
    }
}
