using System.Windows.Controls;
using CefSharp;
using CefSharp.Wpf;

namespace BrowserTool.Browser
{
    /// <summary>
    /// 浏览器Tab管理器，负责Tab的添加、关闭等操作
    /// </summary>
    public class BrowserTabManager
    {
        private TabControl _tabControl;
        private MainWindow _mainWindow;

        public BrowserTabManager(TabControl tabControl, MainWindow mainWindow)
        {
            _tabControl = tabControl;
            _mainWindow = mainWindow;
        }

        /// <summary>
        /// 添加新Tab
        /// </summary>
        public TabItem AddTab(string header, object content)
        {
            var tabItem = new TabItem();
            var customHeader = new CustomTabItem();
            customHeader.Content = header;
            tabItem.Header = customHeader;
            tabItem.Content = content;

            // 设置浏览器事件处理
            var chromiumBrowser = content as ChromiumWebBrowser;
            if (chromiumBrowser != null)
            {
                var headerControl = tabItem.Header as CustomTabItem;
                chromiumBrowser.TitleChanged += (s, e) =>
                {
                    if (headerControl != null)
                    {
                        headerControl.Content = e.NewValue;
                    }
                };

                chromiumBrowser.AddressChanged += (s, e) =>
                {
                    if (tabItem == _tabControl.SelectedItem)
                    {
                        var contentPresenter = tabItem.Content as ContentPresenter;
                        if (contentPresenter != null)
                        {
                            var grid = contentPresenter.Content as Grid;
                            if (grid != null)
                            {
                                var urlTextBox = grid.FindName("UrlTextBox") as TextBox;
                                if (urlTextBox != null)
                                {
                                    urlTextBox.Text = chromiumBrowser.Address;
                                }
                            }
                        }
                    }
                };
            }

            _tabControl.Items.Add(tabItem);
            _tabControl.SelectedItem = tabItem;
            return tabItem;
        }

        /// <summary>
        /// 关闭指定Tab
        /// </summary>
        public void CloseTab(TabItem tabItem)
        {
            _tabControl.Items.Remove(tabItem);
        }
    }
} 