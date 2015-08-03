using Microsoft.WindowsAPICodePack.Dialogs;
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
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        public SettingsDialog(string currentModelDirPath, string currentUser)
        {
            InitializeComponent();

            this.current_user.Text = currentUser;
            this.model_directory_path.Text = currentModelDirPath;
        }

        private void browse_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            CommonOpenFileDialog ofd = new CommonOpenFileDialog();

            ofd.IsFolderPicker = true;
            ofd.Multiselect = false;
            ofd.Title = ".../cavs/hse/data1/hfe/software/tagged_objects";

            // Get the selected file name and display in a TextBox 
            if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
            {
                while (!ofd.FileName.EndsWith("software\\ModelTaggingTool\\tagged_objects"))
                {
                    MessageBoxResult result = MessageBox.Show("Path must include the tagged_objects directory (cavs\\hse\\data1\\hfe\\software\\ModelTaggingTool\\tagged_objects). Please navigate to this directory.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (result == MessageBoxResult.OK)
                    {
                        if (ofd.ShowDialog() != CommonFileDialogResult.Ok)
                        {
                            return;
                        }
                        
                    }
                }

                this.model_directory_path.Text = ofd.FileName;

            }
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        public string CurrentUser
        {
            get { return this.current_user.Text; }
        }

        public string ModelDirectoryPath
        {
            get { return this.model_directory_path.Text; }
        }
    }
}
