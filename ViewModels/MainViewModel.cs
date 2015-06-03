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
    using ModelViewer.Dialogs;


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

        private static string modelDirectory = "";

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
            this.HelpAboutCommand = new DelegateCommand(this.HelpAbout);
            this.RenameObjectCommand = new DelegateCommand(this.RenameObject);
            this.MarkTagAsReviewedCommand = new DelegateCommand(this.MarkTagAsReviewed);
            this.ApplicationTitle = "3D Model Tagging Tool";

            CurrentUser = Properties.Settings.Default.CurrentUser;
            ModelDirectory = Properties.Settings.Default.ModelDirectoryPath;

            //check to see if the model directory path is set correctly
            if (!Directory.Exists(ModelDirectory))
            {
                MessageBox.Show("Model directory not set correctly. Please set the correct path to the model directory.", "Invalid Model Directory Path", MessageBoxButton.OK, MessageBoxImage.Error);
                Settings();
            }

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

        #region Tag Tree Methods

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

        #endregion

        public void addNewTag(string tag)
        {
            string query = "INSERT INTO Tags (tag_name, parent) VALUES ('" + tag + "', 18);";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        public void deleteTag(string tag)
        {
            string query = "DELETE FROM Tags WHERE tag_name = '" + tag + "';";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        public void updateParentTag(int child, int newParent)
        {
            if (child != newParent && !isTagChild(newParent, child))
            {
                string updateQuery = "UPDATE Tags SET parent = " + newParent + " WHERE tag_id=" + child + ";";
                MySqlCommand updateCmd = new MySqlCommand(updateQuery, sqlConnection);
                updateCmd.ExecuteNonQuery();

                refreshTagTree();
            }
        }

        /// <summary>
        /// Method to see if tag 1 is a child of tag 2. Used to validate before updating tag parent.
        /// </summary>
        /// <param name="tag1"></param>
        /// <param name="tag2"></param>
        /// <returns></returns>
        private bool isTagChild(int tag1, int tag2)
        {
            bool isChild = false;

            string query = "SELECT tag_id FROM Tags WHERE parent = " + tag2;
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            foreach (DataRow row in table.Rows)
            {
                if (Convert.ToInt32(row["tag_id"]) == tag1)
                {
                    isChild = true;
                    break;
                }
                isChild = isTagChild(tag1, Convert.ToInt32(row["tag_id"]));
            }


            return isChild;
        }

        #region Assign Tag

        public void assignTagToObject(int tagID)
        {
            //if an object has been loaded
            if (rootSubObjectView != null)
            {
                //check to see what object selected
                SubObjectViewModel s = rootSubObjectView.GetSelectedItem();
                if (s != null && !s.Name.Equals("Objects:"))
                {
                    int fileId = getFileIdByFileName(this.CurrentModelPath);
                    if (fileId != -1)
                    {
                        int objectId = getObjectId(fileId, s.Name);
                        if (objectId != -1)
                        {
                            assignTagToObject(tagID, objectId);
                        }
                    }
                }
            }

            showAssignedTags();

        }

        public void assignTagToObject(int tagID, int objectID)
        {
            try
            {
                string query = "INSERT INTO Object_Tag (object_id, tag_id, tagged_by, reviewed) VALUES (" + objectID + ", " + tagID + ", '" + CurrentUser + "', 0)";
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {

            }

        }

        public void unassignTagFromObject(int tagID)
        {
            //if an object has been loaded
            if (rootSubObjectView != null)
            {
                //check to see what object selected
                SubObjectViewModel s = rootSubObjectView.GetSelectedItem();
                if (s != null && !s.Name.Equals("Objects:"))
                {
                    int fileId = getFileIdByFileName(this.CurrentModelPath);
                    if (fileId != -1)
                    {
                        int objectId = getObjectId(fileId, s.Name);
                        if (objectId != -1)
                        {
                            unassignTagFromObject(tagID, objectId);
                        }
                    }
                }
            }
        }

        public void unassignTagFromObject(int tagID, int objectID)
        {
            try
            {
                string query = "DELETE FROM Object_Tag WHERE object_id = " + objectID + " AND tag_id = " + tagID;
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {

            }

        }

        #endregion

        public void showAssignedTags()
        {
            //get currently selected object
            //if an object has been loaded
            if (rootSubObjectView != null)
            {
                //check to see what object selected
                SubObjectViewModel s = rootSubObjectView.GetSelectedItem();
                if (s != null && !s.Name.Equals("Objects:"))
                {
                    showAssignedTags(s.Name);
                }
            }
        }

        public void showAssignedTags(string objectName)
        {
            refreshTagTree();

            //get tags for object
            int fileId = getFileIdByFileName(this.CurrentModelPath);
            int objectId = getObjectId(fileId, objectName);

            string query = "SELECT tag_name, reviewed FROM Object_Tag, Tags WHERE object_id = " + objectId + " AND Object_Tag.tag_id = Tags.tag_id;";
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            foreach (DataRow row in table.Rows)
            {
                //if the root tag is selected, ignore
                if (!row["tag_name"].Equals("root-tag"))
                {
                    TagViewModel tvm = rootTagView.GetTagViewModelByName((string)row["tag_name"]);
                    tvm.IsChecked = true;
                    if (Convert.ToInt32(row["reviewed"]) == 0)
                    {
                        tvm.IsReviewed = false;
                    }
                }

            }

        }

        #endregion

        #region Object Code

        public void refreshFileTree()
        {
            //get files
            string query = "SELECT * FROM Files";
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            ObjectFile rootFile = new ObjectFile("", "Files:");

            foreach (DataRow row in table.Rows)
            {
                rootFile.Children.Add(new ObjectFile((string)row["file_name"], (string)row["friendly_name"]));
            }

            rootObjectFileView = new ObjectFileViewModel(rootFile);

            this.RaisePropertyChanged("ObjectFiles");

            rootTagView.ExpandAll();
        }

        #region Add Model

        public void AddModel()
        {
            string objectFile = this.fileDialogService.OpenFileDialog("models", null, OpenFileFilter, ".obj");
            //copy model files into new directory
            if (objectFile != null)
            {
                CopyFiles(objectFile);

                //add to database
                addModelToDatabase(Path.GetFileName(objectFile));

                refreshFileTree();

                LoadModel(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(objectFile), Path.GetFileName(objectFile)));
            }

        }

        private void CopyFiles(string path)
        {
            string resourceDir = Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(path));


            //check to see if model directory exists
            if (Directory.Exists(resourceDir))
            {
                //ask user what to do, overwrite or ignore
                return;
            }

            //create path for resources
            Directory.CreateDirectory(resourceDir);

            //copy object file
            File.Copy(path, Path.Combine(resourceDir, Path.GetFileName(path)));

            //copy material files
            List<string> mtlFiles = FindMtlFiles(Path.Combine(resourceDir, Path.GetFileName(path)));
            foreach (string s in mtlFiles)
            {
                if (File.Exists(s)) //mtl files stored as full path, just copy the file
                {
                    File.Copy(s, Path.Combine(resourceDir, Path.GetFileName(s)));
                    List<string> assets = findAssets(s);
                    foreach (string a in assets)
                    {
                        if (File.Exists(a) && !File.Exists(Path.Combine(resourceDir, Path.GetFileName(a))))
                        {
                            File.Copy(a, Path.Combine(resourceDir, Path.GetFileName(a)));
                        }
                        else if (File.Exists(Path.GetDirectoryName(path) + "\\" + a) && !File.Exists(Path.Combine(resourceDir, a)))
                        {
                            File.Copy(Path.GetDirectoryName(path) + "\\" + a, Path.Combine(resourceDir, a));
                        }
                        else if (!File.Exists(Path.Combine(resourceDir, a)))
                        {
                            //open dialog prompting for file location
                            MissingFileDialog mfd = new MissingFileDialog("Cannot find file: " + a + ". Please navigate to file.", Path.GetDirectoryName(path) + "\\" + a);
                            if (mfd.ShowDialog() == true && !File.Exists(Path.Combine(resourceDir, a)))
                            {
                                File.Copy(mfd.FilePath, Path.Combine(resourceDir, a));
                            }
                        }
                    }
                }
                else if (File.Exists(Path.GetDirectoryName(path) + "\\" + s) && !File.Exists(Path.Combine(resourceDir, s)))
                {
                    File.Copy(Path.GetDirectoryName(path) + "\\" + s, Path.Combine(resourceDir, s));
                    List<string> assets = findAssets(Path.Combine(resourceDir, s));
                    foreach (string a in assets)
                    {
                        if (File.Exists(a) && !File.Exists(Path.Combine(resourceDir, Path.GetFileName(a))))
                        {
                            File.Copy(a, Path.Combine(resourceDir, Path.GetFileName(a)));
                        }
                        else if (File.Exists(Path.GetDirectoryName(path) + "\\" + a) && !File.Exists(Path.Combine(resourceDir, a)))
                        {
                            File.Copy(Path.GetDirectoryName(path) + "\\" + a, Path.Combine(resourceDir, a));
                        }
                        else if (!File.Exists(Path.Combine(resourceDir, a)))
                        {
                            //open dialog prompting for file location
                            MissingFileDialog mfd = new MissingFileDialog("Cannot find file: " + a + ". Please navigate to file.", Path.GetDirectoryName(path) + "\\" + a);
                            if (mfd.ShowDialog() == true && !File.Exists(Path.Combine(resourceDir, a)))
                            {
                                File.Copy(mfd.FilePath, Path.Combine(resourceDir, a));
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
                        File.Copy(mfd.FilePath, Path.Combine(resourceDir, s));
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

        private void CopyMtlFiles(List<string> files)
        {

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

        private void CopyAssets(List<string> files)
        {

        }

        private void addModelToDatabase(string filename)
        {
            string query = "INSERT INTO Files (file_name, friendly_name) VALUES ('" + filename + "', 'object');";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        private void addObjectsToDatabase()
        {
            int fileId = getFileIdByFileName(this.currentModelPath);

            foreach (SubObjectViewModel s in rootSubObjectView.Children)
            {
                try
                {
                    string query = "INSERT INTO Objects (file_id, object_name) VALUES (" + fileId + ", '" + s.Name + "');";
                    MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(e.Message);
                }

            }
        }



        #endregion

        public async void LoadModel(string filename)
        {
            this.CurrentModelPath = Path.Combine(modelDirectory, Path.GetFileNameWithoutExtension(filename), filename);
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
            string query = "DELETE FROM Files WHERE file_name = '" + fileName + "';";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();

            //reset viewpoint
            this.CurrentModelPath = "";
            this.CurrentModel = new Model3DGroup();
            this.ApplicationTitle = string.Format(TitleFormatString, this.CurrentModelPath);
            this.viewport.ZoomExtents(0);

            //delete files too
            Directory.Delete(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(fileName)), true);
        }

        private void displaySubObjects()
        {
            SubObject root = new SubObject("Objects:");
            foreach (GeometryModel3D gm in (CurrentModel as Model3DGroup).Children)
            {
                SubObject tag = new SubObject(gm.GetName());
                if (!duplicateSubObject(root.Children, tag.Name))
                {
                    root.Children.Add(tag);
                }

            }

            rootSubObjectView = new SubObjectViewModel(root);

            this.RaisePropertyChanged("SubObjects");

            rootSubObjectView.ExpandAll();

            addObjectsToDatabase();
        }

        private bool duplicateSubObject(IList<SubObject> children, string name)
        {
            foreach (SubObject s in children)
            {
                if (s.Name.Equals(name))
                {
                    return true;
                }
            }
            return false;
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

        private void renameObject(string oldName, string newName)
        {
            int fileId = getFileIdByFileName(this.CurrentModelPath);

            if (fileId != -1)
            {
                int objectId = getObjectId(fileId, oldName);
                if (objectId != -1)
                {
                    string query = "UPDATE Objects SET object_name = '" + newName + "' WHERE object_id = " + objectId;
                    MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void setFileFriendlyName(string fileName, string friendlyName)
        {
            int fileId = getFileIdByFileName(fileName);

            if (fileId != -1)
            {
                string query = "UPDATE Files SET friendly_name = '" + friendlyName + "' WHERE file_id = " + fileId;
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
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

        public ICommand RenameObjectCommand { get; set; }

        public ICommand MarkTagAsReviewedCommand { get; set; }

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
                Properties.Settings.Default.CurrentUser = sd.CurrentUser;
                Properties.Settings.Default.ModelDirectoryPath = sd.ModelDirectoryPath;
                Properties.Settings.Default.Save();
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

            using (Stream stm = File.Create(Path.Combine(modelDirectory, Path.GetFileNameWithoutExtension(this.CurrentModelPath) + ".png")))
            {

                png.Save(stm);

            }
        }

        private void HelpAbout()
        {
            AboutBox ab = new AboutBox();
            ab.ShowDialog();
        }

        private void RenameObject()
        {

        }

        private void MarkTagAsReviewed()
        {

        }

        #endregion

        public void MarkTagAsReviewed(int tagId)
        {
            //check to see what object selected
            SubObjectViewModel s = rootSubObjectView.GetSelectedItem();
            if (s != null && !s.Name.Equals("Objects:"))
            {
                int fileId = getFileIdByFileName(this.CurrentModelPath);
                if (fileId != -1)
                {
                    int objectId = getObjectId(fileId, s.Name);
                    if (objectId != -1)
                    {
                        if (verifyTagReview(tagId, objectId))
                        {
                            reviewTag(tagId, objectId);

                            refreshTagTree();

                            showAssignedTags();
                        }
                        else
                        {
                            MessageBox.Show("You cannot review a tag that you assigned.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                    }
                }
            }
        }

        private bool verifyTagReview(int tagId, int objectId)
        {
            string query = "SELECT tagged_by FROM Object_Tag WHERE object_id = " + objectId + " AND tag_id = " + tagId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            MySqlDataReader reader = cmd.ExecuteReader();

            bool allowed = false;
            if (reader.Read())
            {
                string taggedBy = (string)reader["tagged_by"];
                if (!taggedBy.Equals(this.CurrentUser))
                {
                    allowed = true;
                }
            }
            reader.Close();
            return allowed;

        }

        private void reviewTag(int tagId, int objectId)
        {
            string query = "UPDATE Object_Tag SET reviewed = 1, reviewed_by = '" + CurrentUser + "' WHERE object_id = " + objectId + " AND tag_id = " + tagId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }



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

        private int getObjectId(int file_id, string object_name)
        {
            string query = "SELECT object_id FROM Objects WHERE file_id = " + file_id + " AND object_name = '" + object_name + "';";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            MySqlDataReader reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                int objectId = Convert.ToInt32(reader["object_id"]);
                reader.Close();
                return objectId;
            }
            reader.Close();
            return -1;
        }

        private int getFileIdByFileName(string s)
        {
            //get file id
            string query = "SELECT file_id FROM Files WHERE file_name = '" + Path.GetFileName(s) + "';";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            MySqlDataReader reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                int i = Convert.ToInt32(reader["file_id"]);
                reader.Close();
                return i;
            }

            reader.Close();
            return -1;
        }


    }
}