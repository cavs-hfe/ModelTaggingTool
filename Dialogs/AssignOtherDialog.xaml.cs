using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ModelViewer.Dialogs
{
    /// <summary>
    /// Interaction logic for AssignOtherDialog.xaml
    /// </summary>
    public partial class AssignOtherDialog : Window
    {
        public AssignOtherDialog(List<string> usernames)
        {
            InitializeComponent();

            if (usernames != null)
            {
                user_name.ItemsSource = usernames;
            }
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        public string UserName
        {
            get { return this.user_name.Text; }
        }
    }
}
