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
using System.Windows.Shapes;

namespace ModelViewer
{
    /// <summary>
    /// Interaction logic for NewTagDialog.xaml
    /// </summary>
    public partial class NewTagDialog : Window
    {
        public NewTagDialog()
        {
            InitializeComponent();
            tag_name.Focus();
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        public string TagName
        {
            get { return tag_name.Text; }
        }
    }
}
