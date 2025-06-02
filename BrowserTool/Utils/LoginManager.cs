using System;

namespace BrowserTool.Utils
{
    /// <summary>
    /// 登录状态管理器
    /// </summary>
    public static class LoginManager
    {
        private static bool _isLoggedIn = false;

        /// <summary>
        /// 获取或设置登录状态
        /// </summary>
        public static bool IsLoggedIn
        {
            get { return _isLoggedIn; }
            set 
            { 
                _isLoggedIn = value;
                OnLoginStatusChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// 登录状态改变事件
        /// </summary>
        public static event Action<bool> OnLoginStatusChanged;

        /// <summary>
        /// 设置为已登录状态
        /// </summary>
        public static void SetLoggedIn()
        {
            IsLoggedIn = true;
        }

        /// <summary>
        /// 设置为未登录状态
        /// </summary>
        public static void SetLoggedOut()
        {
            IsLoggedIn = false;
        }
    }
}
