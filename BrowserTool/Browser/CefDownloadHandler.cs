using CefSharp;
using System;
using System.Windows;

namespace BrowserTool.Browser
{
    /// <summary>
    /// CefSharp下载处理器，支持文件下载
    /// </summary>
    public class CefDownloadHandler : IDownloadHandler
    {
        public event Action<string> OnBeforeDownloadFired;
        public event Action<string> OnDownloadUpdatedFired;

        public void OnBeforeDownload(IWebBrowser browserControl, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
            // 下载前回调，可自定义保存路径
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnBeforeDownloadFired?.Invoke(downloadItem.SuggestedFileName);
                // 新建或更新下载任务
                DownloadManager.Instance.AddOrUpdateTask(new DownloadTask
                {
                    FileName = downloadItem.SuggestedFileName,
                    FullPath = downloadItem.FullPath,
                    ReceivedBytes = downloadItem.ReceivedBytes,
                    TotalBytes = downloadItem.TotalBytes,
                    IsComplete = false,
                    Status = "正在下载"
                });
            });
            if (!callback.IsDisposed)
            {
                // 默认保存到用户下载目录
                string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", downloadItem.SuggestedFileName);
                callback.Continue(path, showDialog: true);
            }
        }

        public void OnDownloadUpdated(IWebBrowser browserControl, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback)
        {
            // 下载进度更新
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnDownloadUpdatedFired?.Invoke(downloadItem.FullPath);
                DownloadManager.Instance.AddOrUpdateTask(new DownloadTask
                {
                    FileName = downloadItem.SuggestedFileName,
                    FullPath = downloadItem.FullPath,
                    ReceivedBytes = downloadItem.ReceivedBytes,
                    TotalBytes = downloadItem.TotalBytes,
                    IsComplete = downloadItem.IsComplete,
                    IsCancelled = downloadItem.IsCancelled,
                    IsError = downloadItem.IsCancelled || downloadItem.IsCancelled,
                    Status = downloadItem.IsComplete ? "已完成" : (downloadItem.IsCancelled ? "已取消" : "正在下载"),
                    Callback = callback
                });
            });
        }

        // 新增：实现IDownloadHandler接口的CanDownload方法，允许所有下载
        public bool CanDownload(IWebBrowser browserControl, IBrowser browser, string url, string requestMethod)
        {
            return true;
        }
    }
} 