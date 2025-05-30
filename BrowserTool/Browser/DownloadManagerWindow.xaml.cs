using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace BrowserTool.Browser
{
    /// <summary>
    /// 下载管理器窗口，集中显示所有下载任务
    /// </summary>
    public partial class DownloadManagerWindow : Window
    {
        private static DownloadManagerWindow _instance;
        public static void ShowSingleton()
        {
            if (_instance == null || !_instance.IsVisible)
            {
                _instance = new DownloadManagerWindow();
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }
        }

        public DownloadManagerWindow()
        {
            InitializeComponent();
            this.DataContext = DownloadManager.Instance;
        }

        // 打开文件
        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadGrid.SelectedItem is DownloadTask task && task.IsComplete)
            {
                try { Process.Start(new ProcessStartInfo(task.FullPath) { UseShellExecute = true }); } catch { }
            }
        }

        // 打开文件夹
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadGrid.SelectedItem is DownloadTask task && task.IsComplete)
            {
                try { Process.Start("explorer.exe", $"/select,\"{task.FullPath}\""); } catch { }
            }
        }

        // 取消按钮点击事件
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is DownloadTask task && !task.IsComplete)
            {
                task.Cancel();
            }
        }

        // 清除已完成按钮点击事件
        private void ClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            var completed = DownloadManager.Instance.Tasks.Where(t => t.IsComplete).ToList();
            foreach (var t in completed)
            {
                DownloadManager.Instance.Tasks.Remove(t);
            }
        }
    }
} 