using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows;

namespace BrowserTool
{
    public class IsTreeViewRootConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var tvi = value as TreeViewItem;
            if (tvi == null)
                return System.Windows.Visibility.Collapsed;
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(tvi);
            while (parent != null && !(parent is TreeViewItem) && !(parent is System.Windows.Controls.TreeView))
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            if (parent is System.Windows.Controls.TreeView)
            {
                // 只有父级为TreeView才是根节点
                return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 