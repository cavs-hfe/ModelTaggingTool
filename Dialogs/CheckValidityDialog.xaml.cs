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
    /// Interaction logic for CheckValidityDialog.xaml
    /// </summary>
    public partial class CheckValidityDialog : Window
    {
        MainViewModel mainViewModel;
        List<string> directoryList;
        List<string> databaseEntryList;

        public CheckValidityDialog(MainViewModel mvm, List<string> directoryList, List<string> databaseEntryList)
        {
            InitializeComponent();

            this.mainViewModel = mvm;
            this.directoryList = directoryList;
            directoryListView.ItemsSource = directoryList;
            this.databaseEntryList = databaseEntryList;
            databaseEntriesListView.ItemsSource = databaseEntryList;
        }

        private void btnDialogOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {

        }

        private void purgeDirectories_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult r = MessageBox.Show("Are you sure? This will delete all directories in this list. This action cannot be undone.", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (r.Equals(MessageBoxResult.Yes))
            {
                mainViewModel.purgeDirectories(directoryList);
            }
        }

        private void purgeDatabaseEntries_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult r = MessageBox.Show("Are you sure? This will delete all database entries in this list and any taggings if they exist. This action cannot be undone.", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
            if (r.Equals(MessageBoxResult.Yes))
            {
                mainViewModel.purgeDatabaseEntries(databaseEntryList);
            }
        }
    }
}
