using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using NLog;

namespace BrowserTool.Utils
{
    /// <summary>
    /// 鼠标活动模拟器 - 用于在鼠标和键盘长时间不活动时模拟鼠标移动
    /// 通过智能的移动算法，在屏幕范围内进行鼠标移动，防止系统进入休眠状态
    /// </summary>
    public class MouseActivitySimulator
    {
        private static readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        #region Win32 API 声明

        /// <summary>
        /// 模拟鼠标事件的Win32 API函数
        /// </summary>
        /// <param name="dwFlags">鼠标事件标志，指定要执行的鼠标操作类型</param>
        /// <param name="dx">鼠标在X轴方向的移动距离（相对移动）</param>
        /// <param name="dy">鼠标在Y轴方向的移动距离（相对移动）</param>
        /// <param name="dwData">鼠标滚轮数据，通常为0</param>
        /// <param name="dwExtraInfo">额外信息，通常为0</param>
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        /// <summary>
        /// 获取当前鼠标光标位置的Win32 API函数
        /// </summary>
        /// <param name="lpPoint">输出参数，返回鼠标当前位置的坐标</param>
        /// <returns>成功返回true，失败返回false</returns>
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>
        /// 获取系统最后一次输入信息的Win32 API函数
        /// 用于检测系统空闲时间
        /// </summary>
        /// <param name="plii">LASTINPUTINFO结构体，包含最后输入时间信息</param>
        /// <returns>成功返回true，失败返回false</returns>
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        /// <summary>
        /// 获取系统启动后经过的毫秒数的Win32 API函数
        /// </summary>
        /// <returns>系统启动后经过的毫秒数</returns>
        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        /// <summary>
        /// 获取系统指标的Win32 API函数
        /// 用于获取屏幕分辨率等系统信息
        /// </summary>
        /// <param name="nIndex">系统指标索引</param>
        /// <returns>指定系统指标的值</returns>
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        #endregion

        #region 结构体定义

        /// <summary>
        /// 表示屏幕坐标点的结构体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            /// <summary>X坐标</summary>
            public int X;
            /// <summary>Y坐标</summary>
            public int Y;
        }

        /// <summary>
        /// 系统最后输入信息结构体
        /// 用于获取系统空闲时间
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            /// <summary>结构体大小</summary>
            public uint cbSize;
            /// <summary>最后输入时间（系统启动后的毫秒数）</summary>
            public uint dwTime;
        }

        #endregion

        #region 常量定义

        /// <summary>鼠标移动事件标志</summary>
        private const uint MOUSEEVENTF_MOVE = 0x0001;

        /// <summary>获取屏幕宽度的系统指标索引</summary>
        private const int SM_CXSCREEN = 0;
        /// <summary>获取屏幕高度的系统指标索引</summary>
        private const int SM_CYSCREEN = 1;

        /// <summary>默认移动步长（像素）</summary>
        private const int DEFAULT_MOVE_STEP = 5;
        /// <summary>屏幕边缘安全距离（像素）</summary>
        private const int SCREEN_MARGIN = 50;

        #endregion

        #region 私有字段

        /// <summary>取消令牌源，用于控制异步任务的取消</summary>
        private CancellationTokenSource _cancellationTokenSource;
        /// <summary>模拟器是否正在运行</summary>
        private bool _isRunning;
        /// <summary>检测间隔时间（毫秒）</summary>
        private int _interval;
        /// <summary>当前移动方向（1表示向右，-1表示向左）</summary>
        private int _currentDirection;
        /// <summary>移动步长</summary>
        private int _moveStep;
        /// <summary>屏幕宽度</summary>
        private int _screenWidth;
        /// <summary>屏幕高度</summary>
        private int _screenHeight;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化鼠标活动模拟器
        /// 设置默认参数并获取屏幕尺寸
        /// </summary>
        public MouseActivitySimulator()
        {
            _interval = 120000; // 默认2分钟（120秒）
            _currentDirection = 1; // 默认向右移动
            _moveStep = DEFAULT_MOVE_STEP; // 默认移动步长
            
            // 获取屏幕尺寸
            _screenWidth = GetSystemMetrics(SM_CXSCREEN);
            _screenHeight = GetSystemMetrics(SM_CYSCREEN);
            
            _logger.Debug($"屏幕尺寸: {_screenWidth} x {_screenHeight}");
            _logger.Info("MouseActivitySimulator初始化完成");
            _logger.Warn("测试警告消息");
            _logger.Error("测试错误消息");
            
            // 确保日志被刷新
            LogManager.Flush();
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取模拟器是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        #endregion

        #region 私有方法

        /// <summary>
        /// 获取系统空闲时间（毫秒）
        /// 通过比较当前系统时间和最后输入时间来计算空闲时间
        /// </summary>
        /// <returns>系统空闲时间（毫秒）</returns>
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
        /// 执行智能鼠标移动
        /// 根据当前鼠标位置和屏幕边界，智能选择移动方向
        /// </summary>
        private void PerformSmartMouseMove()
        {
            try
            {
                // 获取当前鼠标位置
                POINT currentPos;
                if (!GetCursorPos(out currentPos))
                {
                    _logger.Error("无法获取当前鼠标位置");
                    return;
                }

                _logger.Debug($"当前鼠标位置: ({currentPos.X}, {currentPos.Y})");

                // 计算移动后的X坐标
                int newX = currentPos.X + (_currentDirection * _moveStep);
                
                // 检查是否需要改变方向
                bool needChangeDirection = false;
                string directionChangeReason = "";

                // 检查右边界
                if (_currentDirection > 0 && newX >= (_screenWidth - SCREEN_MARGIN))
                {
                    needChangeDirection = true;
                    directionChangeReason = $"到达右边界 (newX={newX}, 右边界={_screenWidth - SCREEN_MARGIN})";
                }
                // 检查左边界
                else if (_currentDirection < 0 && newX <= SCREEN_MARGIN)
                {
                    needChangeDirection = true;
                    directionChangeReason = $"到达左边界 (newX={newX}, 左边界={SCREEN_MARGIN})";
                }

                // 如果需要改变方向，则反转方向
                if (needChangeDirection)
                {
                    _currentDirection *= -1;
                    _logger.Debug($"改变移动方向: {directionChangeReason}，新方向: {(_currentDirection > 0 ? "向右" : "向左")}");
                    
                    // 重新计算移动后的坐标
                    newX = currentPos.X + (_currentDirection * _moveStep);
                }

                // 确保新坐标在安全范围内
                newX = Math.Max(SCREEN_MARGIN, Math.Min(newX, _screenWidth - SCREEN_MARGIN));

                // 计算实际移动距离
                int deltaX = newX - currentPos.X;
                int deltaY = 0; // 只进行水平移动

                _logger.Debug($"执行鼠标移动: 当前方向={(_currentDirection > 0 ? "向右" : "向左")}, " +
                            $"移动距离=({deltaX}, {deltaY}), " +
                            $"目标位置=({newX}, {currentPos.Y})");

                // 执行鼠标移动
                if (deltaX != 0)
                {
                    // 先移动到新位置
                    mouse_event(MOUSEEVENTF_MOVE, deltaX, deltaY, 0, 0);
                    _logger.Debug($"鼠标移动完成: 从({currentPos.X}, {currentPos.Y}) 到 ({newX}, {currentPos.Y})");
                    
                    // 等待一小段时间
                    Thread.Sleep(100);
                    
                    // 再移回原位
                    mouse_event(MOUSEEVENTF_MOVE, -deltaX, -deltaY, 0, 0);
                    _logger.Debug($"鼠标回到原位: 从({newX}, {currentPos.Y}) 到 ({currentPos.X}, {currentPos.Y})");
                }
                else
                {
                    _logger.Debug("无需移动鼠标（移动距离为0）");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "执行鼠标移动时发生异常");
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 启动鼠标活动模拟
        /// 从配置文件读取设置，启动后台任务监控系统空闲状态
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                _logger.Warn("鼠标活动模拟器已经在运行中");
                return;
            }

            try
            {
                // 从配置文件读取设置
                if (ConfigurationManager.AppSettings["MouseActivityInterval"] != null)
                {
                    if (int.TryParse(ConfigurationManager.AppSettings["MouseActivityInterval"], out int interval))
                    {
                        _interval = interval;
                    }
                }

                if (ConfigurationManager.AppSettings["MouseMoveStep"] != null)
                {
                    if (int.TryParse(ConfigurationManager.AppSettings["MouseMoveStep"], out int moveStep))
                    {
                        _moveStep = moveStep;
                    }
                }

                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;

                _logger.Info($"启动鼠标活动模拟器 - 检测间隔: {_interval}ms, 移动步长: {_moveStep}px, 屏幕尺寸: {_screenWidth}x{_screenHeight}");

                // 启动后台监控任务
                Task.Run(async () =>
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            // 获取系统空闲时间
                            uint idleTime = GetIdleTime();
                            
                            // 如果空闲时间超过配置的间隔时间，执行智能鼠标移动
                            if (idleTime >= _interval)
                            {
                                _logger.Debug($"检测到系统空闲 {idleTime}ms（阈值: {_interval}ms），执行智能鼠标移动");
                                PerformSmartMouseMove();
                            }

                            // 每秒检查一次空闲状态
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.Info("鼠标活动模拟器任务被取消");
                            break;
                        }
                        catch (Exception ex)
                        {
                            // 发生错误时记录日志并继续运行
                            _logger.Error(ex, "鼠标活动模拟器运行异常");
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                        }
                    }
                }, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "启动鼠标活动模拟器时发生异常");
                _isRunning = false;
            }
        }

        /// <summary>
        /// 停止鼠标活动模拟
        /// 取消后台监控任务并清理资源
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                _logger.Warn("鼠标活动模拟器未在运行");
                return;
            }

            try
            {
                _cancellationTokenSource?.Cancel();
                _isRunning = false;
                _logger.Info("鼠标活动模拟器已停止");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "停止鼠标活动模拟器时发生异常");
            }
        }

        #endregion
    }
}