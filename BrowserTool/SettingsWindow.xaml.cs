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
        // 添加事件
        public event EventHandler SettingsSaved;

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

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData(); // 只在Loaded事件中调用
        }

        private void LoadData()
        {
            System.Diagnostics.Debug.WriteLine($"[LoadData] dgSites is null? {dgSites == null}");
            // 初始化数据库
            SiteConfig.InitializeDatabase();

            allGroups = SiteConfig.GetAllGroups();
            allSites = SiteConfig.GetAllSites() ?? new List<SiteItem>();
            RefreshGroups();
            RefreshSites();
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
                    IsEnabled = true
                };

                SiteConfig.SaveGroup(newGroup);
                groups.Add(new SiteGroupViewModel(newGroup));
                // 触发设置保存事件
                SettingsSaved?.Invoke(this, EventArgs.Empty);
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
            // 触发设置保存事件
            SettingsSaved?.Invoke(this, EventArgs.Empty);
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
                    await DataPort.ImportData(dialog.FileName);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入数据时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            previewWindow.Content = image;
            previewWindow.Show();
        }

        private void btnEditGroup_Click(object sender, RoutedEventArgs e)
        {
            if (tvGroups.SelectedItem is SiteGroupViewModel group)
            {
                var dialog = new GroupEditDialog(group.Name);
                if (dialog.ShowDialog() == true)
                {
                    // 更新组名
                    group.Name = dialog.GroupName;
                    var dbGroup = allGroups.FirstOrDefault(g => g.Id == group.Id);
                    if (dbGroup != null)
                    {
                        dbGroup.Name = dialog.GroupName;
                        SiteConfig.SaveGroup(dbGroup);
                    }
                    RefreshGroups();
                    // 保持选中（刷新后延迟聚焦）
                    var targetGroup = groups.FirstOrDefault(g => g.Id == group.Id);
                    if (targetGroup != null)
                    {
                        Dispatcher.BeginInvoke(new Action(() => {
                            var item = tvGroups.ItemContainerGenerator.ContainerFromItem(targetGroup) as TreeViewItem;
                            if (item != null)
                                item.IsSelected = true;
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    // 触发设置保存事件
                    SettingsSaved?.Invoke(this, EventArgs.Empty);
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