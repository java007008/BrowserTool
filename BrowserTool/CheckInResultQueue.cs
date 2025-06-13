using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BrowserTool.Utils;
using NLog;

namespace BrowserTool
{
    /// <summary>
    /// 打卡结果数据
    /// </summary>
    public class CheckInResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// 页面URL
        /// </summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// 检查时间
        /// </summary>
        public DateTime CheckTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 备注信息
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 打卡结果队列管理器
    /// </summary>
    public class CheckInResultQueue
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private static readonly Lazy<CheckInResultQueue> _instance = new(() => new CheckInResultQueue());
        
        /// <summary>
        /// 单例实例
        /// </summary>
        public static CheckInResultQueue Instance => _instance.Value;
        
        /// <summary>
        /// 结果队列
        /// </summary>
        private readonly ConcurrentQueue<CheckInResult> _resultQueue = new();
        
        /// <summary>
        /// 等待结果的信号量
        /// </summary>
        private readonly SemaphoreSlim _resultSemaphore = new(0);
        
        /// <summary>
        /// 私有构造函数
        /// </summary>
        private CheckInResultQueue()
        {
        }
        
        /// <summary>
        /// 添加检查结果到队列
        /// </summary>
        /// <param name="result">检查结果</param>
        public void EnqueueResult(CheckInResult result)
        {
            try
            {
                _resultQueue.Enqueue(result);
                _resultSemaphore.Release(); // 通知等待的线程
                _logger.Debug($"添加打卡结果到队列: 成功={result.IsSuccess}, URL={result.Url}, 消息={result.Message}");
            }
            catch (Exception ex)
            {
                _logger.Error("添加结果到队列时发生异常", ex);
            }
        }
        
        /// <summary>
        /// 从队列获取检查结果（带超时）
        /// </summary>
        /// <param name="timeoutMinutes">超时时间（分钟）</param>
        /// <returns>检查结果，超时返回null</returns>
        public async Task<CheckInResult?> DequeueResultAsync(int timeoutMinutes = 2)
        {
            try
            {
                _logger.Debug($"开始等待打卡结果，超时时间: {timeoutMinutes}分钟");
                
                // 等待结果或超时
                var timeout = TimeSpan.FromMinutes(timeoutMinutes);
                bool hasResult = await _resultSemaphore.WaitAsync(timeout);
                
                if (hasResult)
                {
                    if (_resultQueue.TryDequeue(out CheckInResult? result))
                    {
                        _logger.Debug($"成功获取打卡结果: 成功={result.IsSuccess}, URL={result.Url}");
                        return result;
                    }
                    else
                    {
                        _logger.Warn("信号量被释放但队列为空");
                        return null;
                    }
                }
                else
                {
                    _logger.Debug($"等待打卡结果超时（{timeoutMinutes}分钟）");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("从队列获取结果时发生异常", ex);
                return null;
            }
        }
        
        /// <summary>
        /// 清空队列
        /// </summary>
        public async Task ClearAsync()
        {
            try
            {
                while (_resultQueue.TryDequeue(out _))
                {
                    // 清空队列
                }
                
                // 重置信号量
                while (_resultSemaphore.CurrentCount > 0)
                {
                    await _resultSemaphore.WaitAsync(0);
                }
                
                _logger.Debug("打卡结果队列已清空");
            }
            catch (Exception ex)
            {
                _logger.Error("清空队列时发生异常", ex);
            }
        }
        
        /// <summary>
        /// 清空队列（同步版本）
        /// </summary>
        public void Clear()
        {
            try
            {
                while (_resultQueue.TryDequeue(out _))
                {
                    // 清空队列
                }
                
                // 重置信号量（同步方式）
                while (_resultSemaphore.CurrentCount > 0)
                {
                    _resultSemaphore.Wait(0);
                }
                
                _logger.Debug("打卡结果队列已清空");
            }
            catch (Exception ex)
            {
                _logger.Error("清空队列时发生异常", ex);
            }
        }
        
        /// <summary>
        /// 获取队列中的结果数量
        /// </summary>
        public int Count => _resultQueue.Count;
    }
} 