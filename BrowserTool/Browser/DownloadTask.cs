using System;
using System.ComponentModel;
using CefSharp;

namespace BrowserTool.Browser
{
    /// <summary>
    /// 下载任务数据结构，支持属性变更通知
    /// </summary>
    public class DownloadTask : INotifyPropertyChanged
    {
        private string _fileName;
        private string _fullPath;
        private long _receivedBytes;
        private long _totalBytes;
        private bool _isComplete;
        private bool _isCancelled;
        private bool _isError;
        private string _status;

        public string FileName 
        { 
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }
        
        public string FullPath 
        { 
            get => _fullPath;
            set
            {
                _fullPath = value;
                OnPropertyChanged(nameof(FullPath));
            }
        }
        
        public long ReceivedBytes 
        { 
            get => _receivedBytes;
            set
            {
                _receivedBytes = value;
                OnPropertyChanged(nameof(ReceivedBytes));
                OnPropertyChanged(nameof(Progress));
            }
        }
        
        public long TotalBytes 
        { 
            get => _totalBytes;
            set
            {
                _totalBytes = value;
                OnPropertyChanged(nameof(TotalBytes));
                OnPropertyChanged(nameof(Progress));
            }
        }
        
        public bool IsComplete 
        { 
            get => _isComplete;
            set
            {
                _isComplete = value;
                OnPropertyChanged(nameof(IsComplete));
            }
        }
        
        public bool IsCancelled 
        { 
            get => _isCancelled;
            set
            {
                _isCancelled = value;
                OnPropertyChanged(nameof(IsCancelled));
            }
        }
        
        public bool IsError 
        { 
            get => _isError;
            set
            {
                _isError = value;
                OnPropertyChanged(nameof(IsError));
            }
        }
        
        public string Status 
        { 
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public DateTime StartTime { get; set; } = DateTime.Now;

        // 下载回调，用于取消下载
        [Newtonsoft.Json.JsonIgnore]
        public IDownloadItemCallback Callback { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public double Progress => TotalBytes > 0 ? (double)ReceivedBytes / TotalBytes : 0;

        /// <summary>
        /// 取消下载
        /// </summary>
        public void Cancel()
        {
            if (Callback != null && !Callback.IsDisposed)
            {
                Callback.Cancel();
                IsCancelled = true;
                Status = "已取消";
                IsComplete = true;
            }
        }
    }
} 