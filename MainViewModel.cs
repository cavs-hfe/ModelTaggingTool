// --------------------------------------------------------------------------------------------------------------------
// Based on code provided in Helix Toolkit example ModelViewer
// --------------------------------------------------------------------------------------------------------------------

namespace ModelViewer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media.Media3D;
    using System.Windows.Threading;

    using HelixToolkit.Wpf;
    using System.Windows.Controls;
    using MySql.Data.MySqlClient;
    using System.Data;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Collections.ObjectModel;
    using ModelViewer.Importer;
    using System.Windows.Media.Imaging;
    using System.Windows.Media;


    public class MainViewModel : Observable
    {
        private const string OpenFileFilter = "3D model files (*.3ds;*.obj;*.lwo;*.stl)|*.3ds;*.obj;*.objz;*.lwo;*.stl";

        private const string TitleFormatString = "3D Model Tagging Tool - {0}";

        private readonly IFileDialogService fileDialogService;

        private readonly IHelixViewport3D viewport;

        private readonly Dispatcher dispatcher;

        private string currentModelPath;

        private string applicationTitle;

        private double expansion;

        private Model3D currentModel;

        private TreeView tagTree;

        private TreeView fileTree;

        private TreeView subObjectTree;

        private MySqlConnection sqlConnection;

        private static string modelDirectory = "I:\\projects\\ERS\\Task-04\\tagging\\tagged_objects\\";

        private static string currentUser;

        private TagViewModel rootTagView;

        private ObjectFileViewModel rootObjectFileView;

        private SubObjectViewModel rootSubObjectView;

        public MainViewModel(IFileDialogService fds, HelixViewport3D viewport, TreeView tagTree, TreeView fileTree, TreeView subObjectTree)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException("viewport");
            }
            this.tagTree = tagTree;
            this.fileTree = fileTree;
            this.subObjectTree = subObjectTree;
            this.dispatcher = Dispatcher.CurrentDispatcher;
            this.Expansion = 1;
            this.fileDialogService = fds;
            this.viewport = viewport;
            this.FileOpenCommand = new DelegateCommand(this.FileOpen);
            this.FileExportCommand = new DelegateCommand(this.FileExport);
            this.FileSaveScreenshotCommand = new DelegateCommand(this.FileSaveScreenshot);
            this.FileExitCommand = new DelegateCommand(FileExit);
            this.ViewZoomExtentsCommand = new DelegateCommand(this.ViewZoomExtents);
            this.EditSettingsCommand = new DelegateCommand(this.Settings);
            this.ApplicationTitle = "3D Model Tagging Tool";

            sqlConnection = new MySqlConnection("server=Mitchell.HPC.MsState.Edu; database=cavs_ivp04;Uid=cavs_ivp04_user;Pwd=TLBcEsm7;");

            try
            {
                sqlConnection.Open();

                refreshTagTree();

                refreshFileTree();
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Error connecting to database: " + e.Message + "\n" + e.StackTrace);
            }
        }

        #region Tag Code

        public void refreshTagTree()
        {

            Tag rootTag = new Tag(18, "Tags:", -1);
            rootTag = PopulateRootTag(rootTag);
            rootTagView = new TagViewModel(rootTag);

            this.RaisePropertyChanged("FirstGeneration");

            rootTagView.ExpandAll();

        }


        private Tag PopulateRootTag(Tag parentTag)
        {
            string query = "SELECT * FROM Tags WHERE parent = " + parentTag.Id;
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            foreach (DataRow row in table.Rows)
            {
                Tag tag = new Tag(Convert.ToInt32(row["tag_id"]), row["tag_name"].ToString(), Convert.ToInt32(row["parent"]));
                PopulateRootTag(tag);
                parentTag.Children.Add(tag);
            }

            return parentTag;
        }

        public void addNewTag(string tag)
        {
            string query = "INSERT INTO Tags (tag_name) VALUES ('" + tag + "');";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        public void deleteTag(string tag)
        {
            string query = "DELETE FROM Tags WHERE tag_name = '" + tag + "';";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        public void updateParentTag(string child, string newParent)
        {
            string query = "SELECT tag_id FROM Tags WHERE tag_name = '" + newParent + "';";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            MySqlDataReader reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                int id = Convert.ToInt32(reader["tag_id"]);
                reader.Close();

                string updateQuery = "UPDATE Tags SET parent = " + id + " WHERE tag_name='" + child + "';";
                MySqlCommand updateCmd = new MySqlCommand(updateQuery, sqlConnection);
                updateCmd.ExecuteNonQuery();
            }

            reader.Close();

            refreshTagTree();
        }

        #endregion

        #region Object Code

        public void refreshFileTree()
        {
            //get files
            string query = "SELECT * FROM Objects";
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            ObjectFile rootFile = new ObjectFile("", "Objects:");

            foreach (DataRow row in table.Rows)
            {
                rootFile.Children.Add(new ObjectFile((string)row["file_name"], (string)row["friendly_name"]));
            }

            rootObjectFileView = new ObjectFileViewModel(rootFile);

            this.RaisePropertyChanged("ObjectFiles");

            rootTagView.ExpandAll();
        }

        #region Add Model

        public async void AddModel()
        {
            string objectFile = this.fileDialogService.OpenFileDialog("models", null, OpenFileFilter, ".obj"); ;
            //copy model files into new directory
            CopyFiles(objectFile);

            //add to database
            addModelToDatabase(Path.GetFileName(objectFile));

            refreshFileTree();

            this.CurrentModelPath = modelDirectory + Path.GetFileName(objectFile);
            this.CurrentModel = await this.LoadAsync(this.CurrentModelPath, false);
            this.ApplicationTitle = string.Format(TitleFormatString, this.CurrentModelPath);
            this.viewport.ZoomExtents(0);

            displaySubObjects();
        }

        private void CopyFiles(string path)
        {

            //check to see if file exists
            if (File.Exists(modelDirectory + Path.GetFileName(path)))
            {
                //ask user what to do, overwrite or ignore
                return;
            }

            //copy object file
            File.Copy(path, modelDirectory + Path.GetFileName(path));

            //copy material files
            List<string> mtlFiles = FindMtlFiles(modelDirectory + Path.GetFileName(path));
            foreach (string s in mtlFiles)
            {
                if (File.Exists(s)) //mtl files stored as full path, just copy the file
                {
                    File.Copy(s, modelDirectory + Path.GetFileName(s));
                    List<string> assets = findAssets(modelDirectory + Path.GetFileName(s));
                    foreach (string a in assets)
                    {
                        if (File.Exists(a) && !File.Exists(modelDirectory + Path.GetFileName(a)))
                        {
                            File.Copy(a, modelDirectory + Path.GetFileName(a));
                        }
                        else if (File.Exists(Path.GetDirectoryName(path) + "\\" + a) && !File.Exists(modelDirectory + a))
                        {
                            File.Copy(Path.GetDirectoryName(path) + "\\" + a, modelDirectory + a);
                        }
                        else if (!File.Exists(modelDirectory + a))
                        {
                            //open dialog prompting for file location
                            MissingFileDialog mfd = new MissingFileDialog("Cannot find file: " + a + ". Please navigate to file.", Path.GetDirectoryName(path) + "\\" + a);
                            if (mfd.ShowDialog() == true && !File.Exists(modelDirectory + a))
                            {
                                File.Copy(mfd.FilePath, modelDirectory + a);
                            }
                        }
                    }
                }
                else if (File.Exists(Path.GetDirectoryName(path) + "\\" + s) && !File.Exists(modelDirectory + s))
                {
                    File.Copy(Path.GetDirectoryName(path) + "\\" + s, modelDirectory + s);
                    List<string> assets = findAssets(modelDirectory + s);
                    foreach (string a in assets)
                    {
                        if (File.Exists(a) && !File.Exists(modelDirectory + Path.GetFileName(a)))
                        {
                            File.Copy(a, modelDirectory + Path.GetFileName(a));
                        }
                        else if (File.Exists(Path.GetDirectoryName(path) + "\\" + a) && !File.Exists(modelDirectory + a))
                        {
                            File.Copy(Path.GetDirectoryName(path) + "\\" + a, modelDirectory + a);
                        }
                        else if (!File.Exists(modelDirectory + a))
                        {
                            //open dialog prompting for file location
                            MissingFileDialog mfd = new MissingFileDialog("Cannot find file: " + a + ". Please navigate to file.", Path.GetDirectoryName(path) + "\\" + a);
                            if (mfd.ShowDialog() == true && !File.Exists(modelDirectory + a))
                            {
                                File.Copy(mfd.FilePath, modelDirectory + a);
                            }
                        }
                    }
                }
                else if (!File.Exists(Path.GetDirectoryName(path) + "\\" + s))
                {
                    //open dialog prompting for file location
                    MissingFileDialog mfd = new MissingFileDialog("Cannot find file: " + s + ". Please navigate to file.", Path.GetDirectoryName(path) + "\\" + s);
                    if (mfd.ShowDialog() == true)
                    {
                        File.Copy(mfd.FilePath, modelDirectory + s);
                    }
                }
            }

        }

        private List<string> FindMtlFiles(string path)
        {
            List<string> files = new List<string>();
            string line;

            // Read the file and display it line by line.
            System.IO.StreamReader file = new System.IO.StreamReader(path);
            while ((line = file.ReadLine()) != null)
            {
                if (line.StartsWith("mtllib"))
                {
                    files.Add(line.Substring(7));
                }
            }

            file.Close();


            return files;
        }

        private List<string> findAssets(string path)
        {
            List<string> files = new List<string>();
            string line;

            // Read the file and display it line by line.
            System.IO.StreamReader file = new System.IO.StreamReader(path);
            while ((line = file.ReadLine()) != null)
            {
                if (line.Contains("map_Ka") || line.Contains("map_Kd") || line.Contains("map_Ks") ||
                    line.Contains("map_Ns") || line.Contains("map_d") || line.Contains("map_bump") ||
                    line.Contains("bump") || line.Contains("disp") || line.Contains("decal"))
                {
                    Match m = Regex.Match(line, @"\S+\.\S+");
                    if (m.Success)
                    {
                        files.Add(m.Value);
                    }
                }
            }

            file.Close();


            return files;
        }

        private void addModelToDatabase(string filename)
        {
            string query = "INSERT INTO Objects (file_name, friendly_name) VALUES ('" + filename + "', 'object');";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        #endregion

        public async void LoadModel(string filename)
        {
            this.CurrentModelPath = modelDirectory + filename;
            this.CurrentModel = await this.LoadAsync(this.CurrentModelPath, false);
            this.ApplicationTitle = string.Format(TitleFormatString, this.CurrentModelPath);
            this.viewport.ZoomExtents(0);

            //populate sub object display
            displaySubObjects();
        }

        private async Task<Model3DGroup> LoadAsync(string model3DPath, bool freeze)
        {
            return await Task.Factory.StartNew(() =>
            {
                var mi = new CAVS_ModelImporter();

                if (freeze)
                {
                    // Alt 1. - freeze the model 
                    return mi.Load(model3DPath, null, true);
                }

                // Alt. 2 - create the model on the UI dispatcher
                return mi.Load(model3DPath, this.dispatcher);
            });
        }

        private Model3DGroup Load(string model3DPath, bool freeze)
        {
            var mi = new CAVS_ModelImporter();

            if (freeze)
            {
                // Alt 1. - freeze the model 
                return mi.Load(model3DPath, null, true);
            }

            // Alt. 2 - create the model on the UI dispatcher
            return mi.Load(model3DPath, this.dispatcher);

        }

        public void deleteObject(string fileName)
        {
            string query = "DELETE FROM Objects WHERE file_name = '" + fileName + "';";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();

            //TODO: delete files too
        }

        private void displaySubObjects()
        {
            SubObject root = new SubObject("Sub-Objects:");
            foreach (GeometryModel3D gm in (CurrentModel as Model3DGroup).Children)
            {
                SubObject tag = new SubObject(gm.GetName());
                root.Children.Add(tag);
            }

            rootSubObjectView = new SubObjectViewModel(root);

            this.RaisePropertyChanged("SubObjects");

            rootSubObjectView.ExpandAll();
        }

        public void highlightObjectByName(string name)
        {
            resetModel();

            foreach (GeometryModel3D gm in (CurrentModel as Model3DGroup).Children)
            {
                if (gm.GetName().Equals(name))
                {


                    MaterialGroup mg = new MaterialGroup();
                    mg.Children.Add(gm.Material);
                    mg.Children.Add(new EmissiveMaterial(new SolidColorBrush(Colors.DarkGreen)));
                    gm.Material = mg;

                    MaterialGroup mgBack = new MaterialGroup();
                    mgBack.Children.Add(gm.BackMaterial);
                    mgBack.Children.Add(new EmissiveMaterial(new SolidColorBrush(Colors.DarkGreen)));
                    gm.BackMaterial = mgBack;
                }
            }
        }

        public void resetModel()
        {
            this.CurrentModel = this.Load(this.CurrentModelPath, false);
        }     

        #endregion

        #region Property Code

        /// <summary>
        /// Returns a read-only collection containing the first person 
        /// in the family tree, to which the TreeView can bind.
        /// </summary>
        public ReadOnlyCollection<TagViewModel> FirstGeneration
        {
            get { return new ReadOnlyCollection<TagViewModel>(new TagViewModel[] { rootTagView }); }
        }

        public ReadOnlyCollection<SubObjectViewModel> SubObjects
        {
            get { return new ReadOnlyCollection<SubObjectViewModel>(new SubObjectViewModel[] { rootSubObjectView }); }
        }

        public ReadOnlyCollection<ObjectFileViewModel> ObjectFiles
        {
            get { return new ReadOnlyCollection<ObjectFileViewModel>(new ObjectFileViewModel[] { rootObjectFileView }); }
        }


        public string ModelDirectory
        {
            get
            {
                return modelDirectory;
            }
            set
            {
                modelDirectory = value;
                this.RaisePropertyChanged("ModelDirectory");
            }
        }

        public string CurrentUser
        {
            get
            {
                return currentUser;
            }
            set
            {
                currentUser = value;
                this.RaisePropertyChanged("CurrentUser");
            }
        }

        public string CurrentModelPath
        {
            get
            {
                return this.currentModelPath;
            }

            set
            {
                this.currentModelPath = value;
                this.RaisePropertyChanged("CurrentModelPath");
            }
        }

        public string ApplicationTitle
        {
            get
            {
                return this.applicationTitle;
            }

            set
            {
                this.applicationTitle = value;
                this.RaisePropertyChanged("ApplicationTitle");
            }
        }

        public double Expansion
        {
            get
            {
                return this.expansion;
            }

            set
            {
                if (!this.expansion.Equals(value))
                {
                    this.expansion = value;
                    this.RaisePropertyChanged("Expansion");
                }
            }
        }

        public Model3D CurrentModel
        {
            get
            {
                return this.currentModel;
            }

            set
            {
                this.currentModel = value;
                this.RaisePropertyChanged("CurrentModel");
            }
        }

        #endregion

        #region Menu Commands

        public ICommand FileOpenCommand { get; set; }

        public ICommand FileExportCommand { get; set; }

        public ICommand FileSaveScreenshotCommand { get; set; }

        public ICommand FileExitCommand { get; set; }

        public ICommand HelpAboutCommand { get; set; }

        public ICommand ViewZoomExtentsCommand { get; set; }

        public ICommand EditSettingsCommand { get; set; }

        private static void FileExit()
        {
            Application.Current.Shutdown();
        }

        private void FileExport()
        {
            var path = this.fileDialogService.SaveFileDialog(null, null, Exporters.Filter, ".png");
            if (path == null)
            {
                return;
            }

            this.viewport.Export(path);
        }

        private void Settings()
        {
            SettingsDialog sd = new SettingsDialog(modelDirectory, currentUser);
            if (sd.ShowDialog() == true)
            {
                this.CurrentUser = sd.CurrentUser;
                modelDirectory = sd.ModelDirectoryPath;
            }
        }

        private void ViewZoomExtents()
        {
            this.viewport.ZoomExtents(500);
        }

        private async void FileOpen()
        {
            this.CurrentModelPath = this.fileDialogService.OpenFileDialog("models", null, OpenFileFilter, ".obj");
            this.CurrentModel = await this.LoadAsync(this.CurrentModelPath, false);
            this.ApplicationTitle = string.Format(TitleFormatString, this.CurrentModelPath);
            this.viewport.ZoomExtents(0);
        }

        private void FileSaveScreenshot()
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap((int)this.viewport.Viewport.ActualWidth, (int)this.viewport.Viewport.ActualHeight, 96, 96, PixelFormats.Pbgra32);

            System.Windows.Shapes.Rectangle vRect = new System.Windows.Shapes.Rectangle();

            vRect.Width = (int)this.viewport.Viewport.ActualWidth;

            vRect.Height = (int)this.viewport.Viewport.ActualHeight;

            vRect.Fill = Brushes.Black;

            vRect.Arrange(new Rect(0, 0, vRect.Width, vRect.Height));

            bmp.Render(vRect);

            bmp.Render(this.viewport.Viewport);

            PngBitmapEncoder png = new PngBitmapEncoder();

            png.Frames.Add(BitmapFrame.Create(bmp));

            using (Stream stm = File.Create(modelDirectory + Path.GetFileNameWithoutExtension(this.CurrentModelPath) + ".png"))
            {

                png.Save(stm);

            }
        }

        #endregion

        public void selectSubObjectTreeItemByName(string name)
        {
            foreach (var i in rootSubObjectView.Children)
            {
                if (i.Name.Equals(name))
                {
                    i.IsSelected = true;
                }
            }
        }
    }
}