private void DevToolsButton_Click(object sender, RoutedEventArgs e)
{
    if (MainTabControl.SelectedItem is TabItem tabItem)
    {
        var browser = tabItem.Content as CefSharp.Wpf.ChromiumWebBrowser;
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

private void btnSettings_Click(object sender, RoutedEventArgs e)
{
    var settingsWindow = new SettingsWindow();
    settingsWindow.Owner = this; // 让设置窗口成为主窗口的子窗口
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
    this.Activate(); // 设置窗口关闭后激活主界面
}

private void LoadMenuGroupsFromDb()
{
    // 实现 LoadMenuGroupsFromDb 方法的逻辑
    // 这里需要根据你的数据库查询逻辑来实现
    // 这里只是一个示例，实际实现需要根据你的数据库查询逻辑来实现
    var allGroups = new List<Group>
    {
        new Group { Name = "Group 1", Sites = new List<Site> { new Site { DisplayName = "Site 1", Url = "http://example.com", Icon = "icon1.png" }, new Site { DisplayName = "Site 2", Url = "https://example.com", Icon = "icon2.png" } } },
        new Group { Name = "Group 2", Sites = new List<Site> { new Site { DisplayName = "Site 3", Url = "http://example.org", Icon = "icon3.png" }, new Site { DisplayName = "Site 4", Url = "https://example.org", Icon = "icon4.png" } } }
    };

    var menuGroups = allGroups.Select(g => new MenuGroup
    {
        GroupName = $"{g.Name} ({g.Sites.Count()})", // 统计所有网址数量
        Items = g.Sites.Select(s => new MenuItemData
        {
            Name = s.DisplayName,
            Url = s.Url,
            Icon = s.Icon
        }).ToList()
    }).ToList();

    // 将 menuGroups 添加到 MenuTree 中
    MenuTree.ItemsSource = menuGroups;
} 