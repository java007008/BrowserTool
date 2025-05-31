using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.IO;
using System.Threading;
using System.Reflection;
using Hardcodet.Wpf.TaskbarNotification;
using BrowserTool.Database;
using CefSharp;
using CefSharp.Wpf;

namespace BrowserTool
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private MainWindow mainWindow;
        private static Mutex mutex = new Mutex(true, "BrowserTool_SingleInstance");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 检查是否已经有实例在运行
                if (!mutex.WaitOne(TimeSpan.Zero, true))
                {
                    // 查找已运行进程并激活主窗口
                    var current = System.Diagnostics.Process.GetCurrentProcess();
                    var processes = System.Diagnostics.Process.GetProcessesByName(current.ProcessName);
                    foreach (var process in processes)
                    {
                        if (process.Id != current.Id)
                        {
                            IntPtr hWnd = process.MainWindowHandle;
                            if (hWnd != IntPtr.Zero)
                            {
                                if (IsIconic(hWnd))
                                    ShowWindow(hWnd, SW_RESTORE);
                                SetForegroundWindow(hWnd);
                            }
                            break;
                        }
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
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (File.Exists(iconPath))
                {
                    trayIcon.Icon = new System.Drawing.Icon(iconPath);
                }
                else
                {
                    // 如果找不到图标文件，则使用动态创建的图标作为备选
                    trayIcon.Icon = CreateDynamicIcon();
                }
                trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;

                // 创建并显示主窗口
                mainWindow = new MainWindow();
                this.MainWindow = mainWindow;

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
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = mainWindow;
            settingsWindow.ShowDialog();
        }

        private void TrayMenu_Exit_Click(object sender, RoutedEventArgs e)
        {
            Shutdown();
        }

        private void TrayMenu_ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var win = new ChangePasswordWindow();
            win.Owner = mainWindow;
            win.ShowDialog();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            mutex.ReleaseMutex();
            
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
            
            base.OnExit(e);
        }
        
        /// <summary>
        /// 初始化CEF浏览器引擎
        /// </summary>
        private void InitializeCef()
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
                    settings.PersistSessionCookies = true;
                    // 用户偏好会自动保存到缓存目录
                    
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

        // Win32 API 导入
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

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

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (mainWindow != null)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            }
        }
    }
}
