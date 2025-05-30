using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CefSharp;
using CefSharp.Wpf;
using BrowserTool.Browser;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Security.Policy;
using BrowserTool.Database.Entities;
using System.Windows.Threading;
using BrowserTool.Database;
using BrowserTool.Utils;

namespace BrowserTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 菜单数据结构
        public class MenuGroup
        {
            public string GroupName { get; set; }
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
        private BrowserTabManager _tabManager;

        // 添加公共属性以访问抽屉状态
        public bool IsDrawerOpen => isDrawerOpen;

        private WindowState _lastWindowState = WindowState.Normal;
        private Rect _restoreBounds;

        // 鼠标活动模拟器实例
        private readonly MouseActivitySimulator _mouseActivitySimulator;

        public MainWindow()
        {
            try
            {
                if (Cef.IsInitialized != true)
                {
                    var settings = new CefSettings();
                    
                    // Set cache path in user data directory
                    string cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrowserTool", "CEF");
                    if (!Directory.Exists(cachePath))
                    {
                        Directory.CreateDirectory(cachePath);
                    }
                    settings.CachePath = cachePath;
                    
                    // Additional settings for v136.1.40
                    settings.PersistSessionCookies = true;
                    
                    // Initialize CEF with the specified settings
                    Cef.Initialize(settings);
                }
                InitializeComponent();
                _mouseActivitySimulator = new MouseActivitySimulator();
                _mouseActivitySimulator.Start(); // 程序启动时自动启动鼠标活动模拟
                LoadMenuGroupsFromDb();
                InitMenuTree();
                _tabManager = new BrowserTabManager(MainTabControl, this);
                this.KeyDown += MainWindow_KeyDown; // 监听F12
                MainTabControl.SelectionChanged += MainTabControl_SelectionChanged; // 监听标签页切换

                // 窗口居中显示
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                // 订阅数据变更事件
                SiteConfig.DataChanged += OnDataChanged;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("MainWindow 初始化异常: " + ex.Message);
                Application.Current.Shutdown();
            }
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
                var groupItem = new TreeViewItem { Header = group.GroupName, IsExpanded = true };

                foreach (var item in filteredItems)
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
                OpenUrlInTab(data.Name, data.Url);
            }
        }

        /// <summary>
        /// 在Tab中打开网址（使用CefSharp浏览器）
        /// </summary>
        private void OpenUrlInTab(string title, string url)
        {
            foreach (TabItem tab in MainTabControl.Items)
            {
                if (tab.Tag is string tagUrl && tagUrl == url)
                {
                    MainTabControl.SelectedItem = tab;
                    return;
                }
            }
            var newBrowser = new ChromiumWebBrowser(url);
            newBrowser.DownloadHandler = new CefDownloadHandler();
            newBrowser.MenuHandler = new CefMenuHandler();
            newBrowser.LifeSpanHandler = new Browser.CefLifeSpanHandler();

            var browserContext = new BrowserContext { IsLoading = true };

            // 自动登录逻辑
            var siteItem = FindSiteItemByUrl(url);
            if (siteItem != null && siteItem.AutoLogin)
            {
                newBrowser.FrameLoadEnd += (sender, args) =>
                {
                    if (args.Frame.IsMain)
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
                        args.Frame.ExecuteJavaScriptAsync(js);
                    }
                };
            }

            newBrowser.LoadingStateChanged += (sender, args) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    browserContext.IsLoading = args.IsLoading;
                });
            };

            var tabItem = new TabItem
            {
                Header = title,
                Tag = url,
                Content = newBrowser,
                DataContext = browserContext
            };
            MainTabControl.Items.Add(tabItem);
            MainTabControl.SelectedItem = tabItem;
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
            }
        }

        private void CopyTitleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                Clipboard.SetText(tabItem.Header.ToString());
            }
        }

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

        private void OpenInNewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl.SelectedItem is TabItem tabItem)
            {
                var browser = tabItem.Content as ChromiumWebBrowser;
                if (browser != null)
                {
                    OpenUrlInTab(tabItem.Header.ToString(), browser.Address);
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
                    Items = g.Sites.Where(s => s.IsEnabled).Select(s => new MenuItemData
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
                        var groupItem = new TreeViewItem { Header = group.GroupName, IsExpanded = true };
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

        // 支持鼠标拖动窗口
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            // 只在点击标题栏区域时允许拖动和双击最大化
            if (e.GetPosition(this).Y <= 40)
            {
                if (e.ClickCount == 2)
                {
                    // 双击最大化/还原
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
                else
                {
                    this.DragMove();
                }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd).AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            if (msg == WM_NCHITTEST)
            {
                var mousePos = GetMousePosition(lParam);
                var windowPos = this.PointToScreen(new System.Windows.Point(0, 0));
                double width = this.ActualWidth;
                double height = this.ActualHeight;
                int edge = 8;

                // 标题栏按钮区域（右上角120x40）
                double btnAreaLeft = windowPos.X + width - 120;
                double btnAreaTop = windowPos.Y;
                double btnAreaRight = windowPos.X + width;
                double btnAreaBottom = windowPos.Y + 40;
                if (mousePos.X >= btnAreaLeft && mousePos.X <= btnAreaRight && mousePos.Y >= btnAreaTop && mousePos.Y <= btnAreaBottom)
                {
                    handled = false; // 让WPF处理按钮点击
                    return IntPtr.Zero;
                }

                // 边缘判定
                if (mousePos.Y >= windowPos.Y && mousePos.Y < windowPos.Y + edge)
                {
                    handled = true;
                    if (mousePos.X >= windowPos.X && mousePos.X < windowPos.X + edge)
                        return (IntPtr)HTTOPLEFT;
                    if (mousePos.X < windowPos.X + width && mousePos.X >= windowPos.X + width - edge)
                        return (IntPtr)HTTOPRIGHT;
                    return (IntPtr)HTTOP;
                }
                if (mousePos.Y < windowPos.Y + height && mousePos.Y >= windowPos.Y + height - edge)
                {
                    handled = true;
                    if (mousePos.X >= windowPos.X && mousePos.X < windowPos.X + edge)
                        return (IntPtr)HTBOTTOMLEFT;
                    if (mousePos.X < windowPos.X + width && mousePos.X >= windowPos.X + width - edge)
                        return (IntPtr)HTBOTTOMRIGHT;
                    return (IntPtr)HTBOTTOM;
                }
                if (mousePos.X >= windowPos.X && mousePos.X < windowPos.X + edge)
                {
                    handled = true;
                    return (IntPtr)HTLEFT;
                }
                if (mousePos.X < windowPos.X + width && mousePos.X >= windowPos.X + width - edge)
                {
                    handled = true;
                    return (IntPtr)HTRIGHT;
                }
                handled = false;
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        private System.Windows.Point GetMousePosition(IntPtr lParam)
        {
            int x = (short)((uint)lParam & 0xFFFF);
            int y = (short)(((uint)lParam >> 16) & 0xFFFF);
            return new System.Windows.Point(x, y);
        }

        private void CloseTabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var tabItem = button.Tag as TabItem;
                if (tabItem != null)
                {
                    // 如果关闭的是当前选中的标签页，需要先选择其他标签页
                    if (tabItem == MainTabControl.SelectedItem && MainTabControl.Items.Count > 1)
                    {
                        int currentIndex = MainTabControl.Items.IndexOf(tabItem);
                        int newIndex = currentIndex > 0 ? currentIndex - 1 : 1;
                        MainTabControl.SelectedIndex = newIndex;
                    }

                    MainTabControl.Items.Remove(tabItem);
                    // 清除MenuTree的选中状态
                    ClearTreeViewSelection();
                }
            }
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            // 订阅设置保存事件
            settingsWindow.SettingsSaved += (s, args) => {
                // 使用 Dispatcher 确保在 UI 线程上执行
                Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        // 重新加载菜单
                        LoadMenuGroupsFromDb();
                        // 强制刷新 UI
                        MenuTree.Items.Refresh();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"刷新菜单时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            };
            
            settingsWindow.ShowDialog();
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
    }
}
