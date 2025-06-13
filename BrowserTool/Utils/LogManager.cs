using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Text;

namespace BrowserTool.Utils
{
    ///// <summary>
    ///// 日志管理器 - 负责将应用程序的调试信息和日志写入到文件
    ///// </summary>
    //public static class LogManager
    //{
    //    #region 私有字段

    //    private static readonly string LogDirectory;
    //    private static readonly string LogFilePath;
    //    private static readonly ConcurrentQueue<LogEntry> LogQueue = new ConcurrentQueue<LogEntry>();
    //    private static readonly Timer LogTimer;
    //    private static readonly object FileLock = new object();
    //    private static volatile bool _isDisposed = false;

    //    #endregion

    //    #region 构造函数

    //    static LogManager()
    //    {
    //        try
    //        {
    //            // 获取exe所在目录
    //            string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    //            LogDirectory = Path.Combine(exeDirectory, "log");
                
    //            // 确保日志目录存在
    //            if (!Directory.Exists(LogDirectory))
    //            {
    //                Directory.CreateDirectory(LogDirectory);
    //            }

    //            // 生成日志文件名（按日期）
    //            string logFileName = $"BrowserTool_{DateTime.Now:yyyy-MM-dd}.log";
    //            LogFilePath = Path.Combine(LogDirectory, logFileName);

    //            // 启动定时器，每500毫秒写入一次日志
    //            LogTimer = new Timer(WriteLogsToFile, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

    //            // 记录日志系统启动
    //            LogInfo("LogManager", "日志系统已启动", $"日志文件路径: {LogFilePath}");
    //        }
    //        catch (Exception ex)
    //        {
    //            // 如果日志系统初始化失败，至少尝试输出到控制台
    //            Console.WriteLine($"LogManager初始化失败: {ex.Message}");
    //        }
    //    }

    //    #endregion

    //    #region 公共日志方法

    //    /// <summary>
    //    /// 记录信息日志
    //    /// </summary>
    //    /// <param name="source">日志来源</param>
    //    /// <param name="message">日志消息</param>
    //    /// <param name="details">详细信息（可选）</param>
    //    public static void LogInfo(string source, string message, string details = null)
    //    {
    //        LogInternal(LogLevel.Info, source, message, details, null);
    //    }

    //    /// <summary>
    //    /// 记录调试日志
    //    /// </summary>
    //    /// <param name="source">日志来源</param>
    //    /// <param name="message">日志消息</param>
    //    /// <param name="details">详细信息（可选）</param>
    //    public static void LogDebug(string source, string message, string details = null)
    //    {
    //        LogInternal(LogLevel.Debug, source, message, details, null);
    //    }

    //    /// <summary>
    //    /// 记录警告日志
    //    /// </summary>
    //    /// <param name="source">日志来源</param>
    //    /// <param name="message">日志消息</param>
    //    /// <param name="details">详细信息（可选）</param>
    //    public static void LogWarning(string source, string message, string details = null)
    //    {
    //        LogInternal(LogLevel.Warning, source, message, details, null);
    //    }

    //    /// <summary>
    //    /// 记录错误日志
    //    /// </summary>
    //    /// <param name="source">日志来源</param>
    //    /// <param name="message">日志消息</param>
    //    /// <param name="exception">异常对象（可选）</param>
    //    /// <param name="details">详细信息（可选）</param>
    //    public static void LogError(string source, string message, Exception exception = null, string details = null)
    //    {
    //        LogInternal(LogLevel.Error, source, message, details, exception);
    //    }

    //    /// <summary>
    //    /// 记录严重错误日志
    //    /// </summary>
    //    /// <param name="source">日志来源</param>
    //    /// <param name="message">日志消息</param>
    //    /// <param name="exception">异常对象（可选）</param>
    //    /// <param name="details">详细信息（可选）</param>
    //    public static void LogFatal(string source, string message, Exception exception = null, string details = null)
    //    {
    //        LogInternal(LogLevel.Fatal, source, message, details, exception);
    //    }

    //    #endregion

    //    #region 私有方法

    //    /// <summary>
    //    /// 内部日志记录方法
    //    /// </summary>
    //    private static void LogInternal(LogLevel level, string source, string message, string details, Exception exception)
    //    {
    //        if (_isDisposed) return;

    //        try
    //        {
    //            var logEntry = new LogEntry
    //            {
    //                Timestamp = DateTime.Now,
    //                Level = level,
    //                Source = source ?? "Unknown",
    //                Message = message ?? "No message",
    //                Details = details,
    //                Exception = exception,
    //                ThreadId = Thread.CurrentThread.ManagedThreadId
    //            };

    //            LogQueue.Enqueue(logEntry);

    //            // 如果是严重错误，立即写入文件
    //            if (level == LogLevel.Fatal || level == LogLevel.Error)
    //            {
    //                Task.Run(() => WriteLogsToFile(null));
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            // 避免日志记录本身出错导致的无限循环
    //            Console.WriteLine($"LogManager内部错误: {ex.Message}");
    //        }
    //    }

    //    /// <summary>
    //    /// 将日志写入文件
    //    /// </summary>
    //    private static void WriteLogsToFile(object state)
    //    {
    //        if (_isDisposed || LogQueue.IsEmpty) return;

    //        try
    //        {
    //            var logsToWrite = new StringBuilder();
                
    //            // 批量处理日志条目
    //            while (LogQueue.TryDequeue(out LogEntry logEntry))
    //            {
    //                logsToWrite.AppendLine(FormatLogEntry(logEntry));
    //            }

    //            if (logsToWrite.Length > 0)
    //            {
    //                lock (FileLock)
    //                {
    //                    File.AppendAllText(LogFilePath, logsToWrite.ToString(), Encoding.UTF8);
    //                }
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Console.WriteLine($"写入日志文件失败: {ex.Message}");
    //        }
    //    }

    //    /// <summary>
    //    /// 格式化日志条目
    //    /// </summary>
    //    private static string FormatLogEntry(LogEntry entry)
    //    {
    //        var sb = new StringBuilder();
            
    //        // 基本信息
    //        sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] [{2}] [Thread-{3}] {4}",
    //            entry.Timestamp,
    //            entry.Level.ToString().ToUpper(),
    //            entry.Source,
    //            entry.ThreadId,
    //            entry.Message);

    //        // 详细信息
    //        if (!string.IsNullOrEmpty(entry.Details))
    //        {
    //            sb.AppendLine();
    //            sb.AppendFormat("    详细信息: {0}", entry.Details);
    //        }

    //        // 异常信息
    //        if (entry.Exception != null)
    //        {
    //            sb.AppendLine();
    //            sb.AppendFormat("    异常类型: {0}", entry.Exception.GetType().Name);
    //            sb.AppendLine();
    //            sb.AppendFormat("    异常消息: {0}", entry.Exception.Message);
                
    //            if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
    //            {
    //                sb.AppendLine();
    //                sb.AppendFormat("    堆栈跟踪: {0}", entry.Exception.StackTrace);
    //            }

    //            // 内部异常
    //            if (entry.Exception.InnerException != null)
    //            {
    //                sb.AppendLine();
    //                sb.AppendFormat("    内部异常: {0} - {1}", 
    //                    entry.Exception.InnerException.GetType().Name,
    //                    entry.Exception.InnerException.Message);
    //            }
    //        }

    //        return sb.ToString();
    //    }

    //    #endregion

    //    #region Logger实例创建

    //    /// <summary>
    //    /// 获取当前调用类的Logger实例
    //    /// </summary>
    //    /// <returns>Logger实例</returns>
    //    public static Logger GetCurrentClassLogger()
    //    {
    //        // 获取调用栈中的调用类名
    //        var stackTrace = new System.Diagnostics.StackTrace();
    //        var callingType = stackTrace.GetFrame(1)?.GetMethod()?.DeclaringType;
    //        var className = callingType?.Name ?? "Unknown";
            
    //        return new Logger(className);
    //    }

    //    #endregion

    //    #region 清理资源

    //    /// <summary>
    //    /// 清理日志管理器资源
    //    /// </summary>
    //    public static void Dispose()
    //    {
    //        if (_isDisposed) return;

    //        _isDisposed = true;

    //        try
    //        {
    //            LogInfo("LogManager", "正在关闭日志系统...");
                
    //            // 停止定时器
    //            LogTimer?.Dispose();

    //            // 写入剩余的日志
    //            WriteLogsToFile(null);

    //            LogInfo("LogManager", "日志系统已关闭");
    //        }
    //        catch (Exception ex)
    //        {
    //            Console.WriteLine($"LogManager清理资源时出错: {ex.Message}");
    //        }
    //    }

    //    #endregion
    //}

    ///// <summary>
    ///// Logger类 - 提供类级别的日志记录功能
    ///// </summary>
    //public class Logger
    //{
    //    private readonly string _className;

    //    internal Logger(string className)
    //    {
    //        _className = className;
    //    }

    //    /// <summary>
    //    /// 记录调试日志
    //    /// </summary>
    //    public void Debug(string message, string details = null)
    //    {
    //        LogManager.LogDebug(_className, message, details);
    //    }

    //    /// <summary>
    //    /// 记录信息日志
    //    /// </summary>
    //    public void Info(string message, string details = null)
    //    {
    //        LogManager.LogInfo(_className, message, details);
    //    }

    //    /// <summary>
    //    /// 记录警告日志
    //    /// </summary>
    //    public void Warn(string message, string details = null)
    //    {
    //        LogManager.LogWarning(_className, message, details);
    //    }

    //    /// <summary>
    //    /// 记录错误日志
    //    /// </summary>
    //    public void Error(string message, Exception exception = null, string details = null)
    //    {
    //        LogManager.LogError(_className, message, exception, details);
    //    }

    //    /// <summary>
    //    /// 记录严重错误日志
    //    /// </summary>
    //    public void Fatal(string message, Exception exception = null, string details = null)
    //    {
    //        LogManager.LogFatal(_className, message, exception, details);
    //    }
    //}


    //#region 辅助类和枚举

    ///// <summary>
    ///// 日志级别
    ///// </summary>
    //public enum LogLevel
    //{
    //    Debug = 0,
    //    Info = 1,
    //    Warning = 2,
    //    Error = 3,
    //    Fatal = 4
    //}

    ///// <summary>
    ///// 日志条目
    ///// </summary>
    //internal class LogEntry
    //{
    //    public DateTime Timestamp { get; set; }
    //    public LogLevel Level { get; set; }
    //    public string Source { get; set; }
    //    public string Message { get; set; }
    //    public string Details { get; set; }
    //    public Exception Exception { get; set; }
    //    public int ThreadId { get; set; }
    //}

    //#endregion
}
