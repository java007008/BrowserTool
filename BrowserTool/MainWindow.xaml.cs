using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Security.Policy;
using CefSharp;
using CefSharp.Wpf;
using BrowserTool.Browser;
using BrowserTool.Database;
using BrowserTool.Database.Entities;
using BrowserTool.Utils;
using Hardcodet.Wpf.TaskbarNotification;

namespace BrowserTool
{
    /// <summary>
    /// 标签页信息类，用于存储标签页的URL和ID
    /// </summary>
    public class TabInfo
    {
        public string Url { get; set; }
        public string TabId { get; set; }
    }
    
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 菜单数据结构
        public class MenuGroup
        {
            public string GroupName { get; set; }
            public bool IsDefaultExpanded { get; set; }
            public List<MenuItemData> Items { get; set; }
        }
        public class MenuItemData
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string Icon { get; set; }
        }

        // 初始化菜单数据
        private List<MenuGroup> menuGroups = new List<MenuGroup>();

        private bool isDrawerOpen = true;
        //private BrowserTabManager _tabManager;

        // 添加公共属性以访问抽屉状态
        public bool IsDrawerOpen => isDrawerOpen;

        private WindowState _lastWindowState = WindowState.Normal;
        private Rect _restoreBounds;

        // 鼠标活动模拟器实例
        private MouseActivitySimulator _mouseActivitySimulator;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // 监听标签页切换
                MainTabControl.SelectionChanged += MainTabControl_SelectionChanged; 

                // 窗口居中显示
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // 订阅数据变更事件
                SiteConfig.DataChanged += OnDataChanged;

                // 添加Windows消息处理
                this.SourceInitialized += MainWindow_SourceInitialized;

                // 订阅登录状态变化事件
                LoginManager.OnLoginStatusChanged += OnLoginStatusChanged;
            }
            catch (Exception ex)
            {
                Application.Current.Shutdown();
            }
        }

        // Windows消息常量
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);
        
        private static readonly int WM_SHOWMAINWINDOW = RegisterWindowMessage("BrowserTool_ShowMainWindow");

        // 窗口消息处理
        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SHOWMAINWINDOW)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] 收到WM_SHOWMAINWINDOW消息: {msg}");
                // 收到显示主窗口的消息，调用App的ShowMainWindow方法
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var app = App.GetCurrentApp();
                    if (app != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[MainWindow] 调用App.ShowMainWindow");
                        app.ShowMainWindow();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[MainWindow] App实例为null");
                    }
                }));
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnDataChanged(object sender, EventArgs e)
        {
            // 在UI线程上更新菜单
            Dispatcher.Invoke(() =>
            {
                LoadMenuGroupsFromDb();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            // 取消订阅事件
            SiteConfig.DataChanged -= OnDataChanged;
        }

        // 初始化左侧菜单树，支持可选过滤
        private void InitMenuTree(string filter = "")
        {
            MenuTree.Items.Clear();
            foreach (var group in menuGroups)
            {
                // 过滤逻辑
                var filteredItems = string.IsNullOrWhiteSpace(filter)
                    ? group.Items
                    : group.Items.Where(i => i.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (filteredItems.Count == 0 && !string.IsNullOrWhiteSpace(filter))
                    continue; // 搜索时无匹配则不显示该分组

                // 一级菜单：Header 直接用字符串，保留小三角和收缩功能
                var groupItem = new TreeViewItem { Header = group.GroupName, IsExpanded = group.IsDefaultExpanded };

                foreach (var item in filteredItems)
                {
                    var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    if (!string.IsNullOrEmpty(item.Icon))
                    {
                        try
                        {
                            var image = new Image { Width = 16, Height = 16, Margin = new Thickness(0, 0, 5, 0) };
                            
                            // 使用图像缓存获取图像
                            var bitmap = Utils.ImageCache.GetImage(item.Icon);
                            
                            if (bitmap != null)
                            {
                                image.Source = bitmap;
                                stackPanel.Children.Add(image);
                            }
                        }
                        catch { }
                    }
                    stackPanel.Children.Add(new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")) });
                    var subItem = new TreeViewItem { Header = stackPanel, Tag = item };
                    groupItem.Items.Add(subItem);
                }
                MenuTree.Items.Add(groupItem);
            }
        }

        // 搜索框变更事件
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.Trim();
            InitMenuTree(filter);
        }

        // 处理菜单点击事件
        private void MenuTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selected = MenuTree.SelectedItem as TreeViewItem;
            if (selected?.Tag is MenuItemData data)
            {
                // 获取父级菜单项（一级菜单）
                var parentItem = selected.Parent as TreeViewItem;
                if (parentItem != null)
                {
                    // 获取一级菜单的名称（根据InitMenuTree方法，一级菜单的Header是直接设置为字符串的）
                    string parentName = parentItem.Header?.ToString() ?? "";
                    
                    // 组合标题：一级菜单名-二级菜单名
                    string combinedTitle = $"{parentName}-{data.Name}";
                    
                    // 打开标签页，使用组合后的标题
                    OpenUrlInTab(combinedTitle, data.Url, true);
                }
                else
                {
                    // 如果没有找到父级菜单项，则使用原始标题
                    OpenUrlInTab(data.Name, data.Url, true);
                }
            }
        }

        /// <summary>
        /// 处理TreeView双击事件，实现一级菜单的展开/收缩
        /// </summary>
        private void MenuTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[双击事件] MouseDoubleClick 被触发");
            
            // 获取双击的TreeViewItem
            var hitTest = e.OriginalSource as DependencyObject;
            TreeViewItem clickedItem = null;
            
            // 向上查找TreeViewItem
            while (hitTest != null)
            {
                if (hitTest is TreeViewItem treeViewItem)
                {
                    clickedItem = treeViewItem;
                    break;
                }
                hitTest = VisualTreeHelper.GetParent(hitTest);
            }

            if (clickedItem != null)
            {
                // 检查是否为一级菜单（直接在MenuTree下的项目）
                bool isTopLevel = false;
                
                // 检查父容器是否是MenuTree本身
                var parent = VisualTreeHelper.GetParent(clickedItem);
                while (parent != null)
                {
                    if (parent == MenuTree)
                    {
                        isTopLevel = true;
                        break;
                    }
                    if (parent is TreeViewItem)
                    {
                        // 如果找到了TreeViewItem父级，说明这不是顶级项
                        break;
                    }
                    parent = VisualTreeHelper.GetParent(parent);
                }

                System.Diagnostics.Debug.WriteLine($"[双击检测] 项目: {clickedItem.Header}, 是否顶级: {isTopLevel}");

                // 如果是一级菜单，切换展开/收缩状态
                if (isTopLevel)
                {
                    // 记录当前状态
                    bool currentState = clickedItem.IsExpanded;
                    System.Diagnostics.Debug.WriteLine($"[一级菜单双击] {clickedItem.Header} - 当前展开状态: {currentState}");
                    
                    // 切换状态
                    clickedItem.IsExpanded = !currentState;
                    
                    System.Diagnostics.Debug.WriteLine($"[一级菜单双击] {clickedItem.Header} - 新展开状态: {clickedItem.IsExpanded}");
                    
                    e.Handled = true; // 阻止事件继续传播
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[二级菜单双击] 忽略处理");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[双击事件] 未找到TreeViewItem");
            }
        }

        /// <summary>
        /// 在Tab中打开网址（使用CefSharp浏览器）
        /// </summary>
        /// <returns>创建的标签页对象，如果已存在相同的标签页则返回该标签页</returns>
        public TabItem OpenUrlInTab(string title, string url, bool keepOriginalTitle = false)
        {
            // 检查是否已存在相同URL的标签页
            foreach (TabItem tab in MainTabControl.Items)
            {
                if (tab.Tag is TabInfo tabInfo && tabInfo.Url == url)
                {
                    MainTabControl.SelectedItem = tab;
                    
                    // 重新加载页面
                    if (tab.Content is ChromiumWebBrowser browser)
                    {
                        browser.Load(url);
                        System.Diagnostics.Debug.WriteLine($"[标签页重新加载] {title} - {url}");
                    }
                    
                    return tab;
                }
            }
            
            // 生成唯一的标签页ID
            string tabId = Guid.NewGuid().ToString();
            
            // 从浏览器实例管理器获取浏览器实例
            var newBrowser = Browser.BrowserInstanceManager.Instance.GetBrowser(url, tabId);
            
            // 使用 FrameLoadEnd 事件来获取页面标题
            EventHandler<FrameLoadEndEventArgs> titleUpdateHandler = null;
            titleUpdateHandler = (sender, args) => 
            {
                // 如果设置了保持原始标题，则不更新标题
                if (keepOriginalTitle)
                {
                    return;
                }
                
                if (args.Frame.IsMain)
                {
                    // 使用 JavaScript 执行获取页面标题
                    args.Frame.EvaluateScriptAsync("document.title").ContinueWith(t => 
                    {
                        if (!t.IsFaulted && t.Result.Success && t.Result.Result != null)
                        {
                            string pageTitle = t.Result.Result.ToString();
                            if (!string.IsNullOrWhiteSpace(pageTitle))
                            {
                                Dispatcher.BeginInvoke(new Action(() => 
                                {
                                    // 查找对应的标签页
                                    foreach (TabItem tab in MainTabControl.Items)
                                    {
                                        if (tab.Tag is TabInfo info && info.TabId == tabId)
                                        {
                                            tab.Header = pageTitle;
                                            System.Diagnostics.Debug.WriteLine($"[标签页标题已更新] {pageTitle}");
                                            break;
                                        }
                                    }
                                }));
                            }
                        }
                    });
                }
            };
            
            // 添加标题更新事件处理
            newBrowser.FrameLoadEnd += titleUpdateHandler;
            
            var browserContext = new BrowserContext { IsLoading = true };
            
            // 自动登录逻辑
            var siteItem = FindSiteItemByUrl(url);
            if (siteItem != null && siteItem.AutoLogin)
            {
                // 使用弱引用事件处理器避免内存泄漏
                EventHandler<FrameLoadEndEventArgs> frameLoadEndHandler = null;
                frameLoadEndHandler = (sender, args) =>
                {
                    if (args.Frame.IsMain)
                    {
                        // 执行自动登录脚本
                        ExecuteAutoLoginScript(args.Frame, siteItem);
                        
                        // 执行一次后取消事件订阅
                        newBrowser.FrameLoadEnd -= frameLoadEndHandler;
                    }
                };
                
                newBrowser.FrameLoadEnd += frameLoadEndHandler;
            }
            
            // 使用弱引用事件处理器避免内存泄漏
            EventHandler<LoadingStateChangedEventArgs> loadingStateChangedHandler = null;
            loadingStateChangedHandler = (sender, args) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (browserContext != null)
                    {
                        browserContext.IsLoading = args.IsLoading;
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);
            };
            
            newBrowser.LoadingStateChanged += loadingStateChangedHandler;
            
            // 创建 TabInfo 存储 URL 和 tabId
            var newTabInfo = new TabInfo
            {
                Url = url,
                TabId = tabId
            };
            
            // 创建标签页并添加到TabControl
            var tabItem = new TabItem
            {
                Header = title,
                Tag = newTabInfo, 
                Content = newBrowser,
                DataContext = browserContext
            };
            
            // 添加标签页关闭事件处理
            tabItem.Unloaded += (sender, e) =>
            {
                // 取消事件订阅
                newBrowser.LoadingStateChanged -= loadingStateChangedHandler;
                newBrowser.FrameLoadEnd -= titleUpdateHandler;
                
                // 释放浏览器实例
                if (sender is TabItem tab && tab.Tag is TabInfo info)
                {
                    Browser.BrowserInstanceManager.Instance.ReleaseBrowser(info.TabId);
                }
            };
            
            MainTabControl.Items.Add(tabItem);
            MainTabControl.SelectedItem = tabItem;
            
            return tabItem;
        }
        
        /// <summary>
        /// 执行自动登录脚本
        /// </summary>
        private void ExecuteAutoLoginScript(IFrame frame, SiteItem siteItem)
        {
            string username = siteItem.UseCommonCredentials ? siteItem.CommonUsername : siteItem.Username;
            string password = siteItem.UseCommonCredentials ? siteItem.CommonPassword : siteItem.Password;
            string usernameSelector = string.IsNullOrWhiteSpace(siteItem.UsernameSelector) ? "input[type=email],input[type=text],input[name*=user],input[name*=email],input[name*=login]" : siteItem.UsernameSelector;
            string passwordSelector = string.IsNullOrWhiteSpace(siteItem.PasswordSelector) ? "input[type=password]" : siteItem.PasswordSelector;
            string captchaSelector = siteItem.CaptchaSelector;
            string loginButtonSelector = string.IsNullOrWhiteSpace(siteItem.LoginButtonSelector) ? "button[type=submit],input[type=submit]" : siteItem.LoginButtonSelector;
            string loginPageFeature = siteItem.LoginPageFeature;
            string captchaValue = siteItem.CaptchaValue ?? "";
            
            if (siteItem.CaptchaMode == 1 && !string.IsNullOrWhiteSpace(siteItem.GoogleSecret))
            {
                captchaValue = BrowserTool.Utils.GoogleAuthenticator.GenerateCode(siteItem.GoogleSecret);
            }
            
            // 判断登录页特征
            string featureCheck = string.IsNullOrWhiteSpace(loginPageFeature) ? "" : $"if(!document.querySelector('{loginPageFeature}')) return;";
            string captchaJs = string.IsNullOrWhiteSpace(captchaSelector) ? "" : $"var captchaInput = document.querySelector('{captchaSelector}'); if(captchaInput) captchaInput.value = '{captchaValue.Replace("'", "\\'")}';";
            
            string js = $@"
                (function() {{
                    {featureCheck}
                    var userInput = document.querySelector('{usernameSelector}');
                    var passInput = document.querySelector('{passwordSelector}');
                    {captchaJs}
                    if(userInput) userInput.value = '{username?.Replace("'", "\\'") ?? ""}';
                    if(passInput) passInput.value = '{password?.Replace("'", "\\'") ?? ""}';
                    var form = userInput ? userInput.form : (passInput ? passInput.form : null);
                    if(form) {{
                        form.submit();
                    }} else {{
                        var btn = document.querySelector('{loginButtonSelector}');
                        if(btn) btn.click();
                    }}
                }})();
            ";
            
            frame.ExecuteJavaScriptAsync(js);
        }

        // 辅助方法：根据url查找SiteItem
        private SiteItem FindSiteItemByUrl(string url)
        {
            var allGroups = BrowserTool.Database.SiteConfig.GetAllGroups();
            foreach (var group in allGroups)
            {
                var site = group.Sites.FirstOrDefault(s => s.Url == url);
                if (site != null) return site;
            }
            return null;
        }

        /// <summary>
        /// F12 打开开发者工具
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12)
            {
                // 获取当前Tab的浏览器控件
                if (MainTabControl.SelectedItem is TabItem tab && tab.Content is ChromiumWebBrowser currentBrowser)
                {
                    CefHelper.ShowDevTools(currentBrowser);
                }
            }
        }

        /// <summary>
        /// 测试环境按钮点击事件处理
        /// </summary>
        private void TestEnvButton_Click(object sender, RoutedEventArgs e)
        {
            // 从配置文件读取测试环境URL
            string testUrl = System.Configuration.ConfigurationManager.AppSettings["TestEnvironmentUrl"];
            
            if (!string.IsNullOrEmpty(testUrl))
            {
                // 直接打开测试环境标签页
                OpenUrlInTab("测试环境", testUrl, true);
            }
            else
            {
                MessageBox.Show("测试环境URL未配置，请在App.config中设置TestEnvironmentUrl", "配置错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 抽屉开关按钮点击事件处理
        /// </summary>
        private void DrawerToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (isDrawerOpen)
            {
                DrawerCol.Width = new GridLength(0);
                DrawerBorder.Visibility = Visibility.Collapsed;
            }
            else
            {
                DrawerCol.Width = new GridLength(200);
                DrawerBorder.Visibility = Visibility.Visible;
            }
            isDrawerOpen = !isDrawerOpen;

            // 获取当前选中的标签页中的浏览器控件
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    var host = browser.GetBrowser()?.GetHost();
                    if (host != null)
                    {
                        // 通知窗口移动或调整大小开始
                        host.NotifyMoveOrResizeStarted();
                        // 通知屏幕信息变化
                        host.NotifyScreenInfoChanged();
                        // 通知窗口已调整大小
                        host.WasResized();
                    }
                }
            }
        }

        // 后退按钮点击事件
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null && browser.CanGoBack)
                {
                    browser.Back();
                }
            }
        }

        // 前进按钮点击事件
        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null && browser.CanGoForward)
                {
                    browser.Forward();
                }
            }
        }

        // 刷新按钮点击事件
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    browser.Reload();
                }
            }
        }

        // URL输入框回车事件
        private void UrlTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (MainTabControl.SelectedItem is TabItem tabItem)
                {
                    var browser = tabItem.Content as ChromiumWebBrowser;
                    if (browser != null)
                    {
                        string url = UrlTextBox.Text;
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                        {
                            url = "https://" + url;
                        }
                        browser.LoadUrl(url);
                    }
                }
            }
        }

        // 开发者工具按钮点击事件
        private void DevToolsButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    string url = UrlTextBox.Text;
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                        {
                            url = "https://" + url;
                        }
                        browser.LoadUrl(url);
                    }
                    //browser.ShowDevTools();
                }
            }
         
        }

        // 获取当前标签页的浏览器控件
        private ChromiumWebBrowser GetCurrentBrowser()
        {
            var selectedTab = MainTabControl.SelectedItem as TabItem;
            if (selectedTab != null)
            {
                var contentPresenter = selectedTab.Content as ContentPresenter;
                if (contentPresenter != null)
                {
                    var grid = contentPresenter.Content as Grid;
                    if (grid != null)
                    {
                        return grid.Children.OfType<ChromiumWebBrowser>().FirstOrDefault();
                    }
                }
            }
            return null;
        }

        // 更新URL输入框内容
        public void UpdateUrl(string url)
        {
            var selectedTab = MainTabControl.SelectedItem as TabItem;
            if (selectedTab != null)
            {
                var contentPresenter = selectedTab.Content as ContentPresenter;
                if (contentPresenter != null)
                {
                    var grid = contentPresenter.Content as Grid;
                    if (grid != null)
                    {
                        var urlTextBox = grid.FindName("UrlTextBox") as TextBox;
                        if (urlTextBox != null)
                        {
                            urlTextBox.Text = url;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 清除TreeView的选中状态
        /// </summary>
        private void ClearTreeViewSelection()
        {
            foreach (TreeViewItem item in MenuTree.Items)
            {
                item.IsSelected = false;
                foreach (TreeViewItem child in item.Items)
                {
                    child.IsSelected = false;
                }
            }
        }

        /// <summary>
        /// 关闭指定的标签页
        /// </summary>
        public void CloseTab(TabItem tabItem)
        {
            if (tabItem != null)
            {
                MainTabControl.Items.Remove(tabItem);
                // 清除MenuTree的选中状态
                ClearTreeViewSelection();
            }
        }

        /// <summary>
        /// 切换地址栏的显示/隐藏状态
        /// </summary>
        public void ToggleUrlBar()
        {
            var urlBar = this.FindName("UrlBar") as Grid;
            if (urlBar != null)
            {
                urlBar.Visibility = urlBar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                // 如果显示地址栏，更新当前地址
                if (urlBar.Visibility == Visibility.Visible)
                {
                    if (MainTabControl.SelectedItem is TabItem tabItem)
                    {
                        var browser = tabItem.Content as ChromiumWebBrowser;
                        if (browser != null)
                        {
                            UrlTextBox.Text = browser.Address;
                        }
                    }
                }
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    UrlTextBox.Text = browser.Address;
                }
            }
        }

        private void MainTabControl_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 获取鼠标位置下的标签页
            var tabItem = GetTabItemAtPosition(e.GetPosition(MainTabControl));
            if (tabItem != null)
            {
                MainTabControl.SelectedItem = tabItem;
            }
        }

        private CustomPopupPlacement[] ContextMenu_PlacementCallback(Size popupSize, Size targetSize, Point offset)
        {
            // 获取鼠标在屏幕上的位置
            Point mousePosition = Mouse.GetPosition(MainTabControl);
            mousePosition = MainTabControl.PointToScreen(mousePosition);

            // 计算菜单位置
            Point placementPoint = new Point(mousePosition.X, mousePosition.Y);

            // 如果左侧菜单是展开的，调整菜单位置
            if (isDrawerOpen)
            {
                placementPoint.X -= DrawerCol.Width.Value;
            }

            return new[]
            {
                new CustomPopupPlacement(placementPoint, PopupPrimaryAxis.Horizontal)
            };
        }

        private TabItem GetTabItemAtPosition(Point position)
        {
            var result = VisualTreeHelper.HitTest(MainTabControl, position);
            if (result != null)
            {
                var element = result.VisualHit;
                while (element != null && !(element is TabItem))
                {
                    element = VisualTreeHelper.GetParent(element);
                }
                return element as TabItem;
            }
            return null;
        }

        private void CloseTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                // 如果标签页包含TabInfo，释放浏览器实例
                if (tabItem.Tag is TabInfo tabInfo)
                {
                    Browser.BrowserInstanceManager.Instance.ReleaseBrowser(tabInfo.TabId);
                }
                
                MainTabControl.Items.Remove(tabItem);
                // 清除MenuTree的选中状态
                ClearTreeViewSelection();
            }
        }

        private void CloseOtherTabsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem selectedTab)
            {
                var tabsToRemove = MainTabControl.Items.Cast<TabItem>()
                    .Where(tab => tab != selectedTab)
                    .ToList();

                foreach (var tab in tabsToRemove)
                {
                    MainTabControl.Items.Remove(tab);
                }
            }
        }

        private void CloseRightTabsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem selectedTab)
            {
                var selectedIndex = MainTabControl.Items.IndexOf(selectedTab);
                var tabsToRemove = MainTabControl.Items.Cast<TabItem>()
                    .Where((tab, index) => index > selectedIndex)
                    .ToList();

                foreach (var tab in tabsToRemove)
                {
                    MainTabControl.Items.Remove(tab);
                }
            }
        }

        private void RefreshTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    browser.Reload();
                }
            }
        }

        private void CopyUrlMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    Clipboard.SetText(browser.Address);
                }
                else if (tabItem.Tag is TabInfo tabInfo)
                {
                    Clipboard.SetText(tabInfo.Url);
                }
            }
        }

        private void DuplicateTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    // 获取当前标签页的标题
                    string baseTitle = tabItem.Header.ToString();
                    // 移除可能存在的数字后缀
                    baseTitle = System.Text.RegularExpressions.Regex.Replace(baseTitle, @"\d+$", "").Trim();
                    
                    // 查找所有以相同基础名称开头的标签页
                    var existingTabs = MainTabControl.Items.Cast<TabItem>()
                        .Where(t => t.Header.ToString().StartsWith(baseTitle))
                        .ToList();

                    // 计算新的数字后缀
                    int newNumber = 1;
                    if (existingTabs.Any())
                    {
                        var numbers = existingTabs
                            .Select(t => t.Header.ToString())
                            .Select(title => System.Text.RegularExpressions.Regex.Match(title, @"\d+$"))
                            .Where(m => m.Success)
                            .Select(m => int.Parse(m.Value))
                            .ToList();
                        
                        if (numbers.Any())
                        {
                            newNumber = numbers.Max() + 1;
                        }
                    }

                    // 创建新的浏览器实例，加载相同的URL
                    var newBrowser = new ChromiumWebBrowser(browser.Address);
                    // 复制原标签页的处理器
                    newBrowser.DownloadHandler = new CefDownloadHandler();
                    newBrowser.MenuHandler = new CefMenuHandler();
                    newBrowser.LifeSpanHandler = new Browser.CefLifeSpanHandler();
                    
                    // 创建新标签页，使用带数字的标题
                    var newTabItem = new TabItem
                    {
                        Header = $"{baseTitle}{newNumber}",
                        Tag = browser.Address,
                        Content = newBrowser
                    };
                    
                    MainTabControl.Items.Add(newTabItem);
                    MainTabControl.SelectedItem = newTabItem;
                }
                else if (tabItem.Tag is TabInfo tabInfo)
                {
                    // 获取当前标签页的标题
                    string baseTitle = tabItem.Header.ToString();
                    // 移除可能存在的数字后缀
                    baseTitle = System.Text.RegularExpressions.Regex.Replace(baseTitle, @"\d+$", "").Trim();
                    
                    // 查找所有以相同基础名称开头的标签页
                    var existingTabs = MainTabControl.Items.Cast<TabItem>()
                        .Where(t => t.Header.ToString().StartsWith(baseTitle))
                        .ToList();

                    // 计算新的数字后缀
                    int newNumber = 1;
                    if (existingTabs.Any())
                    {
                        var numbers = existingTabs
                            .Select(t => t.Header.ToString())
                            .Select(title => System.Text.RegularExpressions.Regex.Match(title, @"\d+$"))
                            .Where(m => m.Success)
                            .Select(m => int.Parse(m.Value))
                            .ToList();
                        
                        if (numbers.Any())
                        {
                            newNumber = numbers.Max() + 1;
                        }
                    }

                    // 创建新的浏览器实例，加载相同的URL
                    var newBrowser = new ChromiumWebBrowser(tabInfo.Url);
                    // 复制原标签页的处理器
                    newBrowser.DownloadHandler = new CefDownloadHandler();
                    newBrowser.MenuHandler = new CefMenuHandler();
                    newBrowser.LifeSpanHandler = new Browser.CefLifeSpanHandler();
                    
                    // 创建新标签页，使用带数字的标题
                    var newTabItem = new TabItem
                    {
                        Header = $"{baseTitle}{newNumber}",
                        Tag = tabInfo,
                        Content = newBrowser
                    };
                    
                    MainTabControl.Items.Add(newTabItem);
                    MainTabControl.SelectedItem = newTabItem;
                }
            }
        }

        private void OpenInNewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    OpenUrlInTab(tabItem.Header.ToString(), browser.Address);
                }
                else if (tabItem.Tag is TabInfo tabInfo)
                {
                    OpenUrlInTab(tabItem.Header.ToString(), tabInfo.Url);
                }
            }
        }

        public class BrowserContext : INotifyPropertyChanged
        {
            private bool _isLoading;
            public bool IsLoading
            {
                get => _isLoading;
                set
                {
                    if (_isLoading != value)
                    {
                        _isLoading = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
                    }
                }
            }


            public event PropertyChangedEventHandler PropertyChanged;
        }

        private void LoadMenuGroupsFromDb()
        {
            try
            {
                // 确保数据库已初始化
                SiteConfig.InitializeDatabase();

                // 确保每次都从数据库重新加载最新数据
                var allGroups = SiteConfig.GetAllGroups();
                System.Diagnostics.Debug.WriteLine($"加载到 {allGroups.Count} 个分组");

                // 清空现有数据
                menuGroups.Clear();

                // 转换数据
                menuGroups = allGroups.Select(g => new MenuGroup
                {
                    GroupName = g.Name,
                    IsDefaultExpanded = g.IsDefaultExpanded,
                    Items = g.Sites.Where(s => s.IsEnabled)
                                   .OrderBy(s => s.SortOrder)  // 确保按照排序顺序显示
                                   .Select(s => new MenuItemData
                    {
                        Name = s.DisplayName,
                        Url = s.Url,
                        Icon = s.Icon
                    }).ToList()
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"转换后得到 {menuGroups.Count} 个菜单组");

                // 强制刷新 UI
                if (MenuTree != null)
                {
                    // 清空现有项
                    MenuTree.Items.Clear();

                    // 添加新项
                    foreach (var group in menuGroups)
                    {
                        var groupItem = new TreeViewItem { Header = group.GroupName, IsExpanded = group.IsDefaultExpanded };
                        foreach (var item in group.Items)
                        {
                            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                            if (!string.IsNullOrEmpty(item.Icon))
                            {
                                try
                                {
                                    var image = new Image { Width = 16, Height = 16, Margin = new Thickness(0, 0, 5, 0) };
                                    var bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    if (item.Icon.StartsWith("data:image") || item.Icon.Length > 200) // Base64
                                    {
                                        var bytes = Convert.FromBase64String(item.Icon.Contains(",") ? item.Icon.Substring(item.Icon.IndexOf(",") + 1) : item.Icon);
                                        bitmap.StreamSource = new MemoryStream(bytes);
                                    }
                                    else if (File.Exists(item.Icon))
                                    {
                                        bitmap.UriSource = new Uri(item.Icon);
                                    }
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.EndInit();
                                    bitmap.Freeze();
                                    image.Source = bitmap;
                                    stackPanel.Children.Add(image);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"加载图标时出错：{ex.Message}");
                                }
                            }
                            stackPanel.Children.Add(new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")) });
                            var subItem = new TreeViewItem { Header = stackPanel, Tag = item };
                            groupItem.Items.Add(subItem);
                        }
                        MenuTree.Items.Add(groupItem);
                    }
                    System.Diagnostics.Debug.WriteLine($"UI 更新完成，共添加 {MenuTree.Items.Count} 个分组");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载菜单数据时出错：{ex}");
                MessageBox.Show($"加载菜单数据时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 最小化按钮点击事件处理
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化按钮点击事件处理
        /// </summary>
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized) 
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
                this.MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
            }
        }

        /// <summary>
        /// 关闭按钮点击事件处理
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _mouseActivitySimulator.Stop(); // 确保在关闭窗口时停止鼠标活动模拟
            Close();
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            // 创建设置窗口，并传递主窗口引用
            var settingsWindow = new SettingsWindow(this);
            
            // 确保在显示对话框之前订阅事件
            System.Diagnostics.Debug.WriteLine("[MainWindow.btnSettings_Click] 准备订阅 SettingsSaved 事件");
            
            // 使用具名方法而不是匿名方法，以便于调试
            settingsWindow.SettingsSaved += SettingsWindow_SettingsSaved;
            
            // 显示对话框
            bool? result = settingsWindow.ShowDialog();
            
            // 对话框关闭后，无论如何都刷新菜单（不依赖事件）
            System.Diagnostics.Debug.WriteLine("[MainWindow.btnSettings_Click] 设置窗口已关闭，准备刷新菜单");
            
            // 刷新菜单
            RefreshMenuFromSettings();
        }
        
        /// <summary>
        /// 刷新主窗口菜单，可以被设置窗口直接调用
        /// </summary>
        public void RefreshMenuFromSettings()
        {
            // 使用 Dispatcher 确保在 UI 线程上执行
            Dispatcher.BeginInvoke(new Action(() => {
                try
                {
                    // 调试输出
                    System.Diagnostics.Debug.WriteLine("[MainWindow.RefreshMenuFromSettings] 开始重新加载菜单");
                    
                    // 重新加载菜单
                    LoadMenuGroupsFromDb();
                    // 强制刷新 UI
                    MenuTree.Items.Refresh();
                    
                    // 调试输出
                    System.Diagnostics.Debug.WriteLine("[MainWindow.RefreshMenuFromSettings] 菜单刷新完成");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow.RefreshMenuFromSettings] 刷新菜单时出错：{ex}");
                    MessageBox.Show($"刷新菜单时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void SettingsWindow_SettingsSaved(object sender, EventArgs args)
        {
            // 调试输出
            System.Diagnostics.Debug.WriteLine("[MainWindow] SettingsSaved 事件被触发，准备刷新菜单");
            
            // 使用 Dispatcher 确保在 UI 线程上执行
            Dispatcher.BeginInvoke(new Action(() => {
                try
                {
                    // 调试输出
                    System.Diagnostics.Debug.WriteLine("[MainWindow] 开始重新加载菜单");
                    
                    // 重新加载菜单
                    LoadMenuGroupsFromDb();
                    // 强制刷新 UI
                    MenuTree.Items.Refresh();
                    
                    // 调试输出
                    System.Diagnostics.Debug.WriteLine("[MainWindow] 菜单刷新完成");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] 刷新菜单时出错：{ex}");
                    MessageBox.Show($"刷新菜单时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        /// <summary>
        /// 最小化按钮点击事件处理
        /// </summary>
        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 关闭按钮点击事件处理
        /// </summary>
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            _mouseActivitySimulator.Stop(); // 确保在关闭窗口时停止鼠标活动模拟
            Close();
        }

        /// <summary>
        /// 复制标签页标题到剪贴板
        /// </summary>
        private void CopyTitleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                Clipboard.SetText(tabItem.Header.ToString());
            }
        }

        /// <summary>
        /// 复制选中内容到剪贴板
        /// </summary>
        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    browser.Copy();
                }
            }
        }

        /// <summary>
        /// 粘贴剪贴板内容
        /// </summary>
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    browser.Paste();
                }
            }
        }

        /// <summary>
        /// 粘贴剪贴板内容并访问URL
        /// </summary>
        private void PasteAndGoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    string url = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                        {
                            url = "https://" + url;
                        }
                        browser.LoadUrl(url);
                    }
                }
            }
        }
        

        /// <summary>
        /// 窗口关闭事件处理 - 最小化到托盘而不是退出
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            // 取消关闭操作，改为隐藏到托盘
            e.Cancel = true;
            this.Hide();
            
            // 可选：显示托盘提示
            var trayIcon = (TaskbarIcon)Application.Current.Resources["TrayIcon"];
            if (trayIcon != null)
            {
                trayIcon.ShowBalloonTip("Browser Tool", "程序已最小化到系统托盘", BalloonIcon.Info);
            }
        }

        /// <summary>
        /// 标题栏鼠标左键按下事件处理 - 支持拖拽和双击最大化/还原
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击最大化/还原
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowState = WindowState.Maximized;
                    this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
                    this.MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
                }
                   
            }
            else if (e.ButtonState == MouseButtonState.Pressed)
            {
                // 单击拖动
                DragMove();
            }
        }
        
        /// <summary>
        /// 主窗体大小改变事件处理
        /// </summary>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 延迟执行以确保布局完成
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 遍历所有标签页，刷新浏览器控件的大小
                    foreach (TabItem tab in MainTabControl.Items)
                    {
                        if (tab.Content is ChromiumWebBrowser browser)
                        {
                            // 强制重新计算布局
                            browser.InvalidateVisual();
                            browser.UpdateLayout();
                            
                            // 如果浏览器已经加载完成，可以尝试调用JavaScript来调整页面
                            if (!browser.IsLoading && browser.CanExecuteJavascriptInMainFrame)
                            {
                                browser.ExecuteScriptAsync("if(window.dispatchEvent) { window.dispatchEvent(new Event('resize')); }");
                            }
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[窗口大小改变] 新尺寸: {e.NewSize.Width}x{e.NewSize.Height}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[窗口大小改变处理错误] {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// 关闭标签页按钮点击事件
        /// </summary>
        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var tabItem = button?.Tag as TabItem;
                
                if (tabItem != null && MainTabControl.Items.Contains(tabItem))
                {
                    // 如果关闭的是当前选中的标签页，且还有其他标签页，则选择相邻的标签页
                    if (MainTabControl.SelectedItem == tabItem && MainTabControl.Items.Count > 1)
                    {
                        var index = MainTabControl.Items.IndexOf(tabItem);
                        if (index > 0)
                        {
                            MainTabControl.SelectedIndex = index - 1;
                        }
                        else if (index < MainTabControl.Items.Count - 1)
                        {
                            MainTabControl.SelectedIndex = index + 1;
                        }
                    }
                    
                    // 释放浏览器资源
                    if (tabItem.Content is ChromiumWebBrowser browser)
                    {
                        browser.Dispose();
                    }
                    
                    // 移除标签页
                    MainTabControl.Items.Remove(tabItem);
                    
                    // 如果没有标签页了，可以考虑添加一个默认标签页或隐藏标签控件
                    if (MainTabControl.Items.Count == 0)
                    {
                        // 可以在这里添加默认行为，比如打开首页
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[关闭标签页错误] {ex.Message}");
            }
        }

        /// <summary>
        /// 登录状态变化事件处理
        /// </summary>
        /// <param name="isLoggedIn">登录状态，true表示已登录，false表示未登录</param>
        private void OnLoginStatusChanged(bool isLoggedIn)
        {
            // 在UI线程中执行
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (isLoggedIn)
                {
                    _mouseActivitySimulator = new MouseActivitySimulator();
                    _mouseActivitySimulator.Start();

                    // 登录成功后刷新菜单
                    RefreshMenuFromSettings();
                }
               
            }));
        }
    }
}
