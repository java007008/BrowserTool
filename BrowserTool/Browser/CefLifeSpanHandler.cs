using CefSharp;
using CefSharp.Wpf;

namespace BrowserTool.Browser
{
    /// <summary>
    /// 拦截target=_blank等新窗口请求，在当前Tab打开
    /// </summary>
    public class CefLifeSpanHandler : ILifeSpanHandler
    {
        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) => false;
        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl,
            string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures,
            IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null;
            // 在当前Tab打开新链接
            chromiumWebBrowser.Load(targetUrl);
            return true; // 阻止新窗口
        }
    }
} 