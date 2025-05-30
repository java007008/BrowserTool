using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace BrowserTool.Utils
{
    public static class WebAutomation
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public static async Task<bool> AutoLogin(string url, string username, string password, bool useCommonCredentials, string commonUsername, string commonPassword)
        {
            try
            {
                // 启动默认浏览器
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });

                // 等待页面加载
                await Task.Delay(3000);

                // 获取当前活动窗口
                var activeWindow = GetForegroundWindow();
                if (activeWindow == IntPtr.Zero) return false;

                // 根据是否使用公共账号选择用户名和密码
                var loginUsername = useCommonCredentials ? commonUsername : username;
                var loginPassword = useCommonCredentials ? commonPassword : password;

                // 模拟键盘输入
                if (!string.IsNullOrEmpty(loginUsername))
                {
                    SendKeys.SendWait(loginUsername);
                    SendKeys.SendWait("{TAB}");
                }

                if (!string.IsNullOrEmpty(loginPassword))
                {
                    SendKeys.SendWait(loginPassword);
                    SendKeys.SendWait("{ENTER}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"自动登录失败：{ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
} 