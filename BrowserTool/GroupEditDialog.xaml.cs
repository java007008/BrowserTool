using System.Windows;

namespace BrowserTool
{
    public partial class GroupEditDialog : Window
    {
        public string GroupName { get; set; }

        public GroupEditDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public GroupEditDialog(string groupName) : this()
        {
            GroupName = groupName;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GroupName))
            {
                MessageBox.Show("请输入分组名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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