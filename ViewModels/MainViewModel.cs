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
    using System.Windows.Data;
    using System.Threading;
    using Microsoft.WindowsAPICodePack.Dialogs;
    using System.Xml;


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

        private MySqlConnection sqlConnection;

        private static string modelDirectory = "";

        private static string currentUser;

        private TagViewModel rootTagView;

        private List<ObjectFile> unassignedFileList = new List<ObjectFile>();

        private List<ObjectFile> myFileList = new List<ObjectFile>();

        private List<ObjectFile> reviewFileList = new List<ObjectFile>();

        private List<ObjectFile> approvedFileList = new List<ObjectFile>();

        private List<SubObject> subObjectList = new List<SubObject>();

        private ObjectFile activeFile;

        public MainViewModel(IFileDialogService fds, HelixViewport3D viewport, TreeView tagTree)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException("viewport");
            }
            this.tagTree = tagTree;
            this.dispatcher = Dispatcher.CurrentDispatcher;
            this.Expansion = 1;
            this.fileDialogService = fds;
            this.viewport = viewport;
            this.FileOpenCommand = new DelegateCommand(this.FileOpen);
            this.FileExportCommand = new DelegateCommand(this.FileExport);
            this.FileExportXMLCommand = new DelegateCommand(this.FileExportXML);
            this.FileSaveScreenshotCommand = new DelegateCommand(this.FileSaveScreenshot);
            this.FileExitCommand = new DelegateCommand(FileExit);
            this.ViewZoomExtentsCommand = new DelegateCommand(this.ViewZoomExtents);
            this.EditSettingsCommand = new DelegateCommand(this.Settings);
            this.HelpAboutCommand = new DelegateCommand(this.HelpAbout);
            this.RenameObjectCommand = new DelegateCommand(this.RenameObject);
            this.MarkTagAsReviewedCommand = new DelegateCommand(this.MarkTagAsReviewed);
            this.CheckDataValidityCommand = new DelegateCommand(this.checkValidityOfDatabase);
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

                refreshFileLists();
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

        public void deleteTag(int tagId)
        {
            string query = "DELETE FROM Tags WHERE tag_id = " + tagId + ";";
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

        public void assignTagToObject(int objectID, int tagID)
        {
            try
            {
                string query = "INSERT INTO Object_Tag (object_id, tag_id, tagged_by, reviewed) VALUES (" + objectID + ", " + tagID + ", '" + CurrentUser + "', 0)";
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }

        public void unassignTagFromObject(int objectID, int tagID)
        {
            try
            {
                string query = "DELETE FROM Object_Tag WHERE object_id = " + objectID + " AND tag_id = " + tagID;
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }

        #endregion

        public void showAssignedTags(int objectId)
        {
            refreshTagTree();

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

        #region Review Tags

        public void MarkTagAsReviewed(int tagId, int objectId)
        {
            if (verifyTagReview(tagId, objectId))
            {
                reviewTag(tagId, objectId);

                refreshTagTree();

                showAssignedTags(objectId);
            }
            else
            {
                MessageBox.Show("You cannot review a tag that you assigned.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        #endregion


        #endregion

        #region Object Code

        #region Refresh File Tree

        public void refreshFileLists()
        {

            //create the sub root file objects
            refreshUnassigned();
            refreshMyFiles();
            refreshReviewable();
            refreshComplete();
        }

        private void refreshUnassigned()
        {
            unassignedFileList = new List<ObjectFile>();

            //get files
            //working query
            //string query = "SELECT * FROM Files";
            //updated query
            string query = "SELECT * FROM Files WHERE `current_user` IS NULL AND review_ready = 0";
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            //populate the root file object with database entries
            foreach (DataRow row in table.Rows)
            {
                string query2 = "SELECT tag_id FROM `Files`, `Objects`, `Object_Tag` WHERE Files.file_id = " + Convert.ToInt32(row["file_id"]) + " AND Object_Tag.object_id = Objects.object_id AND Objects.file_id = Files.file_id";
                adapter = new MySqlDataAdapter(query2, sqlConnection);
                DataTable dt = new DataTable();
                adapter.Fill(dt);

                if (dt.Rows.Count > 0)
                {
                    unassignedFileList.Add(new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), true, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["comment"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"])));
                }
                else
                {
                    unassignedFileList.Add(new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), false, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["comment"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"])));
                }

            }

            this.RaisePropertyChanged("UnassignedFiles");
        }

        private void refreshMyFiles()
        {
            myFileList = new List<ObjectFile>();

            try
            {
                //get files
                string query = "SELECT * FROM Files WHERE `current_user` = '" + this.CurrentUser + "';";
                MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
                DataTable table = new DataTable();
                adapter.Fill(table);

                //populate the root file object with database entries
                foreach (DataRow row in table.Rows)
                {
                    string query2 = "SELECT tag_id FROM `Files`, `Objects`, `Object_Tag` WHERE Files.file_id = " + Convert.ToInt32(row["file_id"]) + " AND Object_Tag.object_id = Objects.object_id AND Objects.file_id = Files.file_id";
                    adapter = new MySqlDataAdapter(query2, sqlConnection);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        myFileList.Add(new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), true, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["comment"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"])));
                    }
                    else
                    {
                        myFileList.Add(new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), false, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["comment"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"])));
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            this.RaisePropertyChanged("MyFiles");

        }

        private void refreshReviewable()
        {
            reviewFileList = new List<ObjectFile>();

            try
            {
                //get files
                //get all files where the tagged_by is not the current user and link Object_Tag to Object and Object to File
                //string query = "SELECT * FROM Files, Objects, Object_Tag WHERE Object_Tag.tagged_by != '" + this.CurrentUser + "' AND Object_Tag.object_id = Object.object_id AND Object.file_id = File.file_id";
                //get all files ready to review
                string query = "SELECT * FROM Files WHERE review_ready = 1 AND reviewed_by IS NULL;";
                MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
                DataTable table = new DataTable();
                adapter.Fill(table);

                //populate the root file object with database entries
                foreach (DataRow row in table.Rows)
                {
                    string query2 = "SELECT tag_id FROM `Files`, `Objects`, `Object_Tag` WHERE Files.file_id = " + Convert.ToInt32(row["file_id"]) + " AND Object_Tag.object_id = Objects.object_id AND Objects.file_id = Files.file_id";
                    adapter = new MySqlDataAdapter(query2, sqlConnection);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        reviewFileList.Add(new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), true, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["comment"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"])));
                    }
                    else
                    {
                        reviewFileList.Add(new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), false, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["comment"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"])));
                    }

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            this.RaisePropertyChanged("ReviewFiles");
        }

        private void refreshComplete()
        {
            approvedFileList = new List<ObjectFile>();

            try
            {
                //get files
                string query = "SELECT * FROM Files WHERE reviewed_by IS NOT NULL;";
                MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
                DataTable table = new DataTable();
                adapter.Fill(table);

                //populate the root file object with database entries
                foreach (DataRow row in table.Rows)
                {
                    string query2 = "SELECT tag_id FROM `Files`, `Objects`, `Object_Tag` WHERE Files.file_id = " + Convert.ToInt32(row["file_id"]) + " AND Object_Tag.object_id = Objects.object_id AND Objects.file_id = Files.file_id";
                    adapter = new MySqlDataAdapter(query2, sqlConnection);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    if (dt.Rows.Count > 0)
                    {
                        approvedFileList.Add(new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), true, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["comment"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"])));
                    }
                    else
                    {
                        approvedFileList.Add(new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), false, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["comment"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"])));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            this.RaisePropertyChanged("ApprovedFiles");
        }

        #endregion

        #region Add Model

        public void AddModel()
        {
            // Create OpenFileDialog 
            CommonOpenFileDialog ofd = new CommonOpenFileDialog();

            ofd.IsFolderPicker = false;
            ofd.Multiselect = true;
            ofd.Title = "Select Object Files";

            ofd.Filters.Add(new CommonFileDialogFilter("3D model files (*.3ds;*.obj;*.lwo;*.stl)", ".3ds,.obj,.objz,.lwo,.stl"));
            //ofd.Filters.Add(new CommonFileDialogFilter("All Files (*.*)", ".*"));

            // Get the selected file name and display in a TextBox 
            if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
            {
                foreach (string s in ofd.FileNames)
                {
                    if (CopyFiles(s))
                    {
                        //add to database
                        addModelToDatabase(Path.GetFileName(s));

                        refreshFileLists();

                        LoadModel(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(s), Path.GetFileName(s)));

                        addObjectsToDatabase();

                        refreshSubObjects(getFileIdByFileName(this.CurrentModelPath));
                    }
                }

            }

            /*string objectFile = this.fileDialogService.OpenFileDialog("models", null, OpenFileFilter, ".obj");
            //copy model files into new directory
            if (objectFile != null)
            {
                if (CopyFiles(objectFile))
                {
                    //add to database
                    addModelToDatabase(Path.GetFileName(objectFile));

                    refreshFileLists();

                    LoadModel(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(objectFile), Path.GetFileName(objectFile)));

                    addObjectsToDatabase();

                    refreshSubObjects(getFileIdByFileName(this.CurrentModelPath));
                }
            }*/

        }

        private bool CopyFiles(string path)
        {
            string resourceDir = Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(path));


            //check to see if model directory exists
            if (Directory.Exists(resourceDir))
            {
                //ask user what to do, overwrite or ignore
                MessageBox.Show("This file has already been added. Please delete the old version if you wish to replace it. NOTE: All taggings for the old object will be lost",
                    "File Already Exists", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
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
                        else if (File.Exists(Path.Combine(Path.GetDirectoryName(path), "assets", a)) && !File.Exists(Path.Combine(resourceDir, a)))
                        {
                            File.Copy(Path.Combine(Path.GetDirectoryName(path), "assets", a), Path.Combine(resourceDir, a));
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
                        if (File.Exists(a) && !File.Exists(Path.Combine(resourceDir, Path.GetFileName(a)))) //if the file path is explicit and is not already in the resource dir
                        {
                            File.Copy(a, Path.Combine(resourceDir, Path.GetFileName(a)));
                        }
                        else if (File.Exists(Path.GetDirectoryName(path) + "\\" + Path.GetFileName(a)) && !File.Exists(Path.Combine(resourceDir, Path.GetFileName(a)))) //else if the file exists in the same directory as the mtl file and it is not already in the resource dir
                        {
                            File.Copy(Path.GetDirectoryName(path) + "\\" + a, Path.Combine(resourceDir, a));
                        }
                        else if (File.Exists(Path.Combine(Path.GetDirectoryName(path), "assets", Path.GetFileName(a))) && !File.Exists(Path.Combine(resourceDir, Path.GetFileName(a)))) //else if the file is in the assets directory and is not in the resource dir
                        {
                            File.Copy(Path.Combine(Path.GetDirectoryName(path), "assets", Path.GetFileName(a)), Path.Combine(resourceDir, Path.GetFileName(a)));
                        }
                        else if (!File.Exists(Path.Combine(resourceDir, a)))
                        {
                            //open dialog prompting for file location
                            MissingFileDialog mfd = new MissingFileDialog("Cannot find file: " + a + ". Please navigate to file.", Path.GetDirectoryName(path) + "\\" + a);
                            if (mfd.ShowDialog() == true && !File.Exists(Path.Combine(resourceDir, Path.GetFileName(a))))
                            {
                                File.Copy(mfd.FilePath, Path.Combine(resourceDir, Path.GetFileName(a)));
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
            return true;

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
            string query = "INSERT INTO Files (file_name, friendly_name, uploaded_by) VALUES ('" + filename + "', 'object', '" + this.CurrentUser + "');";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        private void addObjectsToDatabase()
        {
            int fileId = getFileIdByFileName(this.currentModelPath);

            foreach (string s in getSubObjects())
            {
                try
                {
                    string query = "INSERT INTO Objects (file_id, object_name) VALUES (" + fileId + ", '" + s + "');";
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

        #region Load Model

        public void LoadModel(ObjectFile of)
        {
            this.CurrentModelPath = Path.Combine(modelDirectory, Path.GetFileNameWithoutExtension(of.FileName), of.FileName);
            this.CurrentModel = this.Load(this.CurrentModelPath, false);
            this.ApplicationTitle = string.Format(TitleFormatString, this.CurrentModelPath);
            this.viewport.ZoomExtents(0);
            this.ActiveFile = of;

            //populate sub object display
            refreshSubObjects(getFileIdByFileName(this.CurrentModelPath));
        }

        public void LoadModel(string filename)
        {
            this.CurrentModelPath = Path.Combine(modelDirectory, Path.GetFileNameWithoutExtension(filename), filename);
            this.CurrentModel = this.Load(this.CurrentModelPath, false);
            this.ApplicationTitle = string.Format(TitleFormatString, this.CurrentModelPath);
            this.viewport.ZoomExtents(0);

            //populate sub object display
            refreshSubObjects(getFileIdByFileName(this.CurrentModelPath));
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
            if (File.Exists(model3DPath))
            {
                var mi = new CAVS_ModelImporter();
                try
                {
                    if (freeze)
                    {
                        // Alt 1. - freeze the model 
                        return mi.Load(model3DPath, null, true);
                    }

                    // Alt. 2 - create the model on the UI dispatcher
                    return mi.Load(model3DPath, this.dispatcher);
                }
                catch (InvalidOperationException e)
                {
                    MessageBox.Show("Cannot load file. Please check that the file is a valid 3D model file (not .mtl) and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

            }
            else
            {
                MessageBox.Show("Cannot find file " + model3DPath + ". The database and file system may be out of sync. Run the Validity Check to check for errors.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }


        }

        #endregion

        public void deleteObjects(System.Collections.IList files)
        {
            //reset viewpoint
            this.resetView();

            foreach (ObjectFile f in files)
            {
                //delete files too
                try
                {
                    Directory.Delete(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(f.FileName)), true);
                }
                catch (IOException e)
                {
                    MessageBox.Show("There was an exception deleting files. The database may be out of sync. Run the validity check to verify data integrity.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                //remove from database
                string query = "DELETE FROM Files WHERE file_name = '" + f.FileName + "';";
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }

            //refresh file lists
            refreshFileLists();
        }

        public void deleteObject(string fileName)
        {
            //reset viewpoint
            this.resetView();

            //delete files too
            Directory.Delete(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(fileName)), true);

            //remove from database
            string query = "DELETE FROM Files WHERE file_name = '" + fileName + "';";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();

            //refresh file lists
            refreshFileLists();
        }

        public void assignFiles(System.Collections.IList files)
        {
            foreach (ObjectFile f in files)
            {
                assignFile(f.FileId, this.CurrentUser);
            }
            this.refreshFileLists();
        }

        public void assignFiles(System.Collections.IList files, string username)
        {
            foreach (ObjectFile f in files)
            {
                assignFile(f.FileId, username);
            }
            this.refreshFileLists();
        }

        public void assignFile(int fileId)
        {
            assignFile(fileId, this.CurrentUser);
            this.refreshFileLists();
        }

        public void assignFile(int fileId, string username)
        {
            if (username != "")
            {
                string query = "UPDATE Files SET `current_user` = '" + username + "', review_ready = 0, reviewed_by = NULL WHERE file_id = " + fileId;
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
            else
            {
                string query = "UPDATE Files SET `current_user` = NULL, review_ready = 0, reviewed_by = NULL WHERE file_id = " + fileId;
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
        }

        public void markFileComplete(int fileId)
        {
            string query = "UPDATE Files SET review_ready = 1, `current_user` = NULL WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();

            this.refreshFileLists();
        }

        public void approveReview(int fileId)
        {
            string query = "UPDATE Files SET `reviewed_by` = '" + this.CurrentUser + "' WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();

            this.refreshFileLists();
        }

        public void setFileFriendlyName(string fileName, string friendlyName)
        {
            int fileId = getFileIdByFileName(fileName);

            if (fileId != -1)
            {
                string query = "UPDATE Files SET friendly_name = '" + friendlyName + "' WHERE file_id = " + fileId;
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
        }

        public void resetModel(bool resetSubObjects)
        {
            if (this.CurrentModelPath != null)
            {
                this.CurrentModel = this.Load(this.CurrentModelPath, false);

                if (resetSubObjects)
                {
                    refreshSubObjects(getFileIdByFileName(this.CurrentModelPath));
                }
            }
        }

        public void resetView()
        {
            this.CurrentModelPath = "";
            this.CurrentModel = new Model3DGroup();
            this.ApplicationTitle = string.Format(TitleFormatString, this.CurrentModelPath);
            this.viewport.ZoomExtents(0);

            this.refreshSubObjects(-1);

            this.refreshTagTree();
        }

        #endregion

        #region SubObject Code

        private List<string> getSubObjects()
        {
            List<string> subObjects = new List<string>();

            foreach (GeometryModel3D gm in (CurrentModel as Model3DGroup).Children)
            {
                subObjects.Add(gm.GetName());
            }

            return subObjects;
        }

        private void refreshSubObjects(int fileId)
        {
            subObjectList = new List<SubObject>();

            if (fileId != -1)
            {
                string query = "SELECT * FROM Objects WHERE file_id = " + fileId;
                MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
                DataTable table = new DataTable();
                adapter.Fill(table);

                //populate the root file object with database entries
                foreach (DataRow row in table.Rows)
                {
                    SubObject so = new SubObject(Convert.ToInt32(row["object_id"]), (string)row["object_name"]);
                    subObjectList.Add(so);
                }
            }


            this.RaisePropertyChanged("SubObjects");

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
            resetModel(false);

            if (CurrentModel != null)
            {
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

        public List<string> getCategories()
        {
            List<string> categories = new List<string>();

            string query = "SELECT DISTINCT `category` FROM Files WHERE `category` <> \"\";";
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            //populate the root file object with database entries
            foreach (DataRow row in table.Rows)
            {
                categories.Add(ConvertFromDBValue<string>(row["category"]));
            }

            return categories;
        }

        public void setCategory(int fileId, string category)
        {
            string query = "UPDATE Files SET `category` = '" + category + "' WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        public void setShadows(int fileId, int value)
        {
            string query = "UPDATE Files SET `shadows` = " + value + " WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        public void setZUp(int fileId, int value)
        {
            string query = "UPDATE Files SET `zUp` = " + value + " WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        public void setPhysicsGeometry(int fileId, int value)
        {
            string query = "UPDATE Files SET `physicsGeometry` = " + value + " WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        public void setComments(int fileId, string value)
        {
            string query = "UPDATE Files SET `comment` = @value WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = sqlConnection;
            cmd.CommandText = query;
            cmd.Prepare();
            cmd.Parameters.AddWithValue("value", value);
            cmd.ExecuteNonQuery();
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

        public ReadOnlyCollection<SubObject> SubObjects
        {
            get { return new ReadOnlyCollection<SubObject>(subObjectList); }
        }

        public ReadOnlyCollection<ObjectFile> UnassignedFiles
        {
            get { return new ReadOnlyCollection<ObjectFile>(unassignedFileList); }
        }

        public ReadOnlyCollection<ObjectFile> MyFiles
        {
            get { return new ReadOnlyCollection<ObjectFile>(myFileList); }
        }

        public ReadOnlyCollection<ObjectFile> ReviewFiles
        {
            get { return new ReadOnlyCollection<ObjectFile>(reviewFileList); }
        }

        public ReadOnlyCollection<ObjectFile> ApprovedFiles
        {
            get { return new ReadOnlyCollection<ObjectFile>(approvedFileList); }
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

        public ObjectFile ActiveFile
        {
            get { return activeFile; }
            set
            {
                this.activeFile = value;
                this.RaisePropertyChanged("ActiveFile");
            }
        }

        #endregion

        #region Menu Commands

        public ICommand FileOpenCommand { get; set; }

        public ICommand FileExportCommand { get; set; }

        public ICommand FileExportXMLCommand { get; set; }

        public ICommand FileSaveScreenshotCommand { get; set; }

        public ICommand FileExitCommand { get; set; }

        public ICommand HelpAboutCommand { get; set; }

        public ICommand ViewZoomExtentsCommand { get; set; }

        public ICommand EditSettingsCommand { get; set; }

        public ICommand RenameObjectCommand { get; set; }

        public ICommand MarkTagAsReviewedCommand { get; set; }

        public ICommand CheckDataValidityCommand { get; set; }

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

        private async void FileExportXML()
        {
            CommonSaveFileDialog sfd = new CommonSaveFileDialog();
            sfd.Filters.Add(new CommonFileDialogFilter("XML File", ".xml"));
            if (sfd.ShowDialog().Equals(CommonFileDialogResult.Ok))
            {
                string path = sfd.FileName;
                if (!path.EndsWith(".xml", true, null))
                {
                    path += ".xml";
                }
                await WriteXML(path);
            }
        }

        private async Task WriteXML(string path)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Async = true;

            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                await writer.WriteStartDocumentAsync();

                //start definitions
                await writer.WriteStartElementAsync(null, "definitions", null);

                //start objects
                await writer.WriteStartElementAsync(null, "objects", null);

                ////start default preview
                await writer.WriteStartElementAsync(null, "defaultPreview", null);
                await writer.WriteAttributeStringAsync(null, "path", null, "Icons/DefaultPreview.png");
                await writer.WriteEndElementAsync();
                ////end default preview

                //for each file in the database
                string query = "SELECT * FROM Files";
                MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
                DataTable table = new DataTable();
                adapter.Fill(table);

                //for each file in the database
                foreach (DataRow row in table.Rows)
                {
                    ////start object elements
                    await writer.WriteStartElementAsync(null, "object", null);
                    await writer.WriteAttributeStringAsync(null, "name", null, ConvertFromDBValue<string>(row["friendly_name"]));
                    await writer.WriteAttributeStringAsync(null, "category", null, ConvertFromDBValue<string>(row["category"]));
                    await writer.WriteAttributeStringAsync(null, "preview", null, ConvertFromDBValue<string>(row["screenshot"]));

                    //////start representations
                    await writer.WriteStartElementAsync(null, "representations", null);
                    ////////start ogre3d
                    await writer.WriteStartElementAsync(null, "ogre3d", null);
                    await writer.WriteAttributeStringAsync(null, "mesh", null, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])) + ".mesh");
                    if (ConvertFromDBValue<int>(row["shadows"]).Equals(0))
                    {
                        await writer.WriteAttributeStringAsync(null, "shadows", null, "off");
                    }
                    else
                    {
                        await writer.WriteAttributeStringAsync(null, "shadows", null, "on");
                    }
                    if (ConvertFromDBValue<int>(row["zUp"]).Equals(0))
                    {
                        await writer.WriteAttributeStringAsync(null, "zUp", null, "false");
                    }
                    else
                    {
                        await writer.WriteAttributeStringAsync(null, "zUp", null, "true");
                    }
                    await writer.WriteEndElementAsync();
                    /////////end ogre3d

                    ////////start vane
                    await writer.WriteStartElementAsync(null, "vane", null);
                    await writer.WriteAttributeStringAsync(null, "mesh", null, ConvertFromDBValue<string>(row["file_name"]));
                    await writer.WriteEndElementAsync();
                    ////////end vane

                    await writer.WriteEndElementAsync();
                    //////end representations

                    ///////////////////////////////
                    /////START PARTS          /////
                    ///////////////////////////////

                    string partQuery = "SELECT * FROM Objects WHERE file_id = " + ConvertFromDBValue<int>(row["file_id"]);
                    MySqlDataAdapter partAdapter = new MySqlDataAdapter(partQuery, sqlConnection);
                    DataTable partTable = new DataTable();
                    partAdapter.Fill(partTable);

                    //for each part in the file
                    foreach (DataRow partRow in partTable.Rows)
                    {
                        //////start part
                        await writer.WriteStartElementAsync(null, "part", null);
                        await writer.WriteAttributeStringAsync(null, "name", null, ConvertFromDBValue<string>(partRow["object_name"]));

                        ////////////////
                        // START TAGS //
                        ////////////////

                        string tagQuery = "SELECT tag_name FROM Object_Tag, Tags WHERE Object_Tag.object_id = " + ConvertFromDBValue<int>(partRow["object_id"]) + " AND Object_Tag.tag_id = Tags.tag_id";
                        MySqlDataAdapter tagAdapter = new MySqlDataAdapter(tagQuery, sqlConnection);
                        DataTable tagTable = new DataTable();
                        tagAdapter.Fill(tagTable);

                        foreach (DataRow tagRow in tagTable.Rows)
                        {
                            ////////start tag
                            await writer.WriteStartElementAsync(null, "tag", null);
                            await writer.WriteAttributeStringAsync(null, "name", null, ConvertFromDBValue<string>(tagRow["tag_name"]));
                            await writer.WriteEndElementAsync();
                            ////////end tag
                        }

                        ////////////////
                        //  END TAGS  //
                        ////////////////

                        await writer.WriteEndElementAsync();
                        //////end part
                    }

                    ///////////////////////////////
                    /////END PARTS            /////
                    ///////////////////////////////

                    //////start physics
                    await writer.WriteStartElementAsync(null, "physics", null);
                    await writer.WriteAttributeStringAsync(null, "type", null, "static");

                    ////////start geometry
                    await writer.WriteStartElementAsync(null, "geometry", null);
                    //////////start shape
                    await writer.WriteStartElementAsync(null, "shape", null);
                    await writer.WriteAttributeStringAsync(null, "type", null, "triMesh");
                    await writer.WriteAttributeStringAsync(null, "mesh", null, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])) + ".mesh");
                    if (ConvertFromDBValue<int>(row["zUp"]).Equals(0))
                    {
                        await writer.WriteAttributeStringAsync(null, "upAxis", null, "y");
                    }
                    else
                    {
                        await writer.WriteAttributeStringAsync(null, "upAxis", null, "z");
                    }
                    await writer.WriteEndElementAsync();
                    //////////end shape
                    await writer.WriteEndElementAsync();
                    ////////end geometry

                    await writer.WriteEndElementAsync();
                    //////end physics

                    await writer.WriteEndElementAsync();
                    ////end object

                }//end for each file in database

                await writer.WriteEndElementAsync();
                //end objects

                //start tag hierarchy
                await writer.WriteStartElementAsync(null, "tags", null);

                foreach (TagViewModel tag in rootTagView.Children)
                {
                    await writer.WriteStartElementAsync(null, "tag", null);
                    await writer.WriteAttributeStringAsync(null, "name", null, tag.Name);
                    await writer.WriteEndElementAsync();
                    await WriteTagXml(writer, tag);
                }

                await writer.WriteEndElementAsync();
                //end tag hierarchy

                await writer.WriteEndElementAsync();
                //end definitions

                //flush buffer
                await writer.FlushAsync();

            }


        }

        private async Task WriteTagXml(XmlWriter writer, TagViewModel tag)
        {
            foreach (TagViewModel tvm in tag.Children)
            {
                await writer.WriteStartElementAsync(null, "tag", null);
                await writer.WriteAttributeStringAsync(null, "name", null, tvm.Name);
                await writer.WriteAttributeStringAsync(null, "parent", null, tvm.Parent.Name);
                await writer.WriteEndElementAsync();
                await WriteTagXml(writer, tvm);
            }
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

        public void FileSaveScreenshot()
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

            using (Stream stm = File.Create(Path.Combine(modelDirectory, Path.GetFileNameWithoutExtension(this.CurrentModelPath), Path.GetFileNameWithoutExtension(this.CurrentModelPath) + ".png")))
            {
                png.Save(stm);
            }

            string query = "UPDATE Files SET screenshot = '" + Path.GetFileNameWithoutExtension(this.CurrentModelPath) + ".png" + "' WHERE file_id = " + getFileIdByFileName(this.CurrentModelPath);
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();

            refreshFileLists();
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

        public List<string> getListOfUsers()
        {
            List<string> users = new List<string>();

            string query = "SELECT DISTINCT uploaded_by " +
                "FROM Files " +
                "WHERE uploaded_by IS NOT NULL " +
                "UNION " +
                "SELECT DISTINCT `current_user` " +
                "FROM Files " +
                "WHERE `current_user` IS NOT NULL " +
                "UNION " +
                "SELECT DISTINCT reviewed_by " +
                "FROM Files " +
                "WHERE reviewed_by IS NOT NULL " +
                "UNION " +
                "SELECT DISTINCT tagged_by " +
                "FROM Object_Tag " +
                "WHERE tagged_by IS NOT NULL ";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            MySqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                users.Add((string)reader["uploaded_by"]);
            }

            reader.Close();

            return users;
        }

        public void checkValidityOfDatabase()
        {
            List<string> rawResourceDirs = new List<string>(Directory.GetDirectories(this.ModelDirectory));

            List<string> resourceDirs = new List<string>();
            foreach (string s in rawResourceDirs)
            {
                resourceDirs.Add(new DirectoryInfo(s).Name);
            }

            string query = "SELECT file_name from Files;";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            MySqlDataReader reader = cmd.ExecuteReader();

            List<string> unmatchedDbEntries = new List<string>();

            while (reader.Read())
            {
                if (resourceDirs.Contains(Path.GetFileNameWithoutExtension((string)reader["file_name"])))
                {
                    resourceDirs.Remove(Path.GetFileNameWithoutExtension((string)reader["file_name"]));
                }
                else
                {
                    unmatchedDbEntries.Add((string)reader["file_name"]);
                }
            }

            reader.Close();

            /*Console.WriteLine("The following items have folders in the model directory, but no matching database entry:");
            foreach (string s in resourceDirs)
            {
                Console.WriteLine(s);
            }

            Console.WriteLine("The following items have database entries, but no matching folder in the model directory:");
            foreach (string s in unmatchedDbEntries)
            {
                Console.WriteLine(s);
            }*/

            CheckValidityDialog cvd = new CheckValidityDialog(this, resourceDirs, unmatchedDbEntries);
            cvd.ShowDialog();


        }

        public void purgeDirectories(List<string> directoryList)
        {
            foreach (string s in directoryList)
            {
                Directory.Delete(Path.Combine(this.ModelDirectory, s), true);
            }
        }

        public void purgeDatabaseEntries(List<string> databaseEntryList)
        {
            foreach (string s in databaseEntryList)
            {
                string query = "DELETE FROM Files WHERE file_id = " + getFileIdByFileName(s);
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
        }

        public T ConvertFromDBValue<T>(object obj)
        {
            if (obj == null || obj == DBNull.Value)
            {
                return default(T);
            }
            else
            {
                return (T)obj;
            }
        }

        public bool IsTagTreeEnabled
        {
            get { return rootTagView.IsEnabled; }
            set
            {
                rootTagView.IsEnabled = value;
            }
        }
    }
}