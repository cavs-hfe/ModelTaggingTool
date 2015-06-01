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

        /// <summary>
        /// Initializes a new instance of the <see cref="Window1"/> class.
        /// </summary>
        public Window1()
        {
            this.InitializeComponent();
            mainViewModel = new MainViewModel(new FileDialogService(), view1, tagTree, fileTree, objectsTree);
            this.DataContext = mainViewModel;
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

                mainViewModel.selectSubObjectTreeItemByName(model.GetName());

                e.Handled = true;
            }
            else
            {
                System.Console.WriteLine("Didn't click object");
            }
        }

        private void OnItemMouseDoubleClick(object sender, MouseButtonEventArgs args)
        {
            //make sure double click on same item
            if (sender is TreeViewItem)
            {
                TreeViewItem item = sender as TreeViewItem;
                ObjectFileViewModel ofvm = item.Header as ObjectFileViewModel;
                string filename = ofvm.FileName;

                if (!filename.Equals(""))
                {
                    mainViewModel.LoadModel(filename);
                }

            }

        }

        private void OnSubObjectItemSelected(object sender, RoutedEventArgs args)
        {
            //highlight subobject
            TreeViewItem source = args.OriginalSource as TreeViewItem;
            SubObjectViewModel subObjectView = source.Header as SubObjectViewModel;
            string subName = subObjectView.Name;

            mainViewModel.highlightObjectByName(subName);

            mainViewModel.showAssignedTags(subName);

            args.Handled = true;
        }

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
                Point position = e.GetPosition(null);

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
            TagViewModel selected = tagTree.SelectedItem as TagViewModel;
            if (tagTree.SelectedItem != null)
            {
                DataObject data = new DataObject(System.Windows.DataFormats.Text.ToString(), selected.Name);
                DragDropEffects de = DragDrop.DoDragDrop(tagTree, data, DragDropEffects.Move);
            }

            isDragging = false;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            IDataObject data = e.Data;

            if (data.GetDataPresent(DataFormats.Text))
            {

                TextBlock source = e.OriginalSource as TextBlock;
                if (source != null)
                {
                    if (!((string)data.GetData(DataFormats.Text)).Equals(source.Text))
                    {
                        mainViewModel.updateParentTag((string)data.GetData(DataFormats.Text), source.Text);
                    }
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

            mainViewModel.showAssignedTags();
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
            mainViewModel.refreshFileTree();
        }

        private void DeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (fileTree.SelectedItem != null)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to delete this model?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                {
                    ObjectFileViewModel ofvm = fileTree.SelectedItem as ObjectFileViewModel;
                    mainViewModel.deleteObject(ofvm.FileName);

                    mainViewModel.refreshFileTree();
                }
            }

        }

        #endregion

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            TagViewModel source = ((CheckBox)sender).DataContext as TagViewModel;
            mainViewModel.assignTagToObject(source.Id);
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            TagViewModel source = ((CheckBox)sender).DataContext as TagViewModel;
            mainViewModel.unassignTagFromObject(source.Id);
        }

        private void mnuMarkReviewed_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu cm = ((MenuItem)sender).Parent as ContextMenu;
            TreeViewItem item = cm.PlacementTarget as TreeViewItem;
            TagViewModel tvm = item.Header as TagViewModel;

            mainViewModel.MarkTagAsReviewed(tvm.Id);
        }

    }


}