using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;

namespace BrowserTool.Utils
{
    /// <summary>
    /// 鼠标活动模拟器 - 用于在鼠标长时间不活动时模拟鼠标移动
    /// </summary>
    public class MouseActivitySimulator
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // 鼠标事件常量
        private const uint MOUSEEVENTF_MOVE = 0x0001;      // 鼠标移动事件
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;  // 使用绝对坐标
        private const int SCREEN_WIDTH = 65535;            // 屏幕宽度最大值
        private const int SCREEN_HEIGHT = 65535;           // 屏幕高度最大值

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;
        private int _interval;

        public MouseActivitySimulator()
        {
            _interval = 30000; // 默认30秒
        }

        /// <summary>
        /// 获取模拟器是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

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
                            // 获取当前鼠标位置
                            POINT currentPos;
                            GetCursorPos(out currentPos);

                            // 如果鼠标位置没有变化，模拟移动
                            if (currentPos.X == _lastX && currentPos.Y == _lastY)
                            {
                                // 模拟鼠标移动1像素
                                mouse_event(MOUSEEVENTF_MOVE, 1, 0, 0, 0);
                                Thread.Sleep(100);
                                mouse_event(MOUSEEVENTF_MOVE, -1, 0, 0, 0);
                            }

                            // 更新上次位置
                            _lastX = currentPos.X;
                            _lastY = currentPos.Y;

                            // 等待配置的间隔时间
                            await Task.Delay(_interval, _cancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception)
                        {
                            // 发生错误时继续运行
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                    }
                }, _cancellationTokenSource.Token);
            }
            catch (Exception)
            {
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

        private int _lastX = -1;
        private int _lastY = -1;
    }
} 