using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using BrowserTool.Database.Entities;
using System.Threading.Tasks;
using BrowserTool.Database;

namespace BrowserTool.Utils
{
    public static class DataPort
    {
        public class ExportDataModel
        {
            public List<SiteGroup> Groups { get; set; }
            public List<SiteItem> Sites { get; set; }
        }

        public static async Task ExportData(string filePath)
        {
            try
            {
                var data = new ExportDataModel
                {
                    Groups = SiteConfig.GetGroups(),
                    Sites = new List<SiteItem>()
                };

                // 获取所有分组的网站
                foreach (var group in data.Groups)
                {
                    data.Sites.AddRange(SiteConfig.GetSitesByGroup(group.Id));
                }

                // 序列化为JSON
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // 加密JSON数据
                var encryptedData = CryptoHelper.Encrypt(json);

                // 保存到文件
                await File.WriteAllTextAsync(filePath, encryptedData);

                MessageBox.Show("数据导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static async Task ImportData(string filePath)
        {
            try
            {
                // 读取加密数据
                var encryptedData = await File.ReadAllTextAsync(filePath);

                // 解密数据
                var json = CryptoHelper.Decrypt(encryptedData);

                // 反序列化
                var data = JsonSerializer.Deserialize<ExportDataModel>(json);

                // 导入数据
                foreach (var group in data.Groups)
                {
                    SiteConfig.SaveGroup(group);
                }

                foreach (var site in data.Sites)
                {
                    SiteConfig.SaveSite(site);
                }

                MessageBox.Show("数据导入成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 