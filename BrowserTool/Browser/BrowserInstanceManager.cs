using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;

namespace BrowserTool.Browser
{
    /// <summary>
    /// 浏览器实例管理器，负责创建和管理浏览器实例
    /// 采用无缓存策略，确保每个标签页都是全新、干净的环境
    /// </summary>
    public class BrowserInstanceManager
    {
        private static BrowserInstanceManager _instance;
        private readonly Dictionary<string, ChromiumWebBrowser> _activeBrowsers = new Dictionary<string, ChromiumWebBrowser>();
        
        // 单例模式
        public static BrowserInstanceManager Instance => _instance ??= new BrowserInstanceManager();
        
        private BrowserInstanceManager()
        {
            // 私有构造函数，确保单例
        }
        
        /// <summary>
        /// 创建一个新的浏览器实例
        /// </summary>
        /// <param name="url">要加载的URL（可为空，由调用方控制加载时机）</param>
        /// <param name="tabId">标签页ID</param>
        /// <returns>浏览器实例</returns>
        public ChromiumWebBrowser GetBrowser(string url, string tabId)
        {
            lock (_activeBrowsers)
            {
                // 如果该标签页已有浏览器实例，先清理
                if (_activeBrowsers.ContainsKey(tabId))
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 标签页已存在浏览器实例，先清理 - TabId: {tabId}");
                    ReleaseBrowser(tabId, dispose: true);
                }
                
                // 总是创建全新的浏览器实例
                var browser = new ChromiumWebBrowser();
                
                // 设置通用处理程序
                browser.DownloadHandler = new CefDownloadHandler();
                browser.MenuHandler = new CefMenuHandler();
                browser.LifeSpanHandler = new CefLifeSpanHandler();
                
                // 添加页面加载完成事件，注入深色滚动条样式
                browser.FrameLoadEnd += (sender, args) => {
                    if (args.Frame.IsMain)
                    {
                        // 注入深色滚动条样式
                        DarkThemeStyleInjector.InjectDarkThemeStyles(args.Frame);
                    }
                };
                
                // 注册到活动浏览器列表
                _activeBrowsers[tabId] = browser;
                
                System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 创建新浏览器实例 - TabId: {tabId}");
                
                // 只有在URL不为空且浏览器已初始化时才加载URL
                if (!string.IsNullOrEmpty(url) && browser.IsBrowserInitialized)
                {
                    browser.LoadUrl(url);
                    System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 直接加载URL: {url}");
                }
                else if (!string.IsNullOrEmpty(url))
                {
                    // 如果浏览器未初始化，等待初始化完成后加载
                    DependencyPropertyChangedEventHandler browserInitializedHandler = null;
                    browserInitializedHandler = (sender, e) =>
                    {
                        try
                        {
                            if (browser.IsBrowserInitialized)
                            {
                                browser.IsBrowserInitializedChanged -= browserInitializedHandler;
                                browser.LoadUrl(url);
                                System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 浏览器初始化完成后加载URL: {url}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 延迟加载URL时发生异常: {ex.Message}");
                        }
                    };
                    browser.IsBrowserInitializedChanged += browserInitializedHandler;
                }
                
                return browser;
            }
        }
        
        /// <summary>
        /// 释放并销毁浏览器实例
        /// </summary>
        /// <param name="tabId">标签页ID</param>
        /// <param name="dispose">是否销毁实例（默认为true，确保完全清理）</param>
        public void ReleaseBrowser(string tabId, bool dispose = true)
        {
            if (string.IsNullOrEmpty(tabId))
            {
                System.Diagnostics.Debug.WriteLine("[BrowserInstanceManager] ReleaseBrowser: tabId为空");
                return;
            }

            lock (_activeBrowsers)
            {
                if (_activeBrowsers.TryGetValue(tabId, out ChromiumWebBrowser browser))
                {
                    _activeBrowsers.Remove(tabId);
                    System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 释放浏览器实例 - TabId: {tabId}");
                    
                    // 总是销毁实例，确保完全清理
                    DisposeBrowserInstance(browser, tabId);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 未找到要释放的浏览器实例 - TabId: {tabId}");
                }
            }
        }

        /// <summary>
        /// 安全地销毁浏览器实例
        /// </summary>
        /// <param name="browser">浏览器实例</param>
        /// <param name="tabId">标签页ID</param>
        private void DisposeBrowserInstance(ChromiumWebBrowser browser, string tabId)
        {
            try
            {
                if (browser != null && !browser.IsDisposed)
                {
                    // 清理事件处理器
                    CleanupBrowserEventHandlers(browser);
                    
                    // 销毁浏览器实例
                    browser.Dispose();
                    System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 浏览器实例已销毁 - TabId: {tabId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 销毁浏览器实例时发生异常 - TabId: {tabId}, Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理浏览器事件处理器
        /// </summary>
        /// <param name="browser">浏览器实例</param>
        private void CleanupBrowserEventHandlers(ChromiumWebBrowser browser)
        {
            try
            {
                if (browser?.Tag is Dictionary<string, object> browserTags)
                {
                    // 清理存储在Tag中的事件处理器引用
                    if (browserTags.TryGetValue("titleUpdateHandler", out object titleHandler) && 
                        titleHandler is EventHandler<FrameLoadEndEventArgs> titleUpdateHandler)
                    {
                        browser.FrameLoadEnd -= titleUpdateHandler;
                    }
                    
                    if (browserTags.TryGetValue("loadingStateChangedHandler", out object loadingHandler) && 
                        loadingHandler is EventHandler<LoadingStateChangedEventArgs> loadingStateChangedHandler)
                    {
                        browser.LoadingStateChanged -= loadingStateChangedHandler;
                    }
                    
                    browserTags.Clear();
                    browser.Tag = null;
                    
                    System.Diagnostics.Debug.WriteLine("[BrowserInstanceManager] 浏览器事件处理器已清理");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 清理事件处理器时发生异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清理所有浏览器实例
        /// </summary>
        public void CleanupAllBrowsers()
        {
            lock (_activeBrowsers)
            {
                // 清理所有活动的浏览器实例
                foreach (var kvp in _activeBrowsers.ToList())
                {
                    try
                    {
                        DisposeBrowserInstance(kvp.Value, kvp.Key);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BrowserInstanceManager] 清理浏览器实例时发生异常 - TabId: {kvp.Key}, Error: {ex.Message}");
                    }
                }
                _activeBrowsers.Clear();
                
                System.Diagnostics.Debug.WriteLine("[BrowserInstanceManager] 所有浏览器实例已清理");
            }
        }
    }
}
