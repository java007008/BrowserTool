using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace BrowserTool.Utils
{
    public static class FaviconDownloader
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string IconsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons");
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromDays(7); // 图标缓存过期时间

        static FaviconDownloader()
        {
            if (!Directory.Exists(IconsDirectory))
            {
                Directory.CreateDirectory(IconsDirectory);
            }
        }

        public static async Task<BitmapImage> DownloadFaviconAsync(string url, bool forceRefresh = false)
        {
            try
            {
                var uri = new Uri(url);
                var baseUrl = $"{uri.Scheme}://{uri.Host}";
                var iconPath = Path.Combine(IconsDirectory, $"{uri.Host}.png");

                // 检查缓存是否过期
                if (!forceRefresh && File.Exists(iconPath))
                {
                    var fileInfo = new FileInfo(iconPath);
                    if (DateTime.Now - fileInfo.LastWriteTime < CacheExpiration)
                    {
                        return LoadIconFromFile(iconPath);
                    }
                }

                // 尝试从网站根目录获取favicon
                var faviconUrl = $"{baseUrl}/favicon.ico";
                var response = await client.GetAsync(faviconUrl);
                if (response.IsSuccessStatusCode)
                {
                    var iconBytes = await response.Content.ReadAsByteArrayAsync();
                    return await SaveAndLoadIconAsync(iconBytes, uri.Host);
                }

                // 如果favicon.ico不存在,尝试从HTML中解析favicon链接
                var html = await client.GetStringAsync(url);
                var faviconLinks = ParseFaviconLinks(html, baseUrl);
                
                if (faviconLinks.Any())
                {
                    // 优先选择ICO格式的图标
                    var icoLink = faviconLinks.FirstOrDefault(l => l.EndsWith(".ico", StringComparison.OrdinalIgnoreCase));
                    if (icoLink != null)
                    {
                        response = await client.GetAsync(icoLink);
                        if (response.IsSuccessStatusCode)
                        {
                            var iconBytes = await response.Content.ReadAsByteArrayAsync();
                            return await SaveAndLoadIconAsync(iconBytes, uri.Host);
                        }
                    }

                    // 如果没有ICO格式,尝试其他格式
                    foreach (var link in faviconLinks)
                    {
                        response = await client.GetAsync(link);
                        if (response.IsSuccessStatusCode)
                        {
                            var iconBytes = await response.Content.ReadAsByteArrayAsync();
                            return await SaveAndLoadIconAsync(iconBytes, uri.Host);
                        }
                    }
                }

                return new BitmapImage(new Uri("pack://application:,,,/Resources/default_icon.png"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"下载图标时出错: {ex.Message}");
                return new BitmapImage(new Uri("pack://application:,,,/Resources/default_icon.png"));
            }
        }

        public static async Task<bool> SaveCustomIconAsync(string host, string imagePath)
        {
            try
            {
                var iconPath = Path.Combine(IconsDirectory, $"{host}.png");
                using (var bitmap = new Bitmap(imagePath))
                {
                    bitmap.Save(iconPath, ImageFormat.Png);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ClearIconCache()
        {
            try
            {
                var files = Directory.GetFiles(IconsDirectory, "*.png");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // 忽略删除失败的错误
            }
        }

        private static BitmapImage LoadIconFromFile(string iconPath)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(iconPath);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载图标文件时出错: {ex.Message}");
                return new BitmapImage(new Uri("pack://application:,,,/Resources/default_icon.png"));
            }
        }

        private static List<string> ParseFaviconLinks(string html, string baseUrl)
        {
            var links = new List<string>();
            var patterns = new[]
            {
                @"<link[^>]*rel=[""'](?:shortcut\s+)?icon[""'][^>]*href=[""']([^""']+)[""']",
                @"<link[^>]*href=[""']([^""']+)[""'][^>]*rel=[""'](?:shortcut\s+)?icon[""']"
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    var href = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        // 处理相对路径
                        if (href.StartsWith("/"))
                        {
                            href = baseUrl + href;
                        }
                        else if (!href.StartsWith("http"))
                        {
                            href = baseUrl + "/" + href;
                        }
                        links.Add(href);
                    }
                }
            }

            return links.Distinct().ToList();
        }

        private static async Task<BitmapImage> SaveAndLoadIconAsync(byte[] iconBytes, string host)
        {
            var iconPath = Path.Combine(IconsDirectory, $"{host}.png");
            try
            {
                using (var ms = new MemoryStream(iconBytes))
                using (var bitmap = new Bitmap(ms))
                {
                    bitmap.Save(iconPath, ImageFormat.Png);
                }
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(iconPath);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存图标时出错: {ex.Message}");
                try
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = new MemoryStream(iconBytes);
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
                catch
                {
                    return new BitmapImage(new Uri("pack://application:,,,/Resources/default_icon.png"));
                }
            }
        }
    }
} 