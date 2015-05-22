using System;
using System.Collections.Generic;
using System.IO;
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


namespace ModelViewer
{
    /// <summary>
    /// Interaction logic for Window2.xaml
    /// </summary>
    public partial class MissingFileDialog : Window
    {
        private string path;

        public MissingFileDialog(string instruction, string path)
        {
            InitializeComponent();

            this.path = path;
            this.instruction.Text = instruction;
            this.file_path.Text = path;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = Path.GetExtension(path);
            dlg.Filter = Path.GetFileName(path) + "|*" + Path.GetExtension(path);
            dlg.FileName = Path.GetFileName(path);
            dlg.Multiselect = false;
            dlg.Title = Path.GetFileName(path);

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                this.file_path.Text = dlg.FileName;
            }
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        public string FilePath
        {
            get { return this.file_path.Text; }
        }
    }
}
