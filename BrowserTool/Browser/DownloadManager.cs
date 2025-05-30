using System.Collections.ObjectModel;
using System.Linq;

namespace BrowserTool.Browser
{
    /// <summary>
    /// 下载任务管理器，集中管理所有下载任务（单例）
    /// </summary>
    public class DownloadManager
    {
        private static DownloadManager _instance;
        public static DownloadManager Instance => _instance ?? (_instance = new DownloadManager());

        // 下载任务列表，便于UI绑定
        public ObservableCollection<DownloadTask> Tasks { get; } = new ObservableCollection<DownloadTask>();

        /// <summary>
        /// 添加或更新下载任务
        /// </summary>
        public void AddOrUpdateTask(DownloadTask task)
        {
            // 检查是否存在相同文件名的正在下载的任务
            var existingTask = Tasks.FirstOrDefault(t => 
                t.FileName == task.FileName && 
                !t.IsComplete && 
                !t.IsCancelled && 
                !t.IsError);

            if (existingTask == null)
            {
                // 如果不存在，添加新任务
                Tasks.Add(task);
            }
            else
            {
                // 更新现有任务的进度和状态
                existingTask.ReceivedBytes = task.ReceivedBytes;
                existingTask.TotalBytes = task.TotalBytes;
                existingTask.IsComplete = task.IsComplete;
                existingTask.IsCancelled = task.IsCancelled;
                existingTask.IsError = task.IsError;
                existingTask.Status = task.Status;
            }
        }

        /// <summary>
        /// 移除任务
        /// </summary>
        public void RemoveTask(DownloadTask task)
        {
            if (Tasks.Contains(task))
            {
                Tasks.Remove(task);
            }
        }
    }
} 