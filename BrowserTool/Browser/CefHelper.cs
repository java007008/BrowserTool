using CefSharp.Wpf;

namespace BrowserTool.Browser
{
    /// <summary>
    /// CefSharp浏览器辅助方法
    /// </summary>
    public static class CefHelper
    {
        /// <summary>
        /// 打开开发者工具（F12）
        /// </summary>
        public static void ShowDevTools(ChromiumWebBrowser browser)
        {
            if (browser != null && browser.GetBrowser() != null)
            {
                browser.GetBrowser().GetHost().ShowDevTools();
            }
        }
    }
} 