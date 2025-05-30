using System.Windows;
using System.Windows.Controls;
using CefSharp;
using CefSharp.Wpf;
using System.Windows.Media;

namespace BrowserTool.Browser
{
    /// <summary>
    /// Chrome风格TabItem，带关闭按钮
    /// </summary>
    public partial class CustomTabItem : UserControl
    {
        public CustomTabItem()
        {
            InitializeComponent();
        }

        // 关闭按钮点击事件，触发Tab关闭
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 向父级TabControl冒泡关闭事件
            var tabItem = this.FindParent<TabItem>();
            if (tabItem != null)
            {
                var tabControl = tabItem.FindParent<TabControl>();
                if (tabControl != null)
                {
                    tabControl.Items.Remove(tabItem);
                }
            }
        }

        // 复制标签页
        private void CopyTab_Click(object sender, RoutedEventArgs e)
        {
            var tabItem = this.FindParent<TabItem>();
            if (tabItem != null)
            {
                var tabControl = tabItem.FindParent<TabControl>();
                if (tabControl != null)
                {
                    // 获取当前标签页的浏览器控件
                    var browser = tabItem.Content as ChromiumWebBrowser;
                    if (browser != null)
                    {
                        // 获取当前URL
                        string currentUrl = browser.Address;
                        string currentTitle = browser.Title;

                        // 创建新标签页
                        var newTab = new TabItem();
                        var newBrowser = new ChromiumWebBrowser(currentUrl);

                        // 创建新的标题控件
                        var newHeader = new CustomTabItem();
                        newHeader.Content = GetNextTabTitle(currentTitle, tabControl);

                        newTab.Header = newHeader;
                        newTab.Content = newBrowser;

                        // 在当前标签页后插入新标签页
                        int currentIndex = tabControl.Items.IndexOf(tabItem);
                        tabControl.Items.Insert(currentIndex + 1, newTab);
                        tabControl.SelectedItem = newTab;
                    }
                }
            }
        }

        // 获取下一个标签页标题（自动添加数字）
        private string GetNextTabTitle(string baseTitle, TabControl tabControl)
        {
            string newTitle = baseTitle;
            int counter = 2;

            // 检查是否存在相同标题的标签页
            while (true)
            {
                bool exists = false;
                foreach (TabItem item in tabControl.Items)
                {
                    var header = item.Header as CustomTabItem;
                    if (header != null && header.Content.ToString() == newTitle)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    break;

                newTitle = $"{baseTitle} {counter}";
                counter++;
            }

            return newTitle;
        }
    }

    // 辅助方法：查找父级控件
    public static class VisualTreeHelperExtensions
    {
        public static T FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }
    }
} 