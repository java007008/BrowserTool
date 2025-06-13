using BrowserTool.Database.Entities;
using BrowserTool.Utils;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BrowserTool.Database
{
    public static class SiteConfig
    {
        /// <summary>
        /// 添加数据变更事件
        /// </summary>
        public static event EventHandler DataChanged;

        /// <summary>
        /// 触发数据变更事件的方法
        /// </summary>
        private static void OnDataChanged()
        {
            DataChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// 解密网站敏感信息
        /// </summary>
        /// <param name="site">要解密的网站对象</param>
        private static void DecryptSiteInfo(SiteItem site)
        {
            if (site == null) return;

            try
            {
                // 检查DisplayName和Url是否已经是明文
                bool isDisplayNameEncrypted = IsEncryptedString(site.DisplayName);
                bool isUrlEncrypted = IsEncryptedString(site.Url);


                if (!string.IsNullOrEmpty(site.Username))
                {
                    try { site.Username = CryptoHelper.Decrypt(site.Username); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"解密Username失败: {ex}"); }
                }
                if (!string.IsNullOrEmpty(site.Password))
                {
                    try { site.Password = CryptoHelper.Decrypt(site.Password); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"解密Password失败: {ex}"); }
                }
                if (!string.IsNullOrEmpty(site.CommonUsername))
                {
                    try { site.CommonUsername = CryptoHelper.Decrypt(site.CommonUsername); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"解密CommonUsername失败: {ex}"); }
                }
                if (!string.IsNullOrEmpty(site.CommonPassword))
                {
                    try { site.CommonPassword = CryptoHelper.Decrypt(site.CommonPassword); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"解密CommonPassword失败: {ex}"); }
                }
                if (!string.IsNullOrEmpty(site.GoogleSecret))
                {
                    try { site.GoogleSecret = CryptoHelper.Decrypt(site.GoogleSecret); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"解密GoogleSecret失败: {ex}"); }
                }
                if (!string.IsNullOrEmpty(site.Url) && isUrlEncrypted)
                {
                    try { site.Url = CryptoHelper.Decrypt(site.Url); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"解密Url失败: {ex}"); }
                }
                if (!string.IsNullOrEmpty(site.DisplayName) && isDisplayNameEncrypted)
                {
                    try { site.DisplayName = CryptoHelper.Decrypt(site.DisplayName); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"解密DisplayName失败: {ex}"); }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解密站点信息时出错: {ex}");
            }
        }

        private static bool IsEncryptedString(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;

            // 检查是否是有效的Base64字符串
            if (!IsValidBase64String(str)) return false;

            // 检查长度是否足够长（加密后的字符串通常较长）
            if (str.Length < 16) return false;

            return true;
        }

        private static bool IsValidBase64String(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;

            // 检查长度是否为4的倍数
            if (str.Length % 4 != 0) return false;

            // 检查是否只包含有效的Base64字符
            foreach (char c in str)
            {
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '='))
                {
                    return false;
                }
            }

            // 检查填充字符
            int paddingCount = str.Count(c => c == '=');
            if (paddingCount > 2) return false;

            return true;
        }

        /// <summary>
        /// 批量解密网站敏感信息
        /// </summary>
        /// <param name="sites">要解密的网站集合</param>
        private static void DecryptSiteInfoBatch(ICollection<SiteItem> sites)
        {
            if (sites == null) return;

            foreach (var site in sites)
            {
                DecryptSiteInfo(site);
            }
        }

        /// <summary>
        /// 获取所有启用的分组
        /// </summary>
        /// <returns>启用的分组列表</returns>
        public static List<SiteGroup> GetGroups()
        {
            using (var context = new AppDbContext())
            {
                return context.SiteGroups
                    .Where(g => g.IsEnabled)
                    .OrderBy(g => g.SortOrder)
                    .ThenBy(g => g.Name)
                    .ToList();
            }
        }

        /// <summary>
        /// 根据分组ID获取该分组下的所有网站
        /// </summary>
        /// <param name="groupId">分组ID</param>
        /// <returns>该分组下的网站列表</returns>
        public static List<SiteItem> GetSitesByGroup(int groupId)
        {
            using (var context = new AppDbContext())
            {
                var sites = context.SiteItems
                    .Where(s => s.GroupId == groupId && s.IsEnabled)
                    .OrderBy(s => s.SortOrder)
                    .ThenBy(s => s.DisplayName)
                    .ToList();

                // 解密敏感信息
                DecryptSiteInfoBatch(sites);

                return sites;
            }
        }

        /// <summary>
        /// 保存分组信息
        /// </summary>
        /// <param name="group">要保存的分组</param>
        public static void SaveGroup(SiteGroup group)
        {
            using (var context = new AppDbContext())
            {
                if (group.Id == 0)
                {
                    group.CreateTime = DateTime.Now;
                    group.UpdateTime = DateTime.Now;
                    context.SiteGroups.Add(group);
                }
                else
                {
                    var existingGroup = context.SiteGroups.Find(group.Id);
                    if (existingGroup != null)
                    {
                        context.Entry(existingGroup).CurrentValues.SetValues(group);
                        existingGroup.UpdateTime = DateTime.Now;
                    }
                }
                context.SaveChanges();
                OnDataChanged(); // 触发数据变更事件
            }
        }

        /// <summary>
        /// 保存网站信息
        /// </summary>
        /// <param name="site">要保存的网站</param>
        public static void SaveSite(SiteItem site)
        {
            using (var context = new AppDbContext())
            {
                // 创建副本以避免修改原对象
                var siteToSave = new SiteItem
                {
                    Id = site.Id,
                    GroupId = site.GroupId,
                    DisplayName = site.DisplayName,
                    Username = site.Username,
                    Password = site.Password,
                    CommonUsername = site.CommonUsername,
                    CommonPassword = site.CommonPassword,
                    GoogleSecret = site.GoogleSecret,
                    Url = site.Url,
                    IsEnabled = site.IsEnabled,
                    SortOrder = site.SortOrder,
                    CreateTime = site.CreateTime,
                    UpdateTime = site.UpdateTime,
                    LastAccessTime = site.LastAccessTime,
                    Icon = site.Icon,
                    AccessCount = site.AccessCount
                };

                // 加密敏感信息
                if (!string.IsNullOrEmpty(siteToSave.Username))
                {
                    siteToSave.Username = CryptoHelper.Encrypt(siteToSave.Username);
                }
                if (!string.IsNullOrEmpty(siteToSave.Password))
                {
                    siteToSave.Password = CryptoHelper.Encrypt(siteToSave.Password);
                }
                if (!string.IsNullOrEmpty(siteToSave.CommonUsername))
                {
                    siteToSave.CommonUsername = CryptoHelper.Encrypt(siteToSave.CommonUsername);
                }
                if (!string.IsNullOrEmpty(siteToSave.CommonPassword))
                {
                    siteToSave.CommonPassword = CryptoHelper.Encrypt(siteToSave.CommonPassword);
                }
                if (!string.IsNullOrEmpty(siteToSave.GoogleSecret))
                {
                    siteToSave.GoogleSecret = CryptoHelper.Encrypt(siteToSave.GoogleSecret);
                }
                if (!string.IsNullOrEmpty(siteToSave.Url))
                {
                    siteToSave.Url = CryptoHelper.Encrypt(siteToSave.Url);
                }
                if (!string.IsNullOrEmpty(siteToSave.DisplayName))
                {
                    siteToSave.DisplayName = CryptoHelper.Encrypt(siteToSave.DisplayName);
                }

                if (siteToSave.Id == 0)
                {
                    siteToSave.CreateTime = DateTime.Now;
                    siteToSave.UpdateTime = DateTime.Now;
                    context.SiteItems.Add(siteToSave);
                }
                else
                {
                    var existingSite = context.SiteItems.Find(siteToSave.Id);
                    if (existingSite != null)
                    {
                        context.Entry(existingSite).CurrentValues.SetValues(siteToSave);
                        existingSite.UpdateTime = DateTime.Now;
                    }
                }
                context.SaveChanges();
                OnDataChanged(); // 触发数据变更事件
            }
        }

        /// <summary>
        /// 删除指定ID的分组
        /// </summary>
        /// <param name="groupId">要删除的分组ID</param>
        public static void DeleteGroup(int groupId)
        {
            using (var context = new AppDbContext())
            {
                var group = context.SiteGroups.Find(groupId);
                if (group != null)
                {
                    context.SiteGroups.Remove(group);
                    context.SaveChanges();
                    OnDataChanged(); // 触发数据变更事件
                }
            }
        }

        /// <summary>
        /// 删除指定ID的网站
        /// </summary>
        /// <param name="siteId">要删除的网站ID</param>
        public static void DeleteSite(int siteId)
        {
            using (var context = new AppDbContext())
            {
                var site = context.SiteItems.Find(siteId);
                if (site != null)
                {
                    context.SiteItems.Remove(site);
                    context.SaveChanges();
                    OnDataChanged(); // 触发数据变更事件
                }
            }
        }

        /// <summary>
        /// 更新网站的访问信息
        /// </summary>
        /// <param name="siteId">要更新的网站ID</param>
        public static void UpdateSiteAccess(int siteId)
        {
            using (var context = new AppDbContext())
            {
                var site = context.SiteItems.Find(siteId);
                if (site != null)
                {
                    site.LastAccessTime = DateTime.Now;
                    site.AccessCount++;
                    context.SaveChanges();
                }
            }
        }

        /// <summary>
        /// 获取所有分组（包括未启用的）
        /// </summary>
        /// <returns>所有分组的列表</returns>
        public static List<SiteGroup> GetAllGroups()
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    // 检查数据库连接
                    System.Diagnostics.Debug.WriteLine($"数据库路径: {DatabaseInitializer.GetDbPath()}");

                    // 检查是否有任何分组
                    var groupCount = context.SiteGroups.Count();

                    // 先获取所有分组，不使用Include
                    var groups = context.SiteGroups
                        .OrderBy(g => g.SortOrder)
                        .ThenBy(g => g.Name)
                        .ToList();

                    // 手动加载每个分组的站点并解密
                    foreach (var group in groups)
                    {
                        try
                        {
                            // 确保IsDefaultExpanded有有效值
                            if (group.IsDefaultExpanded != true && group.IsDefaultExpanded != false)
                            {
                                System.Diagnostics.Debug.WriteLine($"警告：分组 {group.Name} (ID: {group.Id}) 的IsDefaultExpanded值无效，设置为默认值false");
                                group.IsDefaultExpanded = false;
                            }

                            // 获取站点数据
                            var sites = context.SiteItems
                                .Where(s => s.GroupId == group.Id)
                                .OrderBy(s => s.SortOrder)
                                .ToList();


                            // 检查每个站点的数据
                            foreach (var site in sites)
                            {
                                try
                                {
                                    
                                    // 确保所有必需字段都有有效值
                                    if (string.IsNullOrEmpty(site.DisplayName))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"警告：站点 ID={site.Id} 的DisplayName为空");
                                        site.DisplayName = "未命名站点";
                                    }
                                    if (string.IsNullOrEmpty(site.Url))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"警告：站点 ID={site.Id} 的Url为空");
                                        site.Url = "about:blank";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"处理站点 ID={site.Id} 时出错: {ex}");
                                }
                            }

                            group.Sites = sites;

                            // 解密站点敏感信息
                            if (group.Sites != null)
                            {
                                try
                                {
                                    DecryptSiteInfoBatch(group.Sites);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"解密分组 {group.Name} 的站点信息时出错: {ex}");
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"处理分组 {group.Name} (ID: {group.Id}) 时出错: {ex}");
                            throw;
                        }
                    }

                    return groups;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取分组时出错: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 获取所有网站（包括未启用的）
        /// </summary>
        /// <returns>所有网站的列表</returns>
        public static List<SiteItem> GetAllSites()
        {
            using (var context = new AppDbContext())
            {
                var sites = context.SiteItems
                    .OrderBy(s => s.SortOrder)
                    .ThenBy(s => s.DisplayName)
                    .ToList();

                // 解密敏感信息
                DecryptSiteInfoBatch(sites);

                return sites;
            }
        }

        /// <summary>
        /// 初始化数据库
        /// </summary>
        public static void InitializeDatabase()
        {
            DatabaseInitializer.Initialize();
        }

        /// <summary>
        /// 添加分组
        /// </summary>
        /// <param name="group">要添加的分组</param>
        public static void AddGroup(SiteGroup group)
        {
            using (var context = new AppDbContext())
            {
                context.SiteGroups.Add(group);
                context.SaveChanges();
                OnDataChanged(); // 触发数据变更事件
            }
        }

        /// <summary>
        /// 更新分组
        /// </summary>
        /// <param name="group">要更新的分组</param>
        public static void UpdateGroup(SiteGroup group)
        {
            using (var context = new AppDbContext())
            {
                context.SiteGroups.Update(group);
                context.SaveChanges();
                OnDataChanged(); // 触发数据变更事件
            }
        }

        /// <summary>
        /// 添加网站
        /// </summary>
        /// <param name="site">要添加的网站</param>
        public static void AddSite(SiteItem site)
        {
            using (var context = new AppDbContext())
            {
                // 创建副本并加密敏感信息
                var siteToAdd = new SiteItem
                {
                    GroupId = site.GroupId,
                    DisplayName = !string.IsNullOrEmpty(site.DisplayName) ? CryptoHelper.Encrypt(site.DisplayName) : site.DisplayName,
                    Username = !string.IsNullOrEmpty(site.Username) ? CryptoHelper.Encrypt(site.Username) : site.Username,
                    Password = !string.IsNullOrEmpty(site.Password) ? CryptoHelper.Encrypt(site.Password) : site.Password,
                    CommonUsername = !string.IsNullOrEmpty(site.CommonUsername) ? CryptoHelper.Encrypt(site.CommonUsername) : site.CommonUsername,
                    CommonPassword = !string.IsNullOrEmpty(site.CommonPassword) ? CryptoHelper.Encrypt(site.CommonPassword) : site.CommonPassword,
                    GoogleSecret = !string.IsNullOrEmpty(site.GoogleSecret) ? CryptoHelper.Encrypt(site.GoogleSecret) : site.GoogleSecret,
                    Url = !string.IsNullOrEmpty(site.Url) ? CryptoHelper.Encrypt(site.Url) : site.Url,
                    IsEnabled = site.IsEnabled,
                    SortOrder = site.SortOrder,
                    CreateTime = DateTime.Now,
                    UpdateTime = DateTime.Now,
                    LastAccessTime = site.LastAccessTime,
                    AccessCount = site.AccessCount
                };

                context.SiteItems.Add(siteToAdd);
                context.SaveChanges();
                OnDataChanged(); // 触发数据变更事件
            }
        }

        /// <summary>
        /// 更新网站
        /// </summary>
        /// <param name="site">要更新的网站</param>
        public static void UpdateSite(SiteItem site)
        {
            using (var context = new AppDbContext())
            {
                var existingSite = context.SiteItems.Find(site.Id);
                if (existingSite != null)
                {
                    // 更新非敏感字段
                    existingSite.GroupId = site.GroupId;
                    //existingSite.DisplayName = site.DisplayName;
                    existingSite.IsEnabled = site.IsEnabled;
                    existingSite.SortOrder = site.SortOrder;
                    existingSite.UpdateTime = DateTime.Now;
                    existingSite.LastAccessTime = site.LastAccessTime;
                    existingSite.AccessCount = site.AccessCount;

                    // 加密并更新敏感字段
                    existingSite.Username = !string.IsNullOrEmpty(site.Username) ? CryptoHelper.Encrypt(site.Username) : site.Username;
                    existingSite.Password = !string.IsNullOrEmpty(site.Password) ? CryptoHelper.Encrypt(site.Password) : site.Password;
                    existingSite.CommonUsername = !string.IsNullOrEmpty(site.CommonUsername) ? CryptoHelper.Encrypt(site.CommonUsername) : site.CommonUsername;
                    existingSite.CommonPassword = !string.IsNullOrEmpty(site.CommonPassword) ? CryptoHelper.Encrypt(site.CommonPassword) : site.CommonPassword;
                    existingSite.GoogleSecret = !string.IsNullOrEmpty(site.GoogleSecret) ? CryptoHelper.Encrypt(site.GoogleSecret) : site.GoogleSecret;
                    existingSite.Url = !string.IsNullOrEmpty(site.Url) ? CryptoHelper.Encrypt(site.Url) : site.Url;
                    existingSite.DisplayName = !string.IsNullOrEmpty(site.DisplayName) ? CryptoHelper.Encrypt(site.DisplayName) : site.DisplayName;

                    context.SaveChanges();
                    OnDataChanged(); // 触发数据变更事件
                }
            }
        }
    }
}