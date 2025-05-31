using CefSharp;
using CefSharp.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using BrowserTool;

namespace BrowserTool.Browser
{
    /// <summary>
    /// CefSharp右键菜单处理器，支持自定义菜单项
    /// </summary>
    public class CefMenuHandler : IContextMenuHandler
    {
        private const int CMD_VIEW_SOURCE = 26501;
        private const int CMD_COPY_URL = 26502;
        private const int CMD_DOWNLOAD_MANAGER = 26503;
        private const int CMD_TOGGLE_URL_BAR = 26504;
        private const int CMD_GO_TO_URL = 26505;
        private const int CMD_REFRESH = 26506;

        public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            // 清除默认菜单
            model.Clear();
            model.AddItem((CefMenuCommand)CMD_VIEW_SOURCE, "查看源代码");
            model.AddItem((CefMenuCommand)CMD_COPY_URL, "复制网址");
            model.AddSeparator();
            model.AddItem((CefMenuCommand)CMD_DOWNLOAD_MANAGER, "下载管理器");
            model.AddSeparator();
            model.AddItem((CefMenuCommand)CMD_TOGGLE_URL_BAR, "显示/隐藏地址栏");
            model.AddItem((CefMenuCommand)CMD_GO_TO_URL, "粘贴并访问");
            model.AddSeparator();
            model.AddItem((CefMenuCommand)CMD_REFRESH, "刷新");
        }

        public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            //粘贴并前往
            if ((int)commandId == CMD_GO_TO_URL)
            {
                string clipboardText = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(clipboardText) && (clipboardText.StartsWith("http://") || clipboardText.StartsWith("https://")))
                {
                    browserControl.Load(clipboardText);
                }
                return true;
            }
            if ((int)commandId == CMD_VIEW_SOURCE)
            {
                // 查看源代码
                frame.ViewSource();
                return true;
            }
            if ((int)commandId == CMD_COPY_URL)
            {
                // 复制当前URL
                Clipboard.SetText(frame.Url);
                return true;
            }
            if ((int)commandId == CMD_DOWNLOAD_MANAGER)
            {
                // 打开下载管理器窗口
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    BrowserTool.Browser.DownloadManagerWindow.ShowSingleton();
                });
                return true;
            }
            if ((int)commandId == CMD_TOGGLE_URL_BAR)
            {
                // 显示/隐藏地址栏
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.ToggleUrlBar();
                    }
                });
                return true;
            }
            if ((int)commandId == CMD_REFRESH)
            {
                // 刷新页面
                browser.Reload();
                return true;
            }
            return false;
        }

        public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame)
        {
        }

        public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            // 返回 false 使用默认菜单显示方式
            return false;
        }
    }
} 