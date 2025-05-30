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
using Hardcodet.Wpf.TaskbarNotification;
using BrowserTool.Database;

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
                    MessageBox.Show("程序已经在运行中！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }

                // 初始化数据库
                DatabaseInitializer.Initialize();

                // 托盘图标初始化
                var trayIcon = (TaskbarIcon)Current.Resources["TrayIcon"];
                trayIcon.Icon = CreateDynamicIcon();
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
            base.OnExit(e);
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
