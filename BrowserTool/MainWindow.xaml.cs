using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using BrowserTool.Browser;
using BrowserTool.Database;
using BrowserTool.Database.Entities;
using BrowserTool.Utils;
using CefSharp;
using CefSharp.Wpf;
using Hardcodet.Wpf.TaskbarNotification;
using NLog;

namespace BrowserTool;

/// <summary>
/// 标签页信息类，用于存储标签页的URL和ID
/// </summary>
public sealed record TabInfo
{
    /// <summary>
    /// 获取或设置当前URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置标签页唯一标识符
    /// </summary>
    public string TabId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置原始URL，用于标识标签页来源的菜单项
    /// </summary>
    public string OriginalUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置菜单项ID，用于唯一标识来源的二级菜单
    /// </summary>
    public int? MenuItemId { get; set; }

    /// <summary>
    /// 获取或设置菜单项标题，用于显示和调试
    /// </summary>
    public string MenuItemTitle { get; set; } = string.Empty;
}

/// <summary>
/// MainWindow.xaml 的交互逻辑类
/// </summary>
public partial class MainWindow : Window
{
    #region 嵌套类型定义

    /// <summary>
    /// 菜单组数据结构
    /// </summary>
    public sealed record MenuGroup
    {
        /// <summary>
        /// 获取或设置组名称
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置是否默认展开
        /// </summary>
        public bool IsDefaultExpanded { get; set; }

        /// <summary>
        /// 获取或设置菜单项列表
        /// </summary>
        public List<MenuItemData> Items { get; set; } = new();
    }

    /// <summary>
    /// 菜单项数据结构
    /// </summary>
    public sealed record MenuItemData
    {
        /// <summary>
        /// 获取或设置菜单项的数据库ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置菜单项名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置菜单项URL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置菜单项图标
        /// </summary>
        public string Icon { get; set; } = string.Empty;
    }

    /// <summary>
    /// 浏览器上下文类，实现属性变更通知
    /// </summary>
    public sealed class BrowserContext : INotifyPropertyChanged
    {
        private bool _isLoading;

        /// <summary>
        /// 获取或设置是否正在加载
        /// </summary>
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

        /// <summary>
        /// 属性变更通知事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
    }



    #endregion

    #region 字段和属性

    /// <summary>
    /// 菜单数据集合
    /// </summary>
    private readonly List<MenuGroup> _menuGroups = new();

    /// <summary>
    /// 抽屉是否打开
    /// </summary>
    private bool _isDrawerOpen = true;

    /// <summary>
    /// 最后的窗口状态
    /// </summary>
    private WindowState _lastWindowState = WindowState.Normal;

    /// <summary>
    /// 恢复边界
    /// </summary>
    private Rect _restoreBounds;

    /// <summary>
    /// 日志记录器
    /// </summary>
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Windows消息常量
    /// </summary>
    private static readonly int WM_SHOWMAINWINDOW = RegisterWindowMessage("BrowserTool_ShowMainWindow");

    /// <summary>
    /// 获取抽屉是否打开状态
    /// </summary>
    public bool IsDrawerOpen => _isDrawerOpen;

    /// <summary>
    /// 打卡页面域名列表（写死的成员变量）
    /// </summary>
    private readonly string[] _checkInDomains = { "attendance.company.com", "checkin.office.com", "www.google.com" };

    #endregion

    #region Windows API 声明

    /// <summary>
    /// 注册自定义Windows消息
    /// </summary>
    /// <param name="lpString">消息字符串</param>
    /// <returns>消息ID</returns>
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int RegisterWindowMessage(string lpString);

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化MainWindow的新实例
    /// </summary>
    public MainWindow()
    {
        try
        {
            InitializeComponent();
            InitializeMainWindow();
        }
        catch (Exception ex)
        {
            _logger.Error("初始化主窗口时发生异常", ex);
            Application.Current.Shutdown();
        }
    }

    /// <summary>
    /// 初始化主窗口设置
    /// </summary>
    private void InitializeMainWindow()
    {
        // 监听标签页切换
        MainTabControl.SelectionChanged += MainTabControl_SelectionChanged;

        // 窗口居中显示
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // 订阅数据变更事件
        SiteConfig.DataChanged += OnDataChanged;

        // 添加Windows消息处理
        SourceInitialized += MainWindow_SourceInitialized;

        // 订阅登录状态变化事件
        LoginManager.OnLoginStatusChanged += OnLoginStatusChanged;

        // 设置ContextMenu的PlacementTarget
        TabContextMenu.Opened += (s, e) =>
        {
            if (TabContextMenu.PlacementTarget == null)
            {
                TabContextMenu.PlacementTarget = MainTabControl;
            }
        };
    }

    #endregion

    #region Windows 消息处理

    /// <summary>
    /// 窗口源初始化事件处理器
    /// </summary>
    private void MainWindow_SourceInitialized(object sender, EventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source.AddHook(WndProc);
    }

    /// <summary>
    /// Windows消息处理程序
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="msg">消息类型</param>
    /// <param name="wParam">消息参数</param>
    /// <param name="lParam">消息参数</param>
    /// <param name="handled">是否已处理</param>
    /// <returns>处理结果</returns>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SHOWMAINWINDOW)
        {
            _logger.Debug($"收到WM_SHOWMAINWINDOW消息: {msg}");

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var app = App.GetCurrentApp();
                if (app is not null)
                {
                    _logger.Debug("调用App.ShowMainWindow");
                    app.ShowMainWindow();
                }
                else
                {
                    _logger.Debug("App实例为null");
                }
            }));

            handled = true;
        }

        return IntPtr.Zero;
    }

    #endregion

    #region 数据变更处理

    /// <summary>
    /// 数据变更事件处理器
    /// </summary>
    private void OnDataChanged(object sender, EventArgs e) =>
        Dispatcher.Invoke(LoadMenuGroupsFromDb);

    /// <summary>
    /// 登录状态变化事件处理器
    /// </summary>
    /// <param name="isLoggedIn">是否已登录</param>
    private void OnLoginStatusChanged(bool isLoggedIn)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (isLoggedIn)
            {
                RefreshMenuFromSettings();
            }
        }));
    }

    #endregion

    #region 资源清理

    /// <summary>
    /// 窗口关闭时的清理工作
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        SiteConfig.DataChanged -= OnDataChanged;
    }

    #endregion

    #region 菜单管理

    /// <summary>
    /// 从数据库加载菜单组数据
    /// </summary>
    private void LoadMenuGroupsFromDb()
    {
        try
        {
            SiteConfig.InitializeDatabase();
            var allGroups = SiteConfig.GetAllGroups();

            _logger.Debug($"加载到 {allGroups.Count} 个分组");

            _menuGroups.Clear();
            _menuGroups.AddRange(allGroups.Select(g => new MenuGroup
            {
                GroupName = g.Name,
                IsDefaultExpanded = g.IsDefaultExpanded,
                Items = g.Sites
                    .Where(s => s.IsEnabled)
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new MenuItemData
                    {
                        Id = s.Id,
                        Name = s.DisplayName,
                        Url = s.Url,
                        Icon = s.Icon
                    }).ToList()
            }));

            RefreshMenuTreeUI();
        }
        catch (Exception ex)
        {
            _logger.Error("加载菜单数据时出错", ex);
            MessageBox.Show($"加载菜单数据时出错：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 刷新菜单树UI
    /// </summary>
    private void RefreshMenuTreeUI()
    {
        if (MenuTree is null)
        {
            return;
        }

        MenuTree.Items.Clear();

        foreach (var group in _menuGroups)
        {
            var groupItem = new TreeViewItem
            {
                Header = group.GroupName,
                IsExpanded = group.IsDefaultExpanded
            };

            foreach (var item in group.Items)
            {
                var stackPanel = CreateMenuItemPanel(item);
                var subItem = new TreeViewItem { Header = stackPanel, Tag = item };
                groupItem.Items.Add(subItem);
            }

            MenuTree.Items.Add(groupItem);
        }

        _logger.Debug($"UI 更新完成，共添加 {MenuTree.Items.Count} 个分组");
    }

    /// <summary>
    /// 创建菜单项面板
    /// </summary>
    /// <param name="item">菜单项数据</param>
    /// <returns>菜单项面板</returns>
    private StackPanel CreateMenuItemPanel(MenuItemData item)
    {
        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

        if (!string.IsNullOrEmpty(item.Icon))
        {
            var image = CreateMenuItemIcon(item.Icon);
            if (image is not null)
            {
                stackPanel.Children.Add(image);
            }
        }

        var textBlock = new TextBlock
        {
            Text = item.Name,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"))
        };

        stackPanel.Children.Add(textBlock);
        return stackPanel;
    }

    /// <summary>
    /// 创建菜单项图标
    /// </summary>
    /// <param name="iconPath">图标路径</param>
    /// <returns>图像控件，如果创建失败则返回null</returns>
    private Image? CreateMenuItemIcon(string iconPath)
    {
        try
        {
            var image = new Image
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 5, 0)
            };

            var bitmap = Utils.ImageCache.GetImage(iconPath);
            if (bitmap is not null)
            {
                image.Source = bitmap;
                return image;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("加载图标时出错", ex);
        }

        return null;
    }

    /// <summary>
    /// 初始化左侧菜单树，支持可选过滤
    /// </summary>
    /// <param name="filter">过滤条件</param>
    private void InitMenuTree(string filter = "")
    {
        MenuTree.Items.Clear();

        foreach (var group in _menuGroups)
        {
            var filteredItems = string.IsNullOrWhiteSpace(filter)
                ? group.Items
                : group.Items.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filteredItems.Count == 0 && !string.IsNullOrWhiteSpace(filter))
            {
                continue;
            }

            var groupItem = new TreeViewItem
            {
                Header = group.GroupName,
                IsExpanded = group.IsDefaultExpanded
            };

            foreach (var item in filteredItems)
            {
                var stackPanel = CreateMenuItemPanel(item);
                var subItem = new TreeViewItem { Header = stackPanel, Tag = item };
                groupItem.Items.Add(subItem);
            }

            MenuTree.Items.Add(groupItem);
        }
    }

    /// <summary>
    /// 刷新主窗口菜单，可以被设置窗口直接调用
    /// </summary>
    public void RefreshMenuFromSettings()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _logger.Debug("[MainWindow.RefreshMenuFromSettings] 开始重新加载菜单");
                LoadMenuGroupsFromDb();
                MenuTree.Items.Refresh();
                _logger.Debug("[MainWindow.RefreshMenuFromSettings] 菜单刷新完成");
            }
            catch (Exception ex)
            {
                _logger.Error("[MainWindow.RefreshMenuFromSettings] 刷新菜单时出错", ex);
                MessageBox.Show($"刷新菜单时出错：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }), DispatcherPriority.Render);
    }

    #endregion

    #region 搜索功能

    /// <summary>
    /// 搜索框文本变更事件处理器
    /// </summary>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) =>
        InitMenuTree(SearchBox.Text.Trim());

    #endregion

    #region 菜单事件处理

    /// <summary>
    /// 菜单项选择变更事件处理器
    /// </summary>
    private void MenuTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (MenuTree.SelectedItem is not TreeViewItem selected || selected.Tag is not MenuItemData data)
        {
            return;
        }

        var parentItem = selected.Parent as TreeViewItem;
        var parentName = parentItem?.Header?.ToString() ?? "";
        var combinedTitle = string.IsNullOrEmpty(parentName) ? data.Name : $"{parentName}-{data.Name}";

        OpenUrlInTab(combinedTitle, data.Url, true, false, data.Id, data.Name);
    }

    /// <summary>
    /// TreeView双击事件处理器，实现一级菜单的展开/收缩
    /// </summary>
    private void MenuTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _logger.Debug("[双击事件] MouseDoubleClick 被触发");

        var clickedItem = FindClickedTreeViewItem(e.OriginalSource as DependencyObject);
        if (clickedItem is null)
        {
            _logger.Debug("[双击事件] 未找到TreeViewItem");
            return;
        }

        var isTopLevel = IsTopLevelTreeViewItem(clickedItem);
        _logger.Debug($"[双击检测] 项目: {clickedItem.Header}, 是否顶级: {isTopLevel}");

        if (isTopLevel)
        {
            var currentState = clickedItem.IsExpanded;
            _logger.Debug($"[一级菜单双击] {clickedItem.Header} - 当前展开状态: {currentState}");

            clickedItem.IsExpanded = !currentState;
            _logger.Debug($"[一级菜单双击] {clickedItem.Header} - 新展开状态: {clickedItem.IsExpanded}");

            e.Handled = true;
        }
        else
        {
            _logger.Debug("[二级菜单双击] 忽略处理");
        }
    }

    /// <summary>
    /// 查找被点击的TreeViewItem
    /// </summary>
    /// <param name="hitTest">命中测试对象</param>
    /// <returns>TreeViewItem或null</returns>
    private static TreeViewItem? FindClickedTreeViewItem(DependencyObject? hitTest)
    {
        while (hitTest is not null)
        {
            if (hitTest is TreeViewItem treeViewItem)
            {
                return treeViewItem;
            }
            hitTest = VisualTreeHelper.GetParent(hitTest);
        }
        return null;
    }

    /// <summary>
    /// 判断是否为顶级TreeViewItem
    /// </summary>
    /// <param name="item">TreeViewItem</param>
    /// <returns>是否为顶级项</returns>
    private bool IsTopLevelTreeViewItem(TreeViewItem item)
    {
        var parent = VisualTreeHelper.GetParent(item);
        while (parent is not null)
        {
            if (parent == MenuTree)
            {
                return true;
            }
            if (parent is TreeViewItem)
            {
                return false;
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
        return false;
    }

    #endregion

    #region 标签页管理

    /// <summary>
    /// 在标签页中打开网址
    /// </summary>
    /// <param name="title">标签页标题</param>
    /// <param name="url">要打开的URL</param>
    /// <param name="keepOriginalTitle">是否保持原始标题</param>
    /// <param name="forceReload">是否强制重新加载页面</param>
    /// <param name="menuItemId">菜单项ID</param>
    /// <param name="menuItemTitle">菜单项标题</param>
    /// <param name="selectedNewTab">是否选择新标签页</param>
    /// <returns>创建的标签页对象</returns>
    public TabItem? OpenUrlInTab(string title, string url, bool keepOriginalTitle = false,
        bool forceReload = false, int menuItemId = 0, string? menuItemTitle = null, bool selectedNewTab = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.Warn("OpenUrlInTab调用失败：URL为空");
                return null;
            }

            title = string.IsNullOrWhiteSpace(title) ? "新标签页" : title;
            url = NormalizeUrl(url);

            _logger.Debug($"开始处理OpenUrlInTab - 标题: {title}, URL: {url}, 菜单ID: {menuItemId}, 强制重载: {forceReload}");

            var existingTab = FindExistingTab(menuItemId, url);
            if (existingTab is not null)
            {
                return HandleExistingTab(existingTab, url, title, forceReload, selectedNewTab, menuItemId);
            }

            _logger.Info($"创建新标签页 - 标题: {title}, URL: {url}, 菜单ID: {menuItemId}");
            return CreateNewTab(title, url, keepOriginalTitle, menuItemId, menuItemTitle, selectedNewTab);
        }
        catch (Exception ex)
        {
            _logger.Error("OpenUrlInTab发生异常", ex);
            MessageBox.Show($"打开标签页时发生错误: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    /// <summary>
    /// 查找已存在的标签页
    /// </summary>
    /// <param name="menuItemId">菜单项ID</param>
    /// <param name="url">URL</param>
    /// <returns>已存在的标签页或null</returns>
    private TabItem? FindExistingTab(int menuItemId, string url)
    {
        foreach (TabItem tab in MainTabControl.Items)
        {
            if (tab.Tag is not TabInfo tabInfo)
            {
                continue;
            }

            if (menuItemId > 0 && tabInfo.MenuItemId == menuItemId)
            {
                return tab;
            }

            if (menuItemId == 0 && tabInfo.OriginalUrl == url)
            {
                return tab;
            }
        }
        return null;
    }

    /// <summary>
    /// 处理已存在的标签页
    /// </summary>
    /// <param name="tab">已存在的标签页</param>
    /// <param name="url">URL</param>
    /// <param name="title">标题</param>
    /// <param name="forceReload">是否强制重载</param>
    /// <param name="selectedNewTab">是否选择新标签页</param>
    /// <param name="menuItemId">菜单项ID</param>
    /// <returns>处理后的标签页</returns>
    private TabItem HandleExistingTab(TabItem tab, string url, string title, bool forceReload,
        bool selectedNewTab, int menuItemId)
    {
        if (selectedNewTab)
        {
            MainTabControl.SelectedItem = tab;
        }

        if (tab.Tag is TabInfo tabInfo)
        {
            var urlChanged = tabInfo.OriginalUrl != url;
            var needReload = forceReload || urlChanged;

            if (needReload && tab.Content is ChromiumWebBrowser browser)
            {
                tabInfo.Url = url;
                tabInfo.OriginalUrl = url;
                LoadUrlSafely(browser, url);
                _logger.Info($"标签页重新加载 - {title} - {url} (菜单ID: {menuItemId}, URL变化: {urlChanged})");
            }
            else
            {
                _logger.Debug($"标签页切换 - {title} - {url} (菜单ID: {menuItemId})");
            }
        }

        return tab;
    }

    /// <summary>
    /// 标准化URL格式
    /// </summary>
    /// <param name="url">原始URL</param>
    /// <returns>标准化后的URL</returns>
    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        url = url.Trim();

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        return url;
    }

    /// <summary>
    /// 安全地加载URL到浏览器
    /// </summary>
    /// <param name="browser">浏览器实例</param>
    /// <param name="url">要加载的URL</param>
    private void LoadUrlSafely(ChromiumWebBrowser browser, string url)
    {
        try
        {
            if (browser.IsBrowserInitialized)
            {
                browser.Load(url);
                _logger.Debug($"直接加载URL: {url}");
            }
            else
            {
                DependencyPropertyChangedEventHandler? browserInitializedHandler = null;
                browserInitializedHandler = (sender, args) =>
                {
                    try
                    {
                        if (browser.IsBrowserInitialized)
                        {
                            browser.IsBrowserInitializedChanged -= browserInitializedHandler;
                            browser.Load(url);
                            _logger.Debug($"浏览器初始化完成后加载URL: {url}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("延迟加载时发生异常", ex);
                    }
                };
                browser.IsBrowserInitializedChanged += browserInitializedHandler;
            }
        }                       
        catch (Exception ex)
        {
            _logger.Error("加载URL时发生异常", ex);
            throw;
        }
    }

    /// <summary>
    /// 确保浏览器正确加载URL
    /// </summary>
    /// <param name="browser">浏览器实例</param>
    /// <param name="url">URL</param>
    private void EnsureBrowserLoadsUrl(ChromiumWebBrowser browser, string url)
    {
        try
        {
            if (!browser.IsBrowserInitialized)
            {
                DependencyPropertyChangedEventHandler? browserInitializedHandler = null;
                browserInitializedHandler = (sender, args) =>
                {
                    try
                    {
                        if (browser.IsBrowserInitialized)
                        {
                            browser.IsBrowserInitializedChanged -= browserInitializedHandler;
                            browser.Load(url);
                            _logger.Debug($"浏览器初始化完成后加载URL: {url}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("延迟加载时发生异常", ex);
                    }
                };
                browser.IsBrowserInitializedChanged += browserInitializedHandler;
            }
            else
            {
                browser.Load(url);
                _logger.Debug($"直接加载URL: {url}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("确保浏览器加载URL时发生异常", ex);
        }
    }

    /// <summary>
    /// 创建新标签页
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="url">URL</param>
    /// <param name="keepOriginalTitle">是否保持原始标题</param>
    /// <param name="menuItemId">菜单项ID</param>
    /// <param name="menuItemTitle">菜单项标题</param>
    /// <param name="selectedNewTab">是否选择新标签页</param>
    /// <returns>新创建的标签页</returns>
    private TabItem CreateNewTab(string title, string url, bool keepOriginalTitle,
        int menuItemId, string? menuItemTitle, bool selectedNewTab = true)
    {
        try
        {
            _logger.Debug($"创建新标签页 - URL: {url}");

            var tabId = Guid.NewGuid().ToString();
            var newBrowser = Browser.BrowserInstanceManager.Instance.GetBrowser("", tabId);

            EnsureBrowserLoadsUrl(newBrowser, url);

            var browserContext = new BrowserContext { IsLoading = true };
            var newTabInfo = new TabInfo
            {
                Url = url,
                TabId = tabId,
                OriginalUrl = url,
                MenuItemId = menuItemId,
                MenuItemTitle = menuItemTitle ?? string.Empty
            };

            var tabItem = new TabItem
            {
                Header = title,
                Tag = newTabInfo,
                Content = newBrowser,
                DataContext = browserContext
            };

            SetupBrowserEventHandlers(newBrowser, tabId, keepOriginalTitle, browserContext);
            SetupAutoLogin(newBrowser, url);
            SetupTabUnloadHandler(tabItem, newBrowser);

            MainTabControl.Items.Add(tabItem);
            if (selectedNewTab)
            {
                MainTabControl.SelectedItem = tabItem;
            }

            _logger.Debug($"新标签页创建完成 - 标题: {title}, URL: {url}");
            return tabItem;
        }
        catch (Exception ex)
        {
            _logger.Error("创建新标签页时发生异常", ex);
            throw;
        }
    }

    /// <summary>
    /// 设置浏览器事件处理器
    /// </summary>
    /// <param name="browser">浏览器实例</param>
    /// <param name="tabId">标签页ID</param>
    /// <param name="keepOriginalTitle">是否保持原始标题</param>
    /// <param name="browserContext">浏览器上下文</param>
    private void SetupBrowserEventHandlers(ChromiumWebBrowser browser, string tabId,
        bool keepOriginalTitle, BrowserContext browserContext)
    {
        EventHandler<FrameLoadEndEventArgs>? titleUpdateHandler = null;
        titleUpdateHandler = (sender, args) =>
        {
            if (keepOriginalTitle || !args.Frame.IsMain)
            {
                return;
            }

            args.Frame.EvaluateScriptAsync("document.title").ContinueWith(t =>
            {
                if (!t.IsFaulted && t.Result.Success && t.Result.Result is not null)
                {
                    var pageTitle = t.Result.Result.ToString();
                    if (!string.IsNullOrWhiteSpace(pageTitle))
                    {
                        UpdateTabTitle(tabId, pageTitle);
                    }
                }
            });
        };

        browser.FrameLoadEnd += titleUpdateHandler;

        EventHandler<LoadingStateChangedEventArgs>? loadingStateChangedHandler = null;
        loadingStateChangedHandler = (sender, args) =>
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (browserContext is not null)
                {
                    browserContext.IsLoading = args.IsLoading;
                }
            }, DispatcherPriority.Background);
            
            // 页面加载完成后检查是否为打卡页面
            if (!args.IsLoading)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(2000); // 等待页面稳定
                    await CheckAndEnqueueCheckInResult(browser);
                });
            }
        };

        browser.LoadingStateChanged += loadingStateChangedHandler;

        StoreBrowserEventHandlers(browser, titleUpdateHandler, loadingStateChangedHandler);
    }

    /// <summary>
    /// 更新标签页标题
    /// </summary>
    /// <param name="tabId">标签页ID</param>
    /// <param name="pageTitle">页面标题</param>
    private void UpdateTabTitle(string tabId, string pageTitle)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (TabItem tab in MainTabControl.Items)
            {
                if (tab.Tag is TabInfo info && info.TabId == tabId)
                {
                    tab.Header = pageTitle;
                    _logger.Debug("标签页标题已更新");
                    break;
                }
            }
        }));
    }

    /// <summary>
    /// 存储浏览器事件处理器引用
    /// </summary>
    /// <param name="browser">浏览器实例</param>
    /// <param name="titleUpdateHandler">标题更新处理器</param>
    /// <param name="loadingStateChangedHandler">加载状态变更处理器</param>
    private static void StoreBrowserEventHandlers(ChromiumWebBrowser browser,
        EventHandler<FrameLoadEndEventArgs> titleUpdateHandler,
        EventHandler<LoadingStateChangedEventArgs> loadingStateChangedHandler)
    {
        browser.Tag ??= new Dictionary<string, object>();

        if (browser.Tag is Dictionary<string, object> browserTags)
        {
            browserTags["titleUpdateHandler"] = titleUpdateHandler;
            browserTags["loadingStateChangedHandler"] = loadingStateChangedHandler;
        }
    }

    /// <summary>
    /// 设置自动登录
    /// </summary>
    /// <param name="browser">浏览器实例</param>
    /// <param name="url">URL</param>
    private void SetupAutoLogin(ChromiumWebBrowser browser, string url)
    {
        var siteItem = FindSiteItemByUrl(url);
        if (siteItem is null || !siteItem.AutoLogin)
        {
            return;
        }

        EventHandler<FrameLoadEndEventArgs>? frameLoadEndHandler = null;
        frameLoadEndHandler = (sender, args) =>
        {
            if (args.Frame.IsMain)
            {
                ExecuteAutoLoginScript(args.Frame, siteItem);
                browser.FrameLoadEnd -= frameLoadEndHandler;
            }
        };

        browser.FrameLoadEnd += frameLoadEndHandler;
    }

    /// <summary>
    /// 设置标签页卸载事件处理器
    /// </summary>
    /// <param name="tabItem">标签页</param>
    /// <param name="browser">浏览器实例</param>
    private void SetupTabUnloadHandler(TabItem tabItem, ChromiumWebBrowser browser)
    {
        tabItem.Unloaded += (sender, e) =>
        {
            try
            {
                _logger.Debug("标签页卸载开始");

                CleanupBrowserEventHandlers(browser);

                if (sender is TabItem tab && tab.Tag is TabInfo info)
                {
                    Browser.BrowserInstanceManager.Instance.ReleaseBrowser(info.TabId);
                    _logger.Debug($"浏览器实例已释放 - TabId: {info.TabId}");
                }

                _logger.Debug("标签页卸载完成");
            }
            catch (Exception ex)
            {
                _logger.Error("标签页卸载时发生异常", ex);
            }
        };
    }

    /// <summary>
    /// 清理浏览器事件处理器
    /// </summary>
    /// <param name="browser">浏览器实例</param>
    private static void CleanupBrowserEventHandlers(ChromiumWebBrowser browser)
    {
        if (browser.Tag is not Dictionary<string, object> browserTags)
        {
            return;
        }

        if (browserTags.TryGetValue("titleUpdateHandler", out var titleHandler) &&
            titleHandler is EventHandler<FrameLoadEndEventArgs> titleUpdateHandler)
        {
            browser.FrameLoadEnd -= titleUpdateHandler;
        }

        if (browserTags.TryGetValue("loadingStateChangedHandler", out var loadingHandler) &&
            loadingHandler is EventHandler<LoadingStateChangedEventArgs> loadingStateChangedHandler)
        {
            browser.LoadingStateChanged -= loadingStateChangedHandler;
        }

        browserTags.Clear();
    }

    /// <summary>
    /// 执行自动登录脚本
    /// </summary>
    /// <param name="frame">浏览器框架</param>
    /// <param name="siteItem">站点配置项</param>
    private static void ExecuteAutoLoginScript(IFrame frame, SiteItem siteItem)
    {
        var username = siteItem.UseCommonCredentials ? siteItem.CommonUsername : siteItem.Username;
        var password = siteItem.UseCommonCredentials ? siteItem.CommonPassword : siteItem.Password;
        var usernameSelector = string.IsNullOrWhiteSpace(siteItem.UsernameSelector)
            ? "input[type=email],input[type=text],input[name*=user],input[name*=email],input[name*=login]"
            : siteItem.UsernameSelector;
        var passwordSelector = string.IsNullOrWhiteSpace(siteItem.PasswordSelector)
            ? "input[type=password]"
            : siteItem.PasswordSelector;
        var captchaSelector = siteItem.CaptchaSelector;
        var loginButtonSelector = string.IsNullOrWhiteSpace(siteItem.LoginButtonSelector)
            ? "button[type=submit],input[type=submit]"
            : siteItem.LoginButtonSelector;
        var loginPageFeature = siteItem.LoginPageFeature;
        var captchaValue = siteItem.CaptchaValue ?? "";

        if (siteItem.CaptchaMode == 1 && !string.IsNullOrWhiteSpace(siteItem.GoogleSecret))
        {
            captchaValue = BrowserTool.Utils.GoogleAuthenticator.GenerateCode(siteItem.GoogleSecret);
        }

        var featureCheck = string.IsNullOrWhiteSpace(loginPageFeature)
            ? ""
            : $"if(!document.querySelector('{loginPageFeature}')) return;";
        var captchaJs = string.IsNullOrWhiteSpace(captchaSelector)
            ? ""
            : $"var captchaInput = document.querySelector('{captchaSelector}'); if(captchaInput) captchaInput.value = '{captchaValue.Replace("'", "\\'")}';";

        var js = $@"
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

    /// <summary>
    /// 根据URL查找站点配置项
    /// </summary>
    /// <param name="url">URL</param>
    /// <returns>站点配置项或null</returns>
    private static SiteItem? FindSiteItemByUrl(string url)
    {
        var allGroups = BrowserTool.Database.SiteConfig.GetAllGroups();
        return allGroups.SelectMany(group => group.Sites)
                       .FirstOrDefault(site => site.Url == url);
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
    /// <param name="tabItem">要关闭的标签页</param>
    public void CloseTab(TabItem? tabItem)
    {
        if (tabItem is not null)
        {
            MainTabControl.Items.Remove(tabItem);
            ClearTreeViewSelection();
        }
    }

    /// <summary>
    /// 关闭当前标签页
    /// </summary>
    private void CloseCurrentTab()
    {
        if (MainTabControl.SelectedItem is TabItem tab)
        {
            CloseTab(tab);
        }
    }

    #endregion

    #region 键盘事件处理

    /// <summary>
    /// 主窗口键盘事件处理器
    /// </summary>
    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            switch (e.Key)
            {
                case Key.F12:
                    HandleF12KeyPress();
                    break;

                case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                    ShowSearchBar();
                    e.Handled = true;
                    break;

                case Key.W when Keyboard.Modifiers == ModifierKeys.Control:
                    CloseCurrentTab();
                    e.Handled = true;
                    break;

                case Key.Escape when PageSearchBar.Visibility == Visibility.Visible:
                    HideSearchBar();
                    e.Handled = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("键盘事件处理错误", ex);
        }
    }

    /// <summary>
    /// 处理F12按键（开发者工具）
    /// </summary>
    private void HandleF12KeyPress()
    {
        if (MainTabControl.SelectedItem is TabItem tab && tab.Content is ChromiumWebBrowser currentBrowser)
        {
            CefHelper.ShowDevTools(currentBrowser);
        }
    }

    #endregion

    #region 搜索栏功能

    /// <summary>
    /// 显示搜索栏
    /// </summary>
    private void ShowSearchBar()
    {
        try
        {
            var currentTab = MainTabControl.SelectedItem as TabItem;
            if (currentTab?.Content is ChromiumWebBrowser browser)
            {
                PageSearchBar.SetBrowser(browser);
                PageSearchBar.Visibility = Visibility.Visible;
                PageSearchBar.FocusSearchBox();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("显示搜索栏错误", ex);
        }
    }

    /// <summary>
    /// 隐藏搜索栏
    /// </summary>
    private void HideSearchBar()
    {
        try
        {
            PageSearchBar.Visibility = Visibility.Collapsed;
            PageSearchBar.Clear();

            var currentTab = MainTabControl.SelectedItem as TabItem;
            if (currentTab?.Content is ChromiumWebBrowser browser)
            {
                browser.Focus();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("隐藏搜索栏错误", ex);
        }
    }

    /// <summary>
    /// 搜索栏关闭请求事件处理器
    /// </summary>
    private void PageSearchBar_CloseRequested(object sender, EventArgs e) => HideSearchBar();

    #endregion

    #region UI控件事件处理

    /// <summary>
    /// 测试环境按钮点击事件处理器
    /// </summary>
    private void TestEnvButton_Click(object sender, RoutedEventArgs e)
    {
        var testUrl = System.Configuration.ConfigurationManager.AppSettings["TestEnvironmentUrl"];

        if (!string.IsNullOrEmpty(testUrl))
        {
            OpenUrlInTab("测试环境", testUrl, true);
        }
        else
        {
            MessageBox.Show("测试环境URL未配置，请在App.config中设置TestEnvironmentUrl", "配置错误",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// 手动打卡按钮点击事件处理器
    /// </summary>
    private async void ManualCheckInButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.Debug("手动打卡按钮被点击");

           await App.GetAutoCheckInSimulator().ExecuteManualCheckIn();
        }
        catch (Exception ex)
        {
            _logger.Error("手动打卡时发生异常", ex);
            MessageBox.Show($"手动打卡失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 抽屉开关按钮点击事件处理器
    /// </summary>
    private void DrawerToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        ToggleDrawer();
        NotifyBrowserLayoutChange();
    }

    /// <summary>
    /// 切换抽屉状态
    /// </summary>
    private void ToggleDrawer()
    {
        if (_isDrawerOpen)
        {
            DrawerCol.Width = new GridLength(0);
            DrawerBorder.Visibility = Visibility.Collapsed;
        }
        else
        {
            DrawerCol.Width = new GridLength(200);
            DrawerBorder.Visibility = Visibility.Visible;
        }
        _isDrawerOpen = !_isDrawerOpen;
    }

    /// <summary>
    /// 通知浏览器布局变化
    /// </summary>
    private void NotifyBrowserLayoutChange()
    {
        try
        {
            if (MainTabControl?.Items.Count == 0)
            {
                return;
            }

            var selectedItem = MainTabControl.SelectedItem as TabItem;
            if (selectedItem is null || !MainTabControl.Items.Contains(selectedItem))
            {
                if (MainTabControl.Items.Count > 0)
                {
                    MainTabControl.SelectedIndex = 0;
                }
                return;
            }

            if (selectedItem.Content is ChromiumWebBrowser browser && !browser.IsDisposed)
            {
                var browserInstance = browser.GetBrowser();
                if (browserInstance is not null && !browserInstance.IsDisposed)
                {
                    var host = browserInstance.GetHost();
                    if (host is not null && !host.IsDisposed)
                    {
                        host.NotifyMoveOrResizeStarted();
                        host.NotifyScreenInfoChanged();
                        host.WasResized();
                    }
                }
            }
        }
        catch (ObjectDisposedException ex)
        {
            _logger.Error("浏览器对象已释放", ex);
        }
        catch (Exception ex)
        {
            _logger.Error("通知浏览器布局变化时出错", ex);
        }
    }

    /// <summary>
    /// 后退按钮点击事件处理器
    /// </summary>
    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is TabItem tabItem &&
            tabItem.Content is ChromiumWebBrowser browser && browser.CanGoBack)
        {
            browser.Back();
        }
    }

    /// <summary>
    /// 前进按钮点击事件处理器
    /// </summary>
    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is TabItem tabItem &&
            tabItem.Content is ChromiumWebBrowser browser && browser.CanGoForward)
        {
            browser.Forward();
        }
    }

    /// <summary>
    /// 刷新按钮点击事件处理器
    /// </summary>
    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is TabItem tabItem &&
            tabItem.Content is ChromiumWebBrowser browser)
        {
            browser.Reload();
        }
    }

    /// <summary>
    /// URL输入框键盘事件处理器
    /// </summary>
    private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || MainTabControl.SelectedItem is not TabItem tabItem ||
            tabItem.Content is not ChromiumWebBrowser browser)
        {
            return;
        }

        var url = UrlTextBox.Text;
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = "https://" + url;
        }
        browser.LoadUrl(url);
    }

    /// <summary>
    /// 开发者工具按钮点击事件处理器
    /// </summary>
    private void DevToolsButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is not TabItem tabItem ||
            tabItem.Content is not ChromiumWebBrowser browser)
        {
            return;
        }

        var url = UrlTextBox.Text;
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            browser.LoadUrl(url);
        }
    }

    /// <summary>
    /// 切换地址栏的显示/隐藏状态
    /// </summary>
    public void ToggleUrlBar()
    {
        if (this.FindName("UrlBar") is not Grid urlBar)
        {
            return;
        }

        urlBar.Visibility = urlBar.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (urlBar.Visibility == Visibility.Visible &&
            MainTabControl.SelectedItem is TabItem tabItem &&
            tabItem.Content is ChromiumWebBrowser browser)
        {
            UrlTextBox.Text = browser.Address;
        }
    }

    /// <summary>
    /// 标签页选择变更事件处理器
    /// </summary>
    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            ClearTreeViewSelection();

            if (PageSearchBar.Visibility == Visibility.Visible)
            {
                var currentTab = MainTabControl.SelectedItem as TabItem;
                if (currentTab?.Content is ChromiumWebBrowser browser)
                {
                    PageSearchBar.SetBrowser(browser);
                }
                else
                {
                    HideSearchBar();
                }
            }

            if (MainTabControl.SelectedItem is TabItem tabItem &&
                tabItem.Content is ChromiumWebBrowser selectedBrowser)
            {
                UrlTextBox.Text = selectedBrowser.Address;
            }
        }
        catch (Exception ex)
        {
            _logger.Error("标签页切换错误", ex);
        }
    }

    #endregion

    #region 右键菜单处理

    /// <summary>
    /// 标签页右键按下事件处理器
    /// </summary>
    private void MainTabControl_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var tabItem = GetTabItemAtPosition(e.GetPosition(MainTabControl));
        if (tabItem is not null)
        {
            MainTabControl.SelectedItem = tabItem;
        }
    }

    /// <summary>
    /// 获取指定位置的标签页
    /// </summary>
    /// <param name="position">位置</param>
    /// <returns>标签页或null</returns>
    private TabItem? GetTabItemAtPosition(Point position)
    {
        var result = VisualTreeHelper.HitTest(MainTabControl, position);
        if (result is null)
        {
            return null;
        }

        var element = result.VisualHit;
        while (element is not null && element is not TabItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }
        return element as TabItem;
    }

    /// <summary>
    /// 关闭标签页菜单项点击事件处理器
    /// </summary>
    private void CloseTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is not TabItem tabItem)
        {
            return;
        }

        if (tabItem.Tag is TabInfo tabInfo)
        {
            Browser.BrowserInstanceManager.Instance.ReleaseBrowser(tabInfo.TabId);
        }

        MainTabControl.Items.Remove(tabItem);
        ClearTreeViewSelection();
    }

    /// <summary>
    /// 关闭其他标签页菜单项点击事件处理器
    /// </summary>
    private void CloseOtherTabsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is not TabItem selectedTab)
        {
            return;
        }

        var tabsToRemove = MainTabControl.Items.Cast<TabItem>()
            .Where(tab => tab != selectedTab)
            .ToList();

        foreach (var tab in tabsToRemove)
        {
            MainTabControl.Items.Remove(tab);
        }
    }

    /// <summary>
    /// 关闭右侧标签页菜单项点击事件处理器
    /// </summary>
    private void CloseRightTabsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is not TabItem selectedTab)
        {
            return;
        }

        var selectedIndex = MainTabControl.Items.IndexOf(selectedTab);
        var tabsToRemove = MainTabControl.Items.Cast<TabItem>()
            .Where((tab, index) => index > selectedIndex)
            .ToList();

        foreach (var tab in tabsToRemove)
        {
            MainTabControl.Items.Remove(tab);
        }
    }

    /// <summary>
    /// 刷新标签页菜单项点击事件处理器
    /// </summary>
    private void RefreshTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is TabItem tabItem &&
            tabItem.Content is ChromiumWebBrowser browser)
        {
            browser.Reload();
        }
    }

    /// <summary>
    /// 复制URL菜单项点击事件处理器
    /// </summary>
    private void CopyUrlMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is not TabItem tabItem)
        {
            return;
        }

        var textToCopy = tabItem.Content switch
        {
            ChromiumWebBrowser browser => browser.Address,
            _ when tabItem.Tag is TabInfo tabInfo => tabInfo.Url,
            _ => null
        };

        if (!string.IsNullOrEmpty(textToCopy))
        {
            Clipboard.SetText(textToCopy);
        }
    }

    /// <summary>
    /// 复制标题菜单项点击事件处理器
    /// </summary>
    private void CopyTitleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is TabItem tabItem)
        {
            Clipboard.SetText(tabItem.Header.ToString());
        }
    }

    /// <summary>
    /// 复制选中内容菜单项点击事件处理器
    /// </summary>
    private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is TabItem tabItem &&
            tabItem.Content is ChromiumWebBrowser browser)
        {
            browser.Copy();
        }
    }

    /// <summary>
    /// 粘贴菜单项点击事件处理器
    /// </summary>
    private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is TabItem tabItem &&
            tabItem.Content is ChromiumWebBrowser browser)
        {
            browser.Paste();
        }
    }

    /// <summary>
    /// 粘贴并访问菜单项点击事件处理器
    /// </summary>
    private void PasteAndGoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is not TabItem tabItem ||
            tabItem.Content is not ChromiumWebBrowser browser)
        {
            return;
        }

        var url = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = "https://" + url;
        }
        browser.LoadUrl(url);
    }

    /// <summary>
    /// 复制标签页菜单项点击事件处理器
    /// </summary>
    private void DuplicateTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is not TabItem tabItem)
        {
            return;
        }

        var (baseTitle, url) = GetTabDuplicationInfo(tabItem);
        var newTitle = GenerateUniqueTabTitle(baseTitle);

        var newBrowser = new ChromiumWebBrowser(url)
        {
            DownloadHandler = new CefDownloadHandler(),
            MenuHandler = new CefMenuHandler(),
            LifeSpanHandler = new Browser.CefLifeSpanHandler()
        };

        var newTabItem = new TabItem
        {
            Header = newTitle,
            Tag = GetTabTag(tabItem, url),
            Content = newBrowser
        };

        MainTabControl.Items.Add(newTabItem);
        MainTabControl.SelectedItem = newTabItem;
    }

    /// <summary>
    /// 获取标签页复制信息
    /// </summary>
    /// <param name="tabItem">标签页</param>
    /// <returns>基础标题和URL</returns>
    private static (string baseTitle, string url) GetTabDuplicationInfo(TabItem tabItem)
    {
        var baseTitle = Regex.Replace(tabItem.Header.ToString(), @"\d+$", "").Trim();

        var url = tabItem.Content switch
        {
            ChromiumWebBrowser browser => browser.Address,
            _ when tabItem.Tag is TabInfo tabInfo => tabInfo.Url,
            _ => string.Empty
        };

        return (baseTitle, url);
    }

    /// <summary>
    /// 生成唯一的标签页标题
    /// </summary>
    /// <param name="baseTitle">基础标题</param>
    /// <returns>唯一标题</returns>
    private string GenerateUniqueTabTitle(string baseTitle)
    {
        var existingTabs = MainTabControl.Items.Cast<TabItem>()
            .Where(t => t.Header.ToString().StartsWith(baseTitle))
            .ToList();

        var newNumber = 1;
        if (existingTabs.Any())
        {
            var numbers = existingTabs
                .Select(t => t.Header.ToString())
                .Select(title => Regex.Match(title, @"\d+$"))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Value))
                .ToList();

            if (numbers.Any())
            {
                newNumber = numbers.Max() + 1;
            }
        }

        return $"{baseTitle}{newNumber}";
    }

    /// <summary>
    /// 获取标签页标记
    /// </summary>
    /// <param name="originalTab">原始标签页</param>
    /// <param name="url">URL</param>
    /// <returns>标签页标记</returns>
    private static object GetTabTag(TabItem originalTab, string url)
    {
        return originalTab.Tag switch
        {
            TabInfo tabInfo => tabInfo,
            _ => url
        };
    }

    /// <summary>
    /// 在新标签页中打开菜单项点击事件处理器
    /// </summary>
    private void OpenInNewTabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabControl.SelectedItem is not TabItem tabItem)
        {
            return;
        }

        var (title, url) = GetTabOpenInfo(tabItem);
        if (!string.IsNullOrEmpty(url))
        {
            OpenUrlInTab(title, url);
        }
    }

    /// <summary>
    /// 获取标签页打开信息
    /// </summary>
    /// <param name="tabItem">标签页</param>
    /// <returns>标题和URL</returns>
    private static (string title, string url) GetTabOpenInfo(TabItem tabItem)
    {
        var title = tabItem.Header.ToString();

        var url = tabItem.Content switch
        {
            ChromiumWebBrowser browser => browser.Address,
            _ when tabItem.Tag is TabInfo tabInfo => tabInfo.Url,
            _ => string.Empty
        };

        return (title, url);
    }

    #endregion

    #region 窗口控制

    /// <summary>
    /// 最小化按钮点击事件处理器
    /// </summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    /// <summary>
    /// 最小化按钮点击事件处理器（备用）
    /// </summary>
    private void btnMinimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    /// <summary>
    /// 最大化按钮点击事件处理器
    /// </summary>
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
            MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
        }
    }

    /// <summary>
    /// 关闭按钮点击事件处理器
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 标题栏鼠标左键按下事件处理器 - 支持拖拽和双击最大化/还原
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>
    /// 窗口关闭事件处理 - 最小化到托盘而不是退出
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();

        if (Application.Current.Resources["TrayIcon"] is TaskbarIcon trayIcon)
        {
            trayIcon.ShowBalloonTip("Browser Tool", "程序已最小化到系统托盘", BalloonIcon.Info);
        }
    }

    /// <summary>
    /// 主窗体大小改变事件处理器
    /// </summary>
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                foreach (TabItem tab in MainTabControl.Items)
                {
                    if (tab.Content is not ChromiumWebBrowser browser)
                    {
                        continue;
                    }

                    browser.InvalidateVisual();
                    browser.UpdateLayout();

                    if (!browser.IsLoading && browser.CanExecuteJavascriptInMainFrame)
                    {
                        browser.ExecuteScriptAsync("if(window.dispatchEvent) { window.dispatchEvent(new Event('resize')); }");
                    }
                }

                _logger.Debug($"[窗口大小改变] 新尺寸: {e.NewSize.Width}x{e.NewSize.Height}");
            }
            catch (Exception ex)
            {
                _logger.Error("窗口大小改变处理错误", ex);
            }
        }), DispatcherPriority.Background);
    }

    /// <summary>
    /// 关闭标签页按钮点击事件处理器
    /// </summary>
    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button || button.Tag is not TabItem tabItem ||
                !MainTabControl.Items.Contains(tabItem))
            {
                return;
            }

            if (MainTabControl.SelectedItem == tabItem && MainTabControl.Items.Count > 1)
            {
                var index = MainTabControl.Items.IndexOf(tabItem);
                MainTabControl.SelectedIndex = index > 0 ? index - 1 : index + 1;
            }

            if (tabItem.Content is ChromiumWebBrowser browser)
            {
                browser.Dispose();
            }

            MainTabControl.Items.Remove(tabItem);
            ClearTreeViewSelection();
        }
        catch (Exception ex)
        {
            _logger.Error("关闭标签页错误", ex);
        }
    }

    #endregion

    #region 设置相关

    /// <summary>
    /// 设置按钮点击事件处理器
    /// </summary>
    private void btnSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(this);

        _logger.Debug("[MainWindow.btnSettings_Click] 准备订阅 SettingsSaved 事件");
        settingsWindow.SettingsSaved += SettingsWindow_SettingsSaved;

        settingsWindow.ShowDialog();

        _logger.Debug("[MainWindow.btnSettings_Click] 设置窗口已关闭，准备刷新菜单");
        RefreshMenuFromSettings();
    }

    /// <summary>
    /// 设置保存事件处理器
    /// </summary>
    private void SettingsWindow_SettingsSaved(object sender, EventArgs args)
    {
        _logger.Debug("[MainWindow] SettingsSaved 事件被触发，准备刷新菜单");

        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _logger.Debug("[MainWindow] 开始重新加载菜单");
                LoadMenuGroupsFromDb();
                MenuTree.Items.Refresh();
                _logger.Debug("[MainWindow] 菜单刷新完成");
            }
            catch (Exception ex)
            {
                _logger.Error("[MainWindow] 刷新菜单时出错", ex);
                MessageBox.Show($"刷新菜单时出错：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }), DispatcherPriority.Render);
    }

    #endregion

    #region 上下文菜单位置回调

    /// <summary>
    /// 上下文菜单位置回调
    /// </summary>
    /// <param name="popupSize">弹出框大小</param>
    /// <param name="targetSize">目标大小</param>
    /// <param name="offset">偏移量</param>
    /// <returns>自定义弹出框位置数组</returns>
    private CustomPopupPlacement[] ContextMenu_PlacementCallback(Size popupSize, Size targetSize, Point offset)
    {
        var mousePosition = Mouse.GetPosition(MainTabControl);
        mousePosition = MainTabControl.PointToScreen(mousePosition);

        var placementPoint = new Point(mousePosition.X, mousePosition.Y);

        if (_isDrawerOpen)
        {
            placementPoint.X -= DrawerCol.Width.Value;
        }

        return new[]
        {
            new CustomPopupPlacement(placementPoint, PopupPrimaryAxis.Horizontal)
        };
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取当前标签页的浏览器控件
    /// </summary>
    /// <returns>当前浏览器控件或null</returns>
    private ChromiumWebBrowser? GetCurrentBrowser()
    {
        if (MainTabControl.SelectedItem is not TabItem selectedTab)
        {
            return null;
        }

        return selectedTab.Content switch
        {
            ChromiumWebBrowser browser => browser,
            ContentPresenter contentPresenter when contentPresenter.Content is Grid grid =>
                grid.Children.OfType<ChromiumWebBrowser>().FirstOrDefault(),
            _ => null
        };
    }

    /// <summary>
    /// 更新URL输入框内容
    /// </summary>
    /// <param name="url">新的URL</param>
    public void UpdateUrl(string url)
    {
        if (MainTabControl.SelectedItem is not TabItem selectedTab)
        {
            return;
        }

        switch (selectedTab.Content)
        {
            case ContentPresenter contentPresenter when contentPresenter.Content is Grid grid:
                {
                    var urlTextBox = grid.FindName("UrlTextBox") as TextBox;
                    if (urlTextBox is not null)
                    {
                        urlTextBox.Text = url;
                    }
                    break;
                }
        }
    }

    #endregion

    #region 自动打卡相关

    /// <summary>
    /// 检查页面并将打卡结果加入队列
    /// </summary>
    /// <param name="browser">浏览器控件</param>
    private async Task CheckAndEnqueueCheckInResult(ChromiumWebBrowser browser)
    {
        try
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                string currentUrl = browser.Address ?? "";
                _logger.Debug($"检查页面打卡结果: {currentUrl}");

                // 检查域名是否为打卡相关域名
                bool isDomainMatch = _checkInDomains.Any(domain => currentUrl.Contains(domain));
                if (!isDomainMatch)
                {
                    _logger.Debug($"当前域名不在打卡域名列表中: {currentUrl}");
                    return;
                }

                _logger.Debug("域名匹配，开始检查页面内容");

                // 等待页面稳定（给页面一些时间完成加载和渲染）
                await Task.Delay(2000);

                // 再次检查浏览器状态
                if (browser.IsDisposed)
                {
                    _logger.Debug("浏览器已被释放，跳过检查");
                    return;
                }

                // 检查页面内容是否包含成功标识
                bool hasSuccessContent = await CheckPageContentForSuccess(browser);

                // 创建检查结果并加入队列
                var result = new CheckInResult
                {
                    IsSuccess = hasSuccessContent,
                    Url = currentUrl,
                    CheckTime = DateTime.Now,
                    Message = hasSuccessContent ? "检测到打卡成功" : "未检测到打卡成功标识"
                };

                CheckInResultQueue.Instance.EnqueueResult(result);
                _logger.Debug($"打卡结果已加入队列: 成功={hasSuccessContent}, URL={currentUrl}");
            });
        }
        catch (Exception ex)
        {
            _logger.Error("检查并入队打卡结果时发生异常", ex);
        }
    }

    /// <summary>
    /// 检查页面内容是否包含成功标识
    /// </summary>
    /// <param name="browser">浏览器控件</param>
    /// <returns>是否包含成功标识</returns>
    private async Task<bool> CheckPageContentForSuccess(ChromiumWebBrowser browser)
    {
        try
        {
            // 首先使用简单的方法检查URL和基本信息
            string url = browser.Address ?? "";
            _logger.Debug($"当前页面URL: {url}");

            //// 先检查URL是否包含成功标识（最快的方法）
            //if (CheckUrlForSuccess(url))
            //{
            //    _logger.Debug("URL包含成功标识，直接返回成功");
            //    return true;
            //}

            // 检查浏览器状态
            if (browser.IsDisposed)
            {
                _logger.Debug("浏览器已被释放，使用URL判断");
                return CheckUrlForSuccess(url);
            }

            var cefBrowser = browser.GetBrowser();
            if (cefBrowser == null || cefBrowser.IsDisposed)
            {
                _logger.Debug("CEF浏览器实例无效，使用URL判断");
                return CheckUrlForSuccess(url);
            }

            // 检查浏览器是否正在加载
            if (browser.IsLoading)
            {
                _logger.Debug("浏览器正在加载，等待加载完成...");
                // 等待最多3秒让页面加载完成
                int waitCount = 0;
                while (browser.IsLoading && waitCount < 6)
                {
                    await Task.Delay(500);
                    waitCount++;
                }
                
                if (browser.IsLoading)
                {
                    _logger.Debug("页面仍在加载，使用URL判断");
                    return CheckUrlForSuccess(url);
                }
            }

            _logger.Debug($"浏览器状态检查通过，BrowserId: {cefBrowser.Identifier}");

            // 使用多种方法获取页面内容
            string bodyText = "";
            string titleText = "";

            // 方法1: 尝试使用扩展方法（减少超时时间到5秒）
            try
            {
                _logger.Debug("开始使用扩展方法获取页面内容...");

                var sourceTask = browser.GetSourceAsync();
                var textTask = browser.GetTextAsync();

                // 减少超时时间到5秒
                var timeoutTask = Task.Delay(5000);
                var completedTask = await Task.WhenAny(
                    Task.WhenAll(sourceTask, textTask),
                    timeoutTask
                );

                if (completedTask != timeoutTask)
                {
                    var source = await sourceTask;
                    var text = await textTask;

                    // 从HTML源码中提取标题
                    var titleMatch = System.Text.RegularExpressions.Regex.Match(source, @"<title[^>]*>([^<]*)</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    titleText = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "";

                    // 使用页面文本内容，限制长度
                    bodyText = text.Length > 1000 ? text.Substring(0, 1000) : text;

                    _logger.Debug($"扩展方法获取成功 - 标题: {titleText}, 内容长度: {bodyText.Length}");
                }
                else
                {
                    _logger.Debug("扩展方法获取超时，尝试备用方法");
                    // 继续尝试其他方法
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"扩展方法获取时发生异常: {ex.Message}");
                // 继续尝试其他方法
            }

            // 方法2: 如果扩展方法失败，尝试获取页面标题
            if (string.IsNullOrEmpty(titleText))
            {
                try
                {
                    _logger.Debug("尝试直接获取页面标题...");
                    var mainFrame = cefBrowser.MainFrame;
                    if (mainFrame != null && !mainFrame.IsDisposed)
                    {
                        // 使用主框架的URL作为备用
                        var frameUrl = mainFrame.Url ?? "";
                        if (!string.IsNullOrEmpty(frameUrl) && frameUrl != url)
                        {
                            _logger.Debug($"使用主框架URL: {frameUrl}");
                            if (CheckUrlForSuccess(frameUrl))
                            {
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"获取主框架信息时发生异常: {ex.Message}");
                }
            }

            // 方法3: 检查浏览器标题（如果可用）
            if (string.IsNullOrEmpty(titleText))
            {
                try
                {
                    // 尝试从浏览器获取标题
                    var browserHost = cefBrowser.GetHost();
                    if (browserHost != null && !browserHost.IsDisposed)
                    {
                        // 这里可以添加更多的检查逻辑
                        _logger.Debug("浏览器主机可用，但无法获取内容");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"获取浏览器主机信息时发生异常: {ex.Message}");
                }
            }

            // 如果所有方法都失败，使用URL判断
            if (string.IsNullOrEmpty(titleText) && string.IsNullOrEmpty(bodyText))
            {
                _logger.Debug("所有内容获取方法都失败，使用URL判断");
                return CheckUrlForSuccess(url);
            }

            // 检查是否包含打卡成功的关键字
            string[] successKeywords = { "打卡成功", "签到成功", "考勤成功", "上班打卡成功", "下班打卡成功", "check-in successful", "attendance successful", "签到完成", "打卡完成", "google" };
            
            bool hasSuccessKeyword = successKeywords.Any(keyword => 
                bodyText.Contains(keyword, StringComparison.OrdinalIgnoreCase) || 
                titleText.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                url.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (hasSuccessKeyword)
            {
                _logger.Debug("页面包含成功标识");
                return true;
            }

            _logger.Debug("页面不包含成功标识");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error("检查页面内容时发生异常", ex);
            return CheckUrlForSuccess(browser.Address ?? "");
        }
    }

    /// <summary>
    /// 基于URL检查是否成功（备用方法）
    /// </summary>
    /// <param name="url">页面URL</param>
    /// <returns>是否包含成功标识</returns>
    private bool CheckUrlForSuccess(string url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        string[] urlSuccessKeywords = { "success", "complete", "done", "成功", "完成", "google" };
        bool hasSuccessInUrl = urlSuccessKeywords.Any(keyword => url.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        
        _logger.Debug($"URL成功检查结果: {hasSuccessInUrl} (URL: {url})");
        return hasSuccessInUrl;
    }

    #endregion
}

