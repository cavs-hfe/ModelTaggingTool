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

namespace ModelViewer.Dialogs
{
    /// <summary>
    /// Interaction logic for EditPartDialog.xaml
    /// </summary>
    public partial class EditPartDialog : Window
    {
        public EditPartDialog(string text, string part)
        {
            InitializeComponent();
            this.instruction.Text = text;
            this.part_name.Text = part;
            this.part_name.Focus();
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        public string PartName
        {
            get { return part_name.Text.Replace(" ", "_"); }
        }
    }
}
