using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace BrowserTool.Utils
{
    /// <summary>
    /// 默认浏览器管理器
    /// </summary>
    public static class DefaultBrowserManager
    {
        private const string BROWSER_NAME = "BrowserTool";
        private const string BROWSER_DESCRIPTION = "Browser Tool Application";

        /// <summary>
        /// 检查是否为默认浏览器
        /// </summary>
        public static bool IsDefaultBrowser()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice"))
                {
                    if (key != null)
                    {
                        var progId = key.GetValue("ProgId")?.ToString();
                        return progId == BROWSER_NAME;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[检查默认浏览器失败] {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 设置为默认浏览器
        /// </summary>
        public static bool SetAsDefaultBrowser()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                
                // 注册应用程序
                RegisterApplication(exePath);
                
                // 在Windows 10及以上版本，需要打开系统设置让用户手动选择
                if (Environment.OSVersion.Version.Major >= 10)
                {
                    var result = MessageBox.Show(
                        "由于Windows安全策略，需要在系统设置中手动设置默认浏览器。\n\n点击\"确定\"将打开系统设置页面，请在\"默认应用\"中选择BrowserTool作为默认Web浏览器。",
                        "设置默认浏览器",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.OK)
                    {
                        // 打开Windows设置的默认应用页面
                        Process.Start("ms-settings:defaultapps");
                        return true;
                    }
                    return false;
                }
                else
                {
                    // Windows 8.1及以下版本可以直接设置
                    SetUrlAssociation("http");
                    SetUrlAssociation("https");
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置默认浏览器失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 注册应用程序
        /// </summary>
        private static void RegisterApplication(string exePath)
        {
            // 注册程序信息
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{BROWSER_NAME}"))
            {
                key.SetValue("", BROWSER_DESCRIPTION);
                key.SetValue("FriendlyTypeName", BROWSER_DESCRIPTION);
            }

            // 注册默认图标
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{BROWSER_NAME}\DefaultIcon"))
            {
                key.SetValue("", $"\"{exePath}\",0");
            }

            // 注册打开命令
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{BROWSER_NAME}\shell\open\command"))
            {
                key.SetValue("", $"\"{exePath}\" \"%1\"");
            }

            // 注册能力
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{BROWSER_NAME}\Capabilities"))
            {
                key.SetValue("ApplicationName", BROWSER_NAME);
                key.SetValue("ApplicationDescription", BROWSER_DESCRIPTION);
            }

            // 注册URL关联
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{BROWSER_NAME}\Capabilities\URLAssociations"))
            {
                key.SetValue("http", BROWSER_NAME);
                key.SetValue("https", BROWSER_NAME);
            }

            // 注册到已注册应用程序列表
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            {
                key.SetValue(BROWSER_NAME, $@"Software\Classes\{BROWSER_NAME}\Capabilities");
            }
        }

        /// <summary>
        /// 设置URL关联
        /// </summary>
        private static void SetUrlAssociation(string protocol)
        {
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\{protocol}\UserChoice"))
            {
                key.SetValue("ProgId", BROWSER_NAME);
            }
        }

        /// <summary>
        /// 移除默认浏览器设置
        /// </summary>
        public static void RemoveDefaultBrowser()
        {
            try
            {
                // 删除注册信息
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{BROWSER_NAME}", false);
                
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
                {
                    key?.DeleteValue(BROWSER_NAME, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[移除默认浏览器设置失败] {ex.Message}");
            }
        }
    }
}
