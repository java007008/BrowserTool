using CefSharp;
using System;
using System.Text;

namespace BrowserTool.Browser
{
    /// <summary>
    /// 负责向CefSharp浏览器注入深色主题样式的工具类
    /// </summary>
    public class DarkThemeStyleInjector
    {
        // 深色滚动条CSS样式
        private const string DarkScrollbarCss = @"
            ::-webkit-scrollbar {
                width: 12px;
                height: 12px;
                background-color: #2D2D30;
            }
            
            ::-webkit-scrollbar-track {
                background-color: #2D2D30;
                border-radius: 6px;
            }
            
            ::-webkit-scrollbar-thumb {
                background-color: #686868;
                border-radius: 6px;
            }
            
            ::-webkit-scrollbar-thumb:hover {
                background-color: #9E9E9E;
            }
            
            ::-webkit-scrollbar-thumb:active {
                background-color: #BFBFBF;
            }
            
            ::-webkit-scrollbar-corner {
                background-color: #2D2D30;
            }
        ";

        /// <summary>
        /// 将深色主题样式注入到浏览器中
        /// </summary>
        /// <param name="frame">要注入样式的浏览器框架</param>
        public static void InjectDarkThemeStyles(IFrame frame)
        {
            if (frame == null || !frame.IsValid)
                return;

            // 创建一个JavaScript，将CSS样式注入到页面头部
            string script = @"
                (function() {
                    var style = document.createElement('style');
                    style.type = 'text/css';
                    style.id = 'browser-tool-dark-theme';
                    style.innerHTML = `" + DarkScrollbarCss + @"`;
                    
                    // 如果已存在相同ID的样式，则先移除
                    var existingStyle = document.getElementById('browser-tool-dark-theme');
                    if (existingStyle) {
                        existingStyle.parentNode.removeChild(existingStyle);
                    }
                    
                    // 将样式添加到文档头部
                    (document.head || document.documentElement).appendChild(style);
                    
                    return true;
                })();
            ";

            // 异步执行JavaScript
            frame.ExecuteJavaScriptAsync(script);
        }
    }
}
