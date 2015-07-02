// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Window1.xaml.cs" company="Helix Toolkit">
//   Copyright (c) 2014 Helix Toolkit contributors
// </copyright>
// <summary>
//   Interaction logic for Window1.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Media3D;

using HelixToolkit.Wpf;
using System.Windows.Media;
using System;
using System.Windows.Controls;
using ModelViewer.Dialogs;
using System.Collections.Generic;
using System.Windows.Documents;
using System.ComponentModel;

namespace ModelViewer
{
    /// <summary>
    /// Interaction logic for Window1.
    /// </summary>
    public partial class Window1
    {
        Point startPoint;
        bool isDragging = false;

        MainViewModel mainViewModel;

        private GridViewColumnHeader listViewSortCol = null;
        private SortAdorner listViewSortAdorner = null;


        /// <summary>
        /// Initializes a new instance of the <see cref="Window1"/> class.
        /// </summary>
        public Window1()
        {
            this.InitializeComponent();

            mainViewModel = new MainViewModel(new FileDialogService(), view1, tagTree);
            this.DataContext = mainViewModel;

            CategoryComboBox.ItemsSource = mainViewModel.getCategories();
        }

        #region Object Loading and Selection

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView lv = e.OriginalSource as ListView;

            if (lv.SelectedItems.Count > 1)
            {

            }
            else
            {
                ObjectFile of = lv.SelectedItem as ObjectFile;

                if (of != null)
                {
                    mainViewModel.LoadModel(of);
                    propertiesTabControl.IsEnabled = true;
                }
                else
                {
                    propertiesTabControl.IsEnabled = false;
                }
            }
            e.Handled = true;

        }

        private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            //mainViewModel.resetModel();

            var viewport = (HelixViewport3D)sender;
            var firstHit = viewport.Viewport.FindHits(e.GetPosition(viewport)).FirstOrDefault();
            if (firstHit != null)
            {
                System.Console.WriteLine("Clicked object");

                var model = firstHit.Model as GeometryModel3D;

                foreach (SubObject v in objectsList.Items)
                {
                    if (v.Name.Equals(model.GetName()))
                    {
                        objectsList.SelectedItem = v;
                        break;
                    }
                }

                e.Handled = true;
            }
            else
            {
                mainViewModel.resetModel(true);
                mainViewModel.refreshTagTree();
                System.Console.WriteLine("Didn't click object");
            }
        }

        private void objectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView lv = e.OriginalSource as ListView;
            SubObject so = lv.SelectedItem as SubObject;

            if (so != null)
            {
                mainViewModel.highlightObjectByName(so.Name);
                mainViewModel.showAssignedTags(so.Id);
            }


            e.Handled = true;
        }

        #endregion

        #region Drag and Drop

        //Drag and drop code from http://www.codeproject.com/Articles/55168/Drag-and-Drop-Feature-in-WPF-TreeView-Control
        private void OnPreviewLeftMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(tagTree);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !isDragging)
            {
                Point position = e.GetPosition(tagTree);

                if (Math.Abs(position.X - startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDrag(e);

                }
            }
        }

        private void StartDrag(MouseEventArgs e)
        {
            isDragging = true;
            TagViewModel source = ((CheckBox)e.OriginalSource).DataContext as TagViewModel;
            if (source != null)
            {
                DataObject data = new DataObject(System.Windows.DataFormats.Text.ToString(), source.Id.ToString());
                DragDropEffects de = DragDrop.DoDragDrop(tagTree, data, DragDropEffects.Move);
            }

            isDragging = false;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            IDataObject data = e.Data;

            if (data.GetDataPresent(DataFormats.Text))
            {
                TagViewModel tvm = ((TreeViewItem)sender).Header as TagViewModel;
                if (tvm != null)
                {
                    mainViewModel.updateParentTag(Convert.ToInt32(data.GetData(DataFormats.Text)), tvm.Id);
                }

            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            try
            {
                Point currentPosition = e.GetPosition(tagTree);

                if ((Math.Abs(currentPosition.X - startPoint.X) > 10.0) ||
                   (Math.Abs(currentPosition.Y - startPoint.Y) > 10.0))
                {
                    // Verify that this is a valid drop and then store the drop target
                    TreeViewItem item = sender as TreeViewItem;
                    if (item != null)
                    {
                        e.Effects = DragDropEffects.Move;
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
                e.Handled = true;
            }
            catch (Exception)
            {
            }
        }

        #endregion

        #region Tag Toolbar Button Handlers

        private void NewTagButton_Click(object sender, RoutedEventArgs e)
        {
            NewTagDialog ntd = new NewTagDialog();
            if (ntd.ShowDialog() == true)
            {
                mainViewModel.addNewTag(ntd.TagName);
                mainViewModel.refreshTagTree();
            }
        }

        private void RefreshTags_Click(object sender, RoutedEventArgs e)
        {
            mainViewModel.refreshTagTree();

            SubObject so = objectsList.SelectedItem as SubObject;
            if (so != null)
            {
                mainViewModel.showAssignedTags(so.Id);
            }
        }

        private void DeleteTag_Click(object sender, RoutedEventArgs e)
        {
            if (tagTree.SelectedItem != null)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to delete this tag? All tags in the hierarchy below will be deleted as well.", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                {
                    TreeViewItem item = tagTree.SelectedItem as TreeViewItem;
                    if (item != null)
                    {
                        mainViewModel.deleteTag(item.Header as string);
                    }
                    mainViewModel.refreshTagTree();
                }

            }

        }

        #endregion

        #region Model Toolbar Button Handlers

        private void NewModelButton_Click(object sender, RoutedEventArgs e)
        {
            mainViewModel.AddModel();
        }

        private void RefreshModels_Click(object sender, RoutedEventArgs e)
        {
            mainViewModel.refreshFileLists();
        }

        private void DeleteModel_Click(object sender, RoutedEventArgs e)
        {
            TabItem tc = filesTabControl.SelectedItem as TabItem;
            ListView lv = tc.Content as ListView;

            if (lv.SelectedItems.Count > 0)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to delete this model(s)?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                {
                    mainViewModel.deleteObjects(lv.SelectedItems);
                }
            }
            else
            {
                MessageBox.Show("You must select a file before you can assign it to yourself. Click a filename to select it and try again.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }

        private void AssignMeButton_Click(object sender, RoutedEventArgs e)
        {
            TabItem tc = filesTabControl.SelectedItem as TabItem;
            ListView lv = tc.Content as ListView;

            if (lv.SelectedItems.Count > 0)
            {
                mainViewModel.assignFiles(lv.SelectedItems);
            }
            else
            {
                MessageBox.Show("You must select a file before you can assign it to yourself. Click a filename to select it and try again.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }

        }

        private void AssignOtherButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> users = mainViewModel.getListOfUsers();
            AssignOtherDialog aod = new AssignOtherDialog(users);
            if (aod.ShowDialog() == true)
            {
                TabItem tc = filesTabControl.SelectedItem as TabItem;
                ListView lv = tc.Content as ListView;

                if (lv.SelectedItems.Count > 0)
                {
                    mainViewModel.assignFiles(lv.SelectedItems, aod.UserName);
                }
                else
                {
                    MessageBox.Show("You must select a file before you can assign it. Click a filename to select it and try again.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }

            }
        }

        private void ApproveButton_Click(object sender, RoutedEventArgs e)
        {
            TabItem tc = filesTabControl.SelectedItem as TabItem;
            ListView lv = tc.Content as ListView;
            ObjectFile of = lv.SelectedItem as ObjectFile;

            if (of != null)
            {
                mainViewModel.approveReview(of.FileId);
            }
            else
            {
                MessageBox.Show("You must select a file before you can approve it. Click a filename to select it and try again.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }

        private void MarkReadyButton_Click(object sender, RoutedEventArgs e)
        {
            TabItem tc = filesTabControl.SelectedItem as TabItem;
            ListView lv = tc.Content as ListView;
            ObjectFile of = lv.SelectedItem as ObjectFile;

            if (of != null)
            {
                mainViewModel.markFileComplete(of.FileId);
            }
            else
            {
                MessageBox.Show("You must select a file before you can mark it complete. Click a filename to select it and try again.", "No File Selected", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
        }

        #endregion

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            TagViewModel source = ((CheckBox)sender).DataContext as TagViewModel;
            source.IsChecked = true;
            SubObject so = objectsList.SelectedItem as SubObject;
            if (so != null && source != null)
            {
                mainViewModel.assignTagToObject(so.Id, source.Id);
            }

        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            TagViewModel source = ((CheckBox)sender).DataContext as TagViewModel;
            source.IsChecked = false;
            SubObject so = objectsList.SelectedItem as SubObject;
            if (so != null && source != null)
            {
                mainViewModel.unassignTagFromObject(so.Id, source.Id);
            }

        }

        private void mnuMarkReviewed_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu cm = ((MenuItem)sender).Parent as ContextMenu;
            TreeViewItem item = cm.PlacementTarget as TreeViewItem;
            TagViewModel tvm = item.Header as TagViewModel;
            SubObject so = objectsList.SelectedItem as SubObject;
            if (so != null && tvm != null)
            {
                mainViewModel.MarkTagAsReviewed(tvm.Id, so.Id);
            }

        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            mainViewModel.resetView();

            unassignedListView.UnselectAll();
            myFilesListView.UnselectAll();
            reviewListView.UnselectAll();
            approvedListView.UnselectAll();

            if (UnassignedTab.IsSelected)
            {
                ApproveButton.IsEnabled = false;
                TakeScreenshotButton.IsEnabled = false;
                MarkReadyButton.IsEnabled = false;
                AssignMeButton.IsEnabled = true;
                AssignOtherButton.IsEnabled = true;
                mainViewModel.IsTagTreeEnabled = false;
            }
            else if (MyFilesTab.IsSelected)
            {
                ApproveButton.IsEnabled = false;
                TakeScreenshotButton.IsEnabled = true;
                MarkReadyButton.IsEnabled = true;
                AssignMeButton.IsEnabled = false;
                AssignOtherButton.IsEnabled = true;
                mainViewModel.IsTagTreeEnabled = true;
            }
            else if (ReviewTab.IsSelected)
            {
                ApproveButton.IsEnabled = true;
                TakeScreenshotButton.IsEnabled = false;
                MarkReadyButton.IsEnabled = false;
                AssignMeButton.IsEnabled = true;
                AssignOtherButton.IsEnabled = true;
                mainViewModel.IsTagTreeEnabled = false;
            }
            else if (ApprovedTab.IsSelected)
            {
                ApproveButton.IsEnabled = false;
                TakeScreenshotButton.IsEnabled = false;
                MarkReadyButton.IsEnabled = false;
                AssignMeButton.IsEnabled = true;
                AssignOtherButton.IsEnabled = true;
                mainViewModel.IsTagTreeEnabled = false;
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            /*GridViewColumnHeader column = (sender as GridViewColumnHeader);
            string sortBy = column.Tag.ToString();

            ListView toSort = null;
            //if (sortBy.StartsWith("Un"))
            //{
                toSort = unassignedListView;
            //}

            if (toSort != null)
            {
                if (listViewSortCol != null)
                {
                    AdornerLayer.GetAdornerLayer(listViewSortCol).Remove(listViewSortAdorner);
                    toSort.Items.SortDescriptions.Clear();
                }

                ListSortDirection newDir = ListSortDirection.Ascending;
                if (listViewSortCol == column && listViewSortAdorner.Direction == newDir)
                    newDir = ListSortDirection.Descending;

                listViewSortCol = column;
                listViewSortAdorner = new SortAdorner(listViewSortCol, newDir);
                AdornerLayer.GetAdornerLayer(listViewSortCol).Add(listViewSortAdorner);
                toSort.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
            }*/

        }

        private void TakeScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            mainViewModel.FileSaveScreenshot();
        }

        private void propertiesTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void TextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb != null)
            {
                mainViewModel.setFileFriendlyName(mainViewModel.ActiveFile.FileName, tb.Text);
            }

        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryComboBox.SelectedItem != null && mainViewModel != null && !mainViewModel.ActiveFile.Category.Equals(CategoryComboBox.SelectedItem.ToString()))
            {
                mainViewModel.setCategory(mainViewModel.ActiveFile.FileId, CategoryComboBox.SelectedItem.ToString());
            }

        }
        
        private void CategoryComboBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (CategoryComboBox.Text != "" && mainViewModel != null)
            {
                mainViewModel.setCategory(mainViewModel.ActiveFile.FileId, CategoryComboBox.Text);
                CategoryComboBox.ItemsSource = mainViewModel.getCategories();
            }
        }

        private void ShadowsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShadowsComboBox.SelectedItem != null && mainViewModel != null)
            {
                mainViewModel.setShadows(mainViewModel.ActiveFile.FileId, ShadowsComboBox.SelectedIndex);
            }
        }

        private void ZUpComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZUpComboBox.SelectedItem != null && mainViewModel != null)
            {
                mainViewModel.setZUp(mainViewModel.ActiveFile.FileId, ZUpComboBox.SelectedIndex);
            }
        }

        private void PhysicsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PhysicsComboBox.SelectedItem != null && mainViewModel != null)
            {
                mainViewModel.setPhysicsGeometry(mainViewModel.ActiveFile.FileId, PhysicsComboBox.SelectedIndex);
            }
        }

        private void FileComments_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            mainViewModel.setComments(mainViewModel.ActiveFile.FileId, FileComments.Text);
        }  
    }

    public class SortAdorner : Adorner
    {
        private static Geometry ascGeometry =
                Geometry.Parse("M 0 4 L 3.5 0 L 7 4 Z");

        private static Geometry descGeometry =
                Geometry.Parse("M 0 0 L 3.5 4 L 7 0 Z");

        public ListSortDirection Direction { get; private set; }

        public SortAdorner(UIElement element, ListSortDirection dir)
            : base(element)
        {
            this.Direction = dir;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (AdornedElement.RenderSize.Width < 20)
                return;

            TranslateTransform transform = new TranslateTransform
                    (
                            AdornedElement.RenderSize.Width - 15,
                            (AdornedElement.RenderSize.Height - 5) / 2
                    );
            drawingContext.PushTransform(transform);

            Geometry geometry = ascGeometry;
            if (this.Direction == ListSortDirection.Descending)
                geometry = descGeometry;
            drawingContext.DrawGeometry(Brushes.Black, null, geometry);

            drawingContext.Pop();
        }
    }


}