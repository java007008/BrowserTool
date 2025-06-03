using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using BrowserTool.Database.Entities;
using BrowserTool.ViewModels;
using BrowserTool.Utils;
using System.Windows.Media;
using BrowserTool.Database;
using System.Configuration;

namespace BrowserTool
{
    public partial class SettingsWindow : Window
    {
        // 添加事件，用于通知主窗口设置已更改
        public event EventHandler SettingsSaved;
        
        // 保存对主窗口的引用
        private MainWindow mainWindow;
        
        // 触发设置已保存事件的辅助方法
        private void RaiseSettingsSaved()
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RaiseSettingsSaved] 准备触发 SettingsSaved 事件，有订阅者: {SettingsSaved != null}");
            SettingsSaved?.Invoke(this, EventArgs.Empty);
            System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RaiseSettingsSaved] SettingsSaved 事件已触发");
            
            // 尝试从实例变量获取主窗口引用
            var mainWindowRef = mainWindow;
            
            // 如果实例变量为空，尝试从 Application.Current.MainWindow 获取
            if (mainWindowRef == null && Application.Current != null && Application.Current.MainWindow is MainWindow)
            {
                mainWindowRef = Application.Current.MainWindow as MainWindow;
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RaiseSettingsSaved] 从 Application.Current.MainWindow 获取了 MainWindow 引用");
            }
            
            // 如果有 MainWindow 引用，则直接刷新菜单
            if (mainWindowRef != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RaiseSettingsSaved] 准备直接刷新主窗口菜单");
                mainWindowRef.Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        // 直接调用主窗口的刷新菜单方法
                        mainWindowRef.RefreshMenuFromSettings();
                        System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RaiseSettingsSaved] 主窗口菜单刷新完成");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RaiseSettingsSaved] 刷新菜单时出错：{ex}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RaiseSettingsSaved] 无法获取 MainWindow 引用，无法直接刷新菜单");
            }
        }

        private ObservableCollection<SiteGroupViewModel> groups;
        private SiteGroupViewModel selectedGroup;
        private List<SiteGroup> allGroups;
        private List<SiteItem> allSites;
        private string currentSearchText = "";
        private string currentSearchType = "全部";

        public SettingsWindow()
        {
            InitializeComponent();
            // 不要在这里调用LoadData或RefreshSites
        }
        
        public SettingsWindow(MainWindow owner) : this()
        {
            this.mainWindow = owner;
            System.Diagnostics.Debug.WriteLine("[SettingsWindow] 创建时设置了 MainWindow 引用");
        }
        
        // 直接刷新主窗口菜单的方法
        private void RefreshMainWindowMenu()
        {
            // 尝试从实例变量获取主窗口引用
            var mainWindowRef = mainWindow;
            
            // 如果实例变量为空，尝试从 Application.Current.MainWindow 获取
            if (mainWindowRef == null && Application.Current != null && Application.Current.MainWindow is MainWindow)
            {
                mainWindowRef = Application.Current.MainWindow as MainWindow;
                System.Diagnostics.Debug.WriteLine("[SettingsWindow.RefreshMainWindowMenu] 从 Application.Current.MainWindow 获取了 MainWindow 引用");
            }
            
            if (mainWindowRef != null)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsWindow.RefreshMainWindowMenu] 直接调用主窗口刷新菜单方法");
                mainWindowRef.Dispatcher.BeginInvoke(new Action(() => {
                    try
                    {
                        // 调用主窗口的刷新菜单方法
                        mainWindowRef.RefreshMenuFromSettings();
                        System.Diagnostics.Debug.WriteLine("[SettingsWindow.RefreshMainWindowMenu] 主窗口菜单刷新完成");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RefreshMainWindowMenu] 刷新菜单时出错：{ex}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[SettingsWindow.RefreshMainWindowMenu] 无法获取 MainWindow 引用，无法刷新菜单");
                // 仅触发事件，不再调用其他方法，避免死循环
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RefreshMainWindowMenu] 仅触发事件，有订阅者: {SettingsSaved != null}");
                SettingsSaved?.Invoke(this, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow.RefreshMainWindowMenu] 事件已触发");
            }
        }

        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[SettingsWindow_Loaded] 窗口加载事件开始");
            try
            {
                // 显示加载指示器
                System.Diagnostics.Debug.WriteLine("[SettingsWindow_Loaded] 准备显示Loading");
                ShowLoading();
                
                System.Diagnostics.Debug.WriteLine("[SettingsWindow_Loaded] 开始异步加载数据");
                await LoadDataAsync(); // 异步加载数据
                System.Diagnostics.Debug.WriteLine("[SettingsWindow_Loaded] 异步加载数据完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow_Loaded] 加载数据时出错：{ex.Message}");
                MessageBox.Show($"加载数据时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 隐藏加载指示器
                System.Diagnostics.Debug.WriteLine("[SettingsWindow_Loaded] 准备隐藏Loading");
                HideLoading();
                System.Diagnostics.Debug.WriteLine("[SettingsWindow_Loaded] 窗口加载事件完成");
            }
        }

        private async Task LoadDataAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[LoadDataAsync] 开始加载数据");
            System.Diagnostics.Debug.WriteLine($"[LoadDataAsync] dgSites is null? {dgSites == null}");
            
            // 在后台线程执行数据库操作
            await Task.Run(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[LoadDataAsync] 在后台线程执行数据库操作");
                // 初始化数据库
                SiteConfig.InitializeDatabase();
                
                // 加载数据
                allGroups = SiteConfig.GetAllGroups();
                allSites = SiteConfig.GetAllSites() ?? new List<SiteItem>();
                System.Diagnostics.Debug.WriteLine($"[LoadDataAsync] 数据库操作完成，加载了 {allGroups?.Count ?? 0} 个分组，{allSites?.Count ?? 0} 个站点");
            });
            
            System.Diagnostics.Debug.WriteLine($"[LoadDataAsync] 开始刷新UI");
            // 在UI线程更新界面
            RefreshGroups();
            RefreshSites();
            System.Diagnostics.Debug.WriteLine($"[LoadDataAsync] UI刷新完成");
        }

        private void RefreshGroups()
        {
            allGroups = SiteConfig.GetAllGroups();
            groups = new ObservableCollection<SiteGroupViewModel>(
                allGroups.Select(g => new SiteGroupViewModel(g))
            );
            tvGroups.ItemsSource = groups;
        }

        private void RefreshSites(string searchText = "", string searchType = "全部")
        {
            if (dgSites == null)
            {
                System.Diagnostics.Debug.WriteLine("[RefreshSites] dgSites is still null, skip binding.");
                return;
            }
            System.Diagnostics.Debug.WriteLine($"[RefreshSites] dgSites is null? {dgSites == null}");
            allSites = SiteConfig.GetAllSites() ?? new List<SiteItem>();
            var sites = selectedGroup != null
                ? (selectedGroup.Sites?.ToList() ?? new List<SiteItem>())
                : (allSites ?? new List<SiteItem>());

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                sites = sites.Where(site =>
                {
                    switch (searchType)
                    {
                        case "名称":
                            return (site.DisplayName ?? string.Empty).Contains(searchText, StringComparison.OrdinalIgnoreCase);
                        case "网址":
                            return (site.Url ?? string.Empty).Contains(searchText, StringComparison.OrdinalIgnoreCase);
                        case "描述":
                            return (site.Description ?? string.Empty).Contains(searchText, StringComparison.OrdinalIgnoreCase);
                        case "标签":
                            return site.Tags != null && site.Tags.Any(t => t.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase));
                        default:
                            return (site.DisplayName ?? string.Empty).Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                   (site.Url ?? string.Empty).Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                   (site.Description ?? string.Empty).Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                   (site.Tags != null && site.Tags.Any(t => t.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase)));
                    }
                }).ToList();
            }

            this.dgSites.ItemsSource = sites;
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            currentSearchText = txtSearch.Text;
            RefreshSites(currentSearchText, currentSearchType);
        }

        private void cmbSearchType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSearchType.SelectedItem is ComboBoxItem selectedItem)
            {
                currentSearchType = selectedItem.Content.ToString();
                RefreshSites(currentSearchText, currentSearchType);
            }
        }

        private void tvGroups_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            selectedGroup = e.NewValue as SiteGroupViewModel;
            RefreshSites(currentSearchText, currentSearchType);
        }

        private void btnAddGroup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new GroupEditDialog();
            if (dialog.ShowDialog() == true)
            {
                var newGroup = new SiteGroup
                {
                    Name = dialog.GroupName,
                    SortOrder = groups.Count,
                    IsEnabled = true,
                    IsDefaultExpanded = dialog.IsDefaultExpanded
                };

                SiteConfig.SaveGroup(newGroup);
                groups.Add(new SiteGroupViewModel(newGroup));
                // 触发设置保存事件
                RaiseSettingsSaved();
            }
        }

        private void btnAddSite_Click(object sender, RoutedEventArgs e)
        {
            if (selectedGroup == null)
            {
                MessageBox.Show("请先选择一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SiteEditDialog();
            if (dialog.ShowDialog() == true)
            {
                var newSite = new SiteItem
                {
                    GroupId = selectedGroup.Id,
                    DisplayName = dialog.DisplayName,
                    Url = dialog.Url,
                    Username = dialog.Username,
                    Password = dialog.Password,
                    CommonUsername = dialog.CommonUsername,
                    CommonPassword = dialog.CommonPassword,
                    UseCommonCredentials = dialog.UseCommonCredentials,
                    AutoLogin = dialog.AutoLogin,
                    Description = dialog.Description,
                    Tags = dialog.Tags,
                    SortOrder = selectedGroup.Sites.Count,
                    IsEnabled = true
                };

                SiteConfig.SaveSite(newSite);
                selectedGroup.Sites.Add(newSite);
                selectedGroup.SiteCount = selectedGroup.Sites.Count;

                RefreshGroups();
                RefreshSites(currentSearchText, currentSearchType);
                // 触发设置保存事件
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            btnAddSite_Click(sender, e);
        }

        private void btnEditSite_Click(object sender, RoutedEventArgs e)
        {
            // 编辑网站功能 - 可以调用现有的btnEdit_Click方法
            btnEdit_Click(sender, e);
        }

        private void btnDeleteSite_Click(object sender, RoutedEventArgs e)
        {
            // 删除网站功能 - 可以调用现有的btnDelete_Click方法
            btnDelete_Click(sender, e);
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selectedSite = dgSites.SelectedItem as SiteItem;
            if (selectedSite == null)
            {
                MessageBox.Show("请先选择一个网站", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new SiteEditDialog(selectedSite);
            if (dialog.ShowDialog() == true)
            {
                selectedSite.DisplayName = dialog.DisplayName;
                selectedSite.Url = dialog.Url;
                selectedSite.Username = dialog.Username;
                selectedSite.Password = dialog.Password;
                selectedSite.CommonUsername = dialog.CommonUsername;
                selectedSite.CommonPassword = dialog.CommonPassword;
                selectedSite.UseCommonCredentials = dialog.UseCommonCredentials;
                selectedSite.AutoLogin = dialog.AutoLogin;
                selectedSite.Description = dialog.Description;
                selectedSite.Tags = dialog.Tags;

                SiteConfig.SaveSite(selectedSite);
                RefreshSites(currentSearchText, currentSearchType);
                // 触发设置保存事件
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedSite = dgSites.SelectedItem as SiteItem;
            if (selectedSite == null)
            {
                MessageBox.Show("请先选择一个网站", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("确定要删除选中的网站吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                SiteConfig.DeleteSite(selectedSite.Id);
                if (selectedGroup != null)
                {
                    selectedGroup.Sites.Remove(selectedSite);
                    selectedGroup.SiteCount = selectedGroup.Sites.Count;
                }
                RefreshGroups();
                RefreshSites(currentSearchText, currentSearchType);
                // 触发设置保存事件
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
        }

        private void btnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            var selectedSite = dgSites.SelectedItem as SiteItem;
            if (selectedSite == null) return;

            var sites = selectedGroup != null ? selectedGroup.Sites : new ObservableCollection<SiteItem>(allSites);
            var index = sites.IndexOf(selectedSite);
            if (index > 0)
            {
                sites.RemoveAt(index);
                sites.Insert(index - 1, selectedSite);

                // 更新排序
                for (int i = 0; i < sites.Count; i++)
                {
                    sites[i].SortOrder = i;
                    SiteConfig.SaveSite(sites[i]);
                }

                RefreshSites(currentSearchText, currentSearchType);
                dgSites.SelectedItem = selectedSite;
            }
        }

        private void btnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            var selectedSite = dgSites.SelectedItem as SiteItem;
            if (selectedSite == null) return;

            var sites = selectedGroup != null ? selectedGroup.Sites : new ObservableCollection<SiteItem>(allSites);
            var index = sites.IndexOf(selectedSite);
            if (index < sites.Count - 1)
            {
                sites.RemoveAt(index);
                sites.Insert(index + 1, selectedSite);

                // 更新排序
                for (int i = 0; i < sites.Count; i++)
                {
                    sites[i].SortOrder = i;
                    SiteConfig.SaveSite(sites[i]);
                }

                RefreshSites(currentSearchText, currentSearchType);
                dgSites.SelectedItem = selectedSite;
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // 使用辅助方法触发设置保存事件
            RaiseSettingsSaved();
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "加密数据文件|*.dat|所有文件|*.*",
                Title = "选择要导入的数据文件"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ShowLoading();
                    await DataPort.ImportData(dialog.FileName);
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入数据时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    HideLoading();
                }
            }
        }

        private async void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "加密数据文件|*.dat|所有文件|*.*",
                Title = "选择保存位置",
                DefaultExt = ".dat"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await DataPort.ExportData(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出数据时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task LoadWebsiteIcon(SiteItem site)
        {
            try
            {
                var icon = await FaviconDownloader.DownloadFaviconAsync(site.Url);
                if (icon != null)
                {
                    // 将BitmapImage转为Base64字符串存储
                    using (var ms = new MemoryStream())
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(icon));
                        encoder.Save(ms);
                        site.Icon = Convert.ToBase64String(ms.ToArray());
                    }
                    SiteConfig.SaveSite(site);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载网站图标时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ConvertToBase64(BitmapImage image)
        {
            using (var ms = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(ms);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        private async void btnRefreshIcon_Click(object sender, RoutedEventArgs e)
        {
            var selectedSite = dgSites.SelectedItem as SiteItem;
            if (selectedSite == null)
            {
                MessageBox.Show("请先选择一个网站", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var icon = await FaviconDownloader.DownloadFaviconAsync(selectedSite.Url, true);
                if (icon != null)
                {
                    selectedSite.Icon = ConvertToBase64(icon);
                    SiteConfig.SaveSite(selectedSite);
                    RefreshSites(currentSearchText, currentSearchType);
                    MessageBox.Show("图标已更新", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新图标时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            var selectedSite = dgSites.SelectedItem as SiteItem;
            if (selectedSite == null)
            {
                MessageBox.Show("请先选择一个网站", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.ico|所有文件|*.*",
                Title = "选择自定义图标"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var imageBytes = File.ReadAllBytes(dialog.FileName);
                    var base64 = Convert.ToBase64String(imageBytes);
                    selectedSite.Icon = base64;
                    SiteConfig.SaveSite(selectedSite);
                    RefreshSites(currentSearchText, currentSearchType);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"设置自定义图标时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnPreviewIcon_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var site = button?.Tag as SiteItem;
            if (site == null) return;

            var previewWindow = new Window
            {
                Title = "图标预览",
                Width = 200,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Style = (Style)FindResource("PreviewWindowStyle")
            };

            var image = new Image
            {
                Stretch = Stretch.Uniform
            };
            // 支持Base64和本地文件
            if (!string.IsNullOrEmpty(site.Icon))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    if (site.Icon.StartsWith("data:image") || site.Icon.Length > 200) // Base64
                    {
                        var bytes = Convert.FromBase64String(site.Icon.Contains(",") ? site.Icon.Substring(site.Icon.IndexOf(",") + 1) : site.Icon);
                        bitmap.StreamSource = new MemoryStream(bytes);
                    }
                    else if (File.Exists(site.Icon))
                    {
                        bitmap.UriSource = new Uri(site.Icon);
                    }
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    image.Source = bitmap;
                }
                catch { }
            }
        }

        private void btnEditGroup_Click(object sender, RoutedEventArgs e)
        {
            if (tvGroups.SelectedItem is SiteGroupViewModel group)
            {
                // 从数据库获取完整的分组信息
                using (var context = new AppDbContext())
                {
                    var dbGroup = context.SiteGroups.FirstOrDefault(g => g.Id == group.Id);
                    if (dbGroup != null)
                    {
                        var dialog = new GroupEditDialog(dbGroup.Name, dbGroup.IsDefaultExpanded);
                        if (dialog.ShowDialog() == true)
                        {
                            try
                            {
                                // 记录当前组ID，用于后续重新选中
                                int groupId = group.Id;
                                string newName = dialog.GroupName;
                                bool newIsDefaultExpanded = dialog.IsDefaultExpanded;

                                // 直接从数据库获取最新的组对象
                                using (var updateContext = new AppDbContext())
                                {
                                    var groupToUpdate = updateContext.SiteGroups.FirstOrDefault(g => g.Id == groupId);
                                    if (groupToUpdate != null)
                                    {
                                        groupToUpdate.Name = newName;
                                        groupToUpdate.IsDefaultExpanded = newIsDefaultExpanded;
                                        updateContext.SaveChanges();
                                    }
                                }

                                // 更新本地数据
                                group.Name = newName;
                                allGroups.First(g => g.Id == groupId).Name = newName;
                                allGroups.First(g => g.Id == groupId).IsDefaultExpanded = newIsDefaultExpanded;

                                // 刷新界面
                                RefreshGroups();

                                // 直接刷新主窗口菜单
                                RefreshMainWindowMenu();

                                // 调试输出
                                System.Diagnostics.Debug.WriteLine($"[btnEditGroup_Click] 刷新主窗口菜单已调用");
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"更新分组信息时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("请先选择一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnDeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (tvGroups.SelectedItem is SiteGroupViewModel group)
            {
                string msg = $"确定要删除分组{group.Name}及其下所有网站吗？";
                if (MessageBox.Show(msg, "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // 先删除所有网站
                    foreach (var site in group.Sites.ToList())
                    {
                        SiteConfig.DeleteSite(site.Id);
                    }
                    // 删除分组
                    SiteConfig.DeleteGroup(group.Id);
                    allGroups.RemoveAll(g => g.Id == group.Id);
                    groups.Remove(group);
                    selectedGroup = null;
                    RefreshGroups();
                    RefreshSites(currentSearchText, currentSearchType);
                    // 触发设置保存事件
                    SettingsSaved?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                MessageBox.Show("请先选择一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnMoveGroupUp_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroup = tvGroups.SelectedItem as SiteGroupViewModel;
            if (selectedGroup == null) return;

            var index = groups.IndexOf(selectedGroup);
            if (index > 0)
            {
                groups.RemoveAt(index);
                groups.Insert(index - 1, selectedGroup);

                // 更新排序
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = allGroups.First(g => g.Id == groups[i].Id);
                    group.SortOrder = i;
                    SiteConfig.SaveGroup(group);
                }

                // 重新从数据库加载数据
                allGroups = SiteConfig.GetAllGroups();
                allSites = SiteConfig.GetAllSites() ?? new List<SiteItem>();
                RefreshGroups();
                
                // 找到移动后的分组
                var movedGroup = groups[index - 1];
                selectedGroup = movedGroup;
                RefreshSites(currentSearchText, currentSearchType);

                // 使用 Dispatcher 延迟设置选中项
                Dispatcher.BeginInvoke(new Action(() => {
                    var item = tvGroups.ItemContainerGenerator.ContainerFromItem(selectedGroup) as TreeViewItem;
                    if (item != null)
                    {
                        item.IsSelected = true;
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);

                // 触发设置保存事件
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
        }

        private void btnMoveGroupDown_Click(object sender, RoutedEventArgs e)
        {
            var selectedGroup = tvGroups.SelectedItem as SiteGroupViewModel;
            if (selectedGroup == null) return;

            var index = groups.IndexOf(selectedGroup);
            if (index < groups.Count - 1)
            {
                groups.RemoveAt(index);
                groups.Insert(index + 1, selectedGroup);

                // 更新排序
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = allGroups.First(g => g.Id == groups[i].Id);
                    group.SortOrder = i;
                    SiteConfig.SaveGroup(group);
                }

                // 重新从数据库加载数据
                allGroups = SiteConfig.GetAllGroups();
                allSites = SiteConfig.GetAllSites() ?? new List<SiteItem>();
                RefreshGroups();
                
                // 找到移动后的分组
                var movedGroup = groups[index + 1];
                selectedGroup = movedGroup;
                RefreshSites(currentSearchText, currentSearchType);

                // 使用 Dispatcher 延迟设置选中项
                Dispatcher.BeginInvoke(new Action(() => {
                    var item = tvGroups.ItemContainerGenerator.ContainerFromItem(selectedGroup) as TreeViewItem;
                    if (item != null)
                    {
                        item.IsSelected = true;
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);

                // 触发设置保存事件
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 设为默认浏览器按钮点击事件
        /// </summary>
        private void btnSetDefaultBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查登录状态
                if (!Utils.LoginManager.IsLoggedIn)
                {
                    MessageBox.Show("请先登录后再设置默认浏览器", "未登录", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 检查当前是否已经是默认浏览器
                if (DefaultBrowserManager.IsDefaultBrowser())
                {
                    MessageBox.Show("BrowserTool 已经是默认浏览器", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 尝试设置为默认浏览器
                var result = MessageBox.Show(
                    "确定要将 BrowserTool 设置为默认浏览器吗？\n\n这将使所有网页链接默认使用 BrowserTool 打开。",
                    "设置默认浏览器",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (DefaultBrowserManager.SetAsDefaultBrowser())
                    {
                        MessageBox.Show("默认浏览器设置操作已完成。\n\n请检查系统设置确认更改是否生效。", "设置完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置默认浏览器时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"[设置默认浏览器错误] {ex}");
            }
        }

        /// <summary>
        /// 显示加载指示器
        /// </summary>
        private void ShowLoading()
        {
            LoadingControl.IsLoading = true;
        }

        /// <summary>
        /// 隐藏加载指示器
        /// </summary>
        private void HideLoading()
        {
            LoadingControl.IsLoading = false;
        }

        public class SiteGroupViewModel
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int SiteCount { get; set; }
            public ObservableCollection<SiteItem> Sites { get; set; } = new ObservableCollection<SiteItem>();

            public SiteGroupViewModel(SiteGroup group)
            {
                Id = group.Id;
                Name = group.Name;
                SiteCount = SiteConfig.GetSitesByGroup(group.Id).Count;
                Sites = new ObservableCollection<SiteItem>(SiteConfig.GetSitesByGroup(group.Id));
            }
        }
    }
}