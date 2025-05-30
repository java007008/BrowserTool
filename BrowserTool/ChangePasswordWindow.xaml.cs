using System.Windows;
using BrowserTool.Utils;

namespace BrowserTool
{
    public partial class ChangePasswordWindow : Window
    {
        public ChangePasswordWindow()
        {
            InitializeComponent();
        }
        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (!PasswordHelper.VerifyOrSetStartupPassword(txtOld.Password))
            {
                MessageBox.Show("旧密码错误！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                txtOld.Clear();
                txtOld.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(txtNew1.Password) || txtNew1.Password != txtNew2.Password)
            {
                MessageBox.Show("两次输入的新密码不一致或为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                txtNew1.Clear();
                txtNew2.Clear();
                txtNew1.Focus();
                return;
            }

            // 1. 用旧密码设置KeyManager
            KeyManager.SetPassword(txtOld.Password);
            var allGroups = Database.SiteConfig.GetAllGroups();
           

            // 2. 用新密码设置KeyManager
            KeyManager.SetPassword(txtNew1.Password);
            foreach (var group in allGroups)
            {
                foreach (var site in group.Sites)
                {
                    Database.SiteConfig.SaveSite(site);
                }
            }

            // 3. 保存新启动密码
            PasswordHelper.SaveStartupPassword(txtNew1.Password);
            MessageBox.Show("密码修改成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            

            // 4. 激活主窗口
            if (Owner != null)
            {
                Owner.Show();
                Owner.WindowState = WindowState.Normal;
                Owner.Activate();
            }

            DialogResult = true;
            Close();
            //Application.Current.Shutdown();
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 