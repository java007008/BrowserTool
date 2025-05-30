using System.Windows;
using BrowserTool.Database.Entities;

namespace BrowserTool
{
    public partial class SiteEditDialog : Window
    {
        public string DisplayName { get; set; }
        public string Url { get; set; }
        public string Description { get; set; }
        public string Tags { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string CommonUsername { get; set; }
        public string CommonPassword { get; set; }
        public bool UseCommonCredentials { get; set; }
        public bool AutoLogin { get; set; }
        public string UsernameSelector { get; set; }
        public string PasswordSelector { get; set; }
        public string CaptchaSelector { get; set; }
        public string LoginButtonSelector { get; set; }
        public string LoginPageFeature { get; set; }
        public string CaptchaValue { get; set; }
        public bool IsPresetCaptcha { get => CaptchaMode == 0; set { if (value) CaptchaMode = 0; } }
        public bool IsGoogleCaptcha { get => CaptchaMode == 1; set { if (value) CaptchaMode = 1; } }
        public int CaptchaMode { get; set; }
        public string GoogleSecret { get; set; }
        private SiteItem _site;

        public SiteEditDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public SiteEditDialog(SiteItem site)
        {
            InitializeComponent();
            DataContext = this;
            _site = new SiteItem();
            DisplayName = site.DisplayName;
            Url = site.Url;
            Description = site.Description;
            Tags = site.Tags;
            Username = site.Username;
            Password = site.Password;
            CommonUsername = site.CommonUsername;
            CommonPassword = site.CommonPassword;
            UseCommonCredentials = site.UseCommonCredentials;
            AutoLogin = site.AutoLogin;
            UsernameSelector = site.UsernameSelector;
            PasswordSelector = site.PasswordSelector;
            CaptchaSelector = site.CaptchaSelector;
            LoginButtonSelector = site.LoginButtonSelector;
            LoginPageFeature = site.LoginPageFeature;
            CaptchaValue = site.CaptchaValue;
            CaptchaMode = site.CaptchaMode;
            GoogleSecret = site.GoogleSecret;
            // 设置密码框的值
            if (!string.IsNullOrEmpty(Password))
            {
                txtPassword.Password = Password;
            }
            if (!string.IsNullOrEmpty(CommonPassword))
            {
                txtCommonPassword.Password = CommonPassword;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                MessageBox.Show("请输入显示名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(Url))
            {
                MessageBox.Show("请输入网址", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_site == null)
            {
                _site = new SiteItem();
            }

            // 更新密码
            Password = txtPassword.Password;
            CommonPassword = txtCommonPassword.Password;
            // 回写所有字段
            _site.DisplayName = DisplayName;
            _site.Url = Url;
            _site.Description = Description;
            _site.Tags = Tags;
            _site.Username = Username;
            _site.Password = Password;
            _site.CommonUsername = CommonUsername;
            _site.CommonPassword = CommonPassword;
            _site.UseCommonCredentials = UseCommonCredentials;
            _site.AutoLogin = AutoLogin;
            _site.UsernameSelector = UsernameSelector;
            _site.PasswordSelector = PasswordSelector;
            _site.CaptchaSelector = CaptchaSelector;
            _site.LoginButtonSelector = LoginButtonSelector;
            _site.LoginPageFeature = LoginPageFeature;
            _site.CaptchaValue = CaptchaValue;
            _site.CaptchaMode = CaptchaMode;
            _site.GoogleSecret = GoogleSecret;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 