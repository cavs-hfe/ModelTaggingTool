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

        /// <summary>
        /// Initializes a new instance of the <see cref="Window1"/> class.
        /// </summary>
        public Window1()
        {
            this.InitializeComponent();
            this.DataContext = new MainViewModel(new FileDialogService(), view1, tagTree, fileTree);
        }

        private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            resetModel();

            var viewport = (HelixViewport3D)sender;
            var firstHit = viewport.Viewport.FindHits(e.GetPosition(viewport)).FirstOrDefault();
            if (firstHit != null)
            {
                System.Console.WriteLine("Clicked object");

                var model = firstHit.Model as GeometryModel3D;

                //default state, add the green EM
                MaterialGroup mg = new MaterialGroup();
                mg.Children.Add(model.Material);
                mg.Children.Add(new EmissiveMaterial(new SolidColorBrush(Colors.DarkGreen)));
                model.Material = mg;

                MaterialGroup mgBack = new MaterialGroup();
                mgBack.Children.Add(model.BackMaterial);
                mgBack.Children.Add(new EmissiveMaterial(new SolidColorBrush(Colors.DarkGreen)));
                model.BackMaterial = mgBack;

                e.Handled = true;
            }
            else
            {
                System.Console.WriteLine("Didn't click object");
            }
        }

        private void resetMaterials()
        {
            Model3DGroup group = model1.Content as Model3DGroup;
            foreach (var v in group.Children)
            {
                GeometryModel3D g = v as GeometryModel3D;
                if (g.Material is MaterialGroup)
                {
                    var materialGroup = g.Material as MaterialGroup;

                    if (materialGroup.Children.Last() is EmissiveMaterial)
                    {
                        EmissiveMaterial em = materialGroup.Children.Last() as EmissiveMaterial;
                        if (em.Brush == new SolidColorBrush(Colors.DarkGreen))
                        {
                            MaterialGroup mg = new MaterialGroup();
                            foreach (Material m in materialGroup.Children)
                            {
                                if (m is EmissiveMaterial)
                                {
                                    mg.Children.Add(new EmissiveMaterial(new SolidColorBrush(Colors.White)));
                                }
                                else
                                {
                                    mg.Children.Add(m);
                                }
                            }

                            g.Material = mg;

                            /*MaterialGroup mgBack = new MaterialGroup();
                            mgBack.Children.Add(g.BackMaterial);
                            mgBack.Children.Add(new EmissiveMaterial(new SolidColorBrush(Colors.White)));
                            g.BackMaterial = mgBack;*/
                        }
                    }
                }
            }
        }

        private void resetModel()
        {
            MainViewModel mvm = this.DataContext as MainViewModel;
            mvm.resetModel();
        }

        private void OnItemMouseDoubleClick(object sender, MouseButtonEventArgs args)
        {
            //Console.WriteLine("double clicked");

            //make sure double click on same item
            if (sender is TreeViewItem)
            {
                //Console.WriteLine("double clicked on " + fileTree.SelectedItem);
                string filename = fileTree.SelectedItem.ToString();
                filename = filename.Substring(filename.IndexOf("(") + 1, filename.Length - filename.IndexOf("(") - 2);
                //Console.WriteLine("filename:" + filename);

                MainViewModel mvm = this.DataContext as MainViewModel;
                mvm.LoadModel(filename);
            }

        }

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
                        MainViewModel mvm = this.DataContext as MainViewModel;
                        mvm.updateParentTag((string)data.GetData(DataFormats.Text), source.Text);
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

        private void NewTagButton_Click(object sender, RoutedEventArgs e)
        {
            NewTagDialog ntd = new NewTagDialog();
            if (ntd.ShowDialog() == true)
            {
                MainViewModel mvm = this.DataContext as MainViewModel;
                mvm.addNewTag(ntd.TagName);
                mvm.refreshTagTree();
            }
        }

        private void NewModelButton_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel mvm = this.DataContext as MainViewModel;
            mvm.AddModel();
        }

        private void RefreshTags_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel mvm = this.DataContext as MainViewModel;
            mvm.refreshTagTree();
        }

        private void RefreshModels_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel mvm = this.DataContext as MainViewModel;
            mvm.refreshFileTree();
        }

        private void DeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (fileTree.SelectedItem != null)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to delete this model?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                {
                    MainViewModel mvm = this.DataContext as MainViewModel;
                    //TreeViewItem item = fileTree.SelectedItem as TreeViewItem;
                    //if (item != null)
                    //{
                    string filename = fileTree.SelectedItem as string;
                    mvm.deleteObject(filename.Substring(filename.IndexOf("(") + 1, filename.Length - filename.IndexOf("(") - 2));
                    //}
                    mvm.refreshFileTree();
                }
            }

        }

        private void DeleteTag_Click(object sender, RoutedEventArgs e)
        {
            if (tagTree.SelectedItem != null)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to delete this tag? All tags in the hierarchy below will be deleted as well.", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
                if (result == MessageBoxResult.Yes)
                {
                    MainViewModel mvm = this.DataContext as MainViewModel;
                    TreeViewItem item = tagTree.SelectedItem as TreeViewItem;
                    if (item != null)
                    {
                        mvm.deleteTag(item.Header as string);
                    }
                    mvm.refreshTagTree();
                }

            }

        }


    }


}