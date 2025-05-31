using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace BrowserTool.Utils
{
    /// <summary>
    /// 图像缓存类，用于缓存已加载的图像，提高性能
    /// </summary>
    public static class ImageCache
    {
        // 使用字典缓存已加载的图像
        private static readonly Dictionary<string, BitmapImage> _cache = new Dictionary<string, BitmapImage>();
        
        // 缓存大小限制
        private const int MAX_CACHE_SIZE = 100;
        
        /// <summary>
        /// 获取图像，如果缓存中存在则直接返回，否则加载并缓存
        /// </summary>
        /// <param name="iconSource">图像源（文件路径或Base64字符串）</param>
        /// <returns>BitmapImage对象</returns>
        public static BitmapImage GetImage(string iconSource)
        {
            if (string.IsNullOrEmpty(iconSource))
                return null;
                
            // 检查缓存
            if (_cache.TryGetValue(iconSource, out BitmapImage cachedImage))
                return cachedImage;
                
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                
                // 处理Base64图像
                if (iconSource.StartsWith("data:image") || iconSource.Length > 200)
                {
                    var base64Data = iconSource.Contains(",") 
                        ? iconSource.Substring(iconSource.IndexOf(",") + 1) 
                        : iconSource;
                        
                    var bytes = Convert.FromBase64String(base64Data);
                    bitmap.StreamSource = new MemoryStream(bytes);
                }
                // 处理文件路径
                else if (File.Exists(iconSource))
                {
                    bitmap.UriSource = new Uri(iconSource);
                }
                else
                {
                    return null;
                }
                
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // 重要：使图像可在线程间共享
                
                // 管理缓存大小
                if (_cache.Count >= MAX_CACHE_SIZE)
                {
                    ClearOldestEntries(10); // 清除最早的10个条目
                }
                
                // 添加到缓存
                _cache[iconSource] = bitmap;
                
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 清除最早添加的缓存条目
        /// </summary>
        /// <param name="count">要清除的条目数</param>
        private static void ClearOldestEntries(int count)
        {
            int i = 0;
            var keys = new List<string>(_cache.Keys);
            
            foreach (var key in keys)
            {
                if (i >= count) break;
                
                _cache.Remove(key);
                i++;
            }
        }
        
        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
