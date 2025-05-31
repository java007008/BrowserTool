using System;
using System.Collections.Generic;
using System.Linq;
using CefSharp;
using CefSharp.Wpf;

namespace BrowserTool.Browser
{
    /// <summary>
    /// 浏览器实例管理器，用于管理和重用浏览器实例
    /// </summary>
    public class BrowserInstanceManager
    {
        private static BrowserInstanceManager _instance;
        private readonly List<BrowserInstance> _availableBrowsers = new List<BrowserInstance>();
        private readonly Dictionary<string, BrowserInstance> _activeBrowsers = new Dictionary<string, BrowserInstance>();
        
        // 单例模式
        public static BrowserInstanceManager Instance => _instance ??= new BrowserInstanceManager();
        
        // 最大缓存浏览器实例数量
        private const int MAX_CACHED_BROWSERS = 3;
        
        private BrowserInstanceManager()
        {
            // 私有构造函数，确保单例
        }
        
        /// <summary>
        /// 获取一个浏览器实例，如果有可用的缓存实例则重用，否则创建新实例
        /// </summary>
        /// <param name="url">要加载的URL</param>
        /// <param name="tabId">标签页ID</param>
        /// <returns>浏览器实例</returns>
        public ChromiumWebBrowser GetBrowser(string url, string tabId)
        {
            lock (_availableBrowsers)
            {
                BrowserInstance instance;
                
                // 检查是否有可用的浏览器实例
                if (_availableBrowsers.Count > 0)
                {
                    instance = _availableBrowsers[0];
                    _availableBrowsers.RemoveAt(0);
                }
                else
                {
                    // 创建新的浏览器实例
                    var browser = new ChromiumWebBrowser();
                    
                    // 设置通用处理程序
                    browser.DownloadHandler = new CefDownloadHandler();
                    browser.MenuHandler = new CefMenuHandler();
                    browser.LifeSpanHandler = new CefLifeSpanHandler();
                    
                    instance = new BrowserInstance { Browser = browser };
                }
                
                // 将实例标记为活动状态
                _activeBrowsers[tabId] = instance;
                
                // 加载URL
                if (!string.IsNullOrEmpty(url))
                {
                    instance.Browser.LoadUrl(url);
                }
                
                return instance.Browser;
            }
        }
        
        /// <summary>
        /// 释放浏览器实例，将其放回池中或销毁
        /// </summary>
        /// <param name="tabId">标签页ID</param>
        /// <param name="dispose">是否完全销毁实例</param>
        public void ReleaseBrowser(string tabId, bool dispose = false)
        {
            lock (_availableBrowsers)
            {
                if (_activeBrowsers.TryGetValue(tabId, out BrowserInstance instance))
                {
                    _activeBrowsers.Remove(tabId);
                    
                    if (dispose)
                    {
                        // 完全销毁实例
                        instance.Browser.Dispose();
                    }
                    else
                    {
                        // 清理浏览器状态
                        try
                        {
                            instance.Browser.LoadUrl("about:blank");
                            
                            // 如果缓存未满，则添加到可用列表
                            if (_availableBrowsers.Count < MAX_CACHED_BROWSERS)
                            {
                                _availableBrowsers.Add(instance);
                            }
                            else
                            {
                                // 缓存已满，销毁实例
                                instance.Browser.Dispose();
                            }
                        }
                        catch
                        {
                            // 如果清理失败，直接销毁
                            instance.Browser.Dispose();
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 清理所有浏览器实例
        /// </summary>
        public void CleanupAllBrowsers()
        {
            lock (_availableBrowsers)
            {
                // 清理所有活动的浏览器实例
                foreach (var instance in _activeBrowsers.Values)
                {
                    try
                    {
                        instance.Browser.Dispose();
                    }
                    catch { }
                }
                _activeBrowsers.Clear();
                
                // 清理所有可用的浏览器实例
                foreach (var instance in _availableBrowsers)
                {
                    try
                    {
                        instance.Browser.Dispose();
                    }
                    catch { }
                }
                _availableBrowsers.Clear();
            }
        }
    }
    
    /// <summary>
    /// 浏览器实例包装类
    /// </summary>
    public class BrowserInstance
    {
        public ChromiumWebBrowser Browser { get; set; }
        public DateTime LastUsed { get; set; } = DateTime.Now;
    }
}
