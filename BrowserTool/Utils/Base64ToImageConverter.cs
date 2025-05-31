using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace BrowserTool.Utils
{
    public class Base64ToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null || !(value is string base64String) || string.IsNullOrEmpty(base64String))
                {
                    // 返回默认图标
                    return new BitmapImage(new Uri("pack://application:,,,/Resources/default_icon.png"));
                }

                // 尝试将Base64字符串转换为图像
                byte[] bytes = System.Convert.FromBase64String(base64String);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze(); // 重要：使图像可在线程间共享
                    return image;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Base64ToImageConverter转换错误: {ex.Message}");
                // 转换失败时返回默认图标
                return new BitmapImage(new Uri("pack://application:,,,/Resources/default_icon.png"));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("不支持从图像转换回Base64字符串");
        }
    }
}
