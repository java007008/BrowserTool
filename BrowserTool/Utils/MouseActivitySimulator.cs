using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;

namespace BrowserTool.Utils
{
    /// <summary>
    /// 鼠标活动模拟器 - 用于在鼠标和键盘长时间不活动时模拟鼠标移动
    /// </summary>
    public class MouseActivitySimulator
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        // 鼠标事件常量
        private const uint MOUSEEVENTF_MOVE = 0x0001;      // 鼠标移动事件

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;
        private int _interval;
        private DateTime _lastUserActivity;

        public MouseActivitySimulator()
        {
            _interval = 120000; // 默认2分钟
            _lastUserActivity = DateTime.Now;
        }

        /// <summary>
        /// 获取模拟器是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 获取系统空闲时间（毫秒）
        /// </summary>
        private uint GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            
            if (GetLastInputInfo(ref lastInputInfo))
            {
                return GetTickCount() - lastInputInfo.dwTime;
            }
            return 0;
        }

        /// <summary>
        /// 启动鼠标活动模拟
        /// </summary>
        public void Start()
        {
            try
            {
                // 从配置文件读取设置
                bool enableSimulation = true; // 默认启用
                if (ConfigurationManager.AppSettings["EnableMouseActivitySimulation"] != null)
                {
                    bool.TryParse(ConfigurationManager.AppSettings["EnableMouseActivitySimulation"], out enableSimulation);
                }

                if (!enableSimulation)
                {
                    return; // 如果配置为禁用，则不启动模拟
                }

                // 读取间隔时间
                if (ConfigurationManager.AppSettings["MouseActivityInterval"] != null)
                {
                    int.TryParse(ConfigurationManager.AppSettings["MouseActivityInterval"], out _interval);
                }

                if (_isRunning)
                {
                    return;
                }

                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;

                Task.Run(async () =>
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            // 获取系统空闲时间
                            uint idleTime = GetIdleTime();
                            
                            // 如果空闲时间超过配置的间隔时间，执行鼠标模拟
                            if (idleTime >= _interval)
                            {
                                // 模拟鼠标移动2像素然后移回，确保系统检测到活动
                                mouse_event(MOUSEEVENTF_MOVE, 2, 0, 0, 0);
                                Thread.Sleep(50);
                                mouse_event(MOUSEEVENTF_MOVE, -2, 0, 0, 0);
                                
                                // 记录模拟活动（用于调试）
                                System.Diagnostics.Debug.WriteLine($"检测到空闲 {idleTime}ms，执行鼠标活动模拟");
                            }
                            else
                            {
                                // 记录当前状态（用于调试）
                                System.Diagnostics.Debug.WriteLine($"系统活跃中，空闲时间: {idleTime}ms，阈值: {_interval}ms");
                            }

                            // 每秒检查一次空闲状态
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            // 发生错误时继续运行
                            System.Diagnostics.Debug.WriteLine($"鼠标活动模拟器异常: {ex.Message}");
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                    }
                }, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动鼠标活动模拟器失败: {ex.Message}");
                // 如果配置读取失败，使用默认值继续运行
                if (!_isRunning)
                {
                    _cancellationTokenSource = new CancellationTokenSource();
                    _isRunning = true;
                }
            }
        }

        /// <summary>
        /// 停止鼠标活动模拟
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _cancellationTokenSource?.Cancel();
            _isRunning = false;
        }
    }
}