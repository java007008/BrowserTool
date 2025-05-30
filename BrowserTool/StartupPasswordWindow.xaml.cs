using System.Windows;
using BrowserTool.Utils;

namespace BrowserTool
{
    public partial class StartupPasswordWindow : Window
    {
        public string InputPassword => txtPassword.Password;
        public StartupPasswordWindow()
        {
            InitializeComponent();
        }
        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputPassword))
            {
                MessageBox.Show("密码不能为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPassword.Focus();
                return;
            }
            bool result = PasswordHelper.VerifyOrSetStartupPassword(InputPassword);
            if (result)
            {
                KeyManager.SetPassword(InputPassword);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("密码错误！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 