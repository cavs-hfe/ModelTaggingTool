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
    using ModelViewer.Models;
    using ModelViewer.Exporter;
    using System.Diagnostics;


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

        private Dictionary<string, Brush> colorUserMapping = new Dictionary<string, Brush>();

        private Queue<Brush> commentColors = new Queue<Brush>(new[] { Brushes.Red, Brushes.Blue, Brushes.Green, Brushes.Orange, Brushes.Purple, Brushes.Yellow, Brushes.Cyan, Brushes.Violet });

        private int percentageComplete = 0;

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
            this.FileExportANVELCommand = new DelegateCommand(this.FileExportANVELObject);
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

            //get current user and model directory from Settings
            CurrentUser = Properties.Settings.Default.CurrentUser;
            ModelDirectory = Properties.Settings.Default.ModelDirectoryPath;

            //check to see if the model directory path is set correctly
            if (!Directory.Exists(ModelDirectory))
            {
                MessageBox.Show("Model directory not set correctly. Please set the correct path to the model directory.", "Invalid Model Directory Path", MessageBoxButton.OK, MessageBoxImage.Error);
                Settings();
            }

            //set up and open mySql connection
            sqlConnection = new MySqlConnection("server=Mitchell.HPC.MsState.Edu; database=cavs_ivp04;Uid=cavs_ivp04_user;Pwd=TLBcEsm7;");

            try
            {
                sqlConnection.Open();

                //get the tag tree and file list information from the database
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

        /// <summary>
        ///  and populate the rootTagView, updating the tag tree UI.
        /// </summary>
        public void refreshTagTree()
        {
            Tag rootTag = new Tag(18, "Tags:", -1);
            rootTag = PopulateRootTag(rootTag);
            rootTagView = new TagViewModel(rootTag);

            this.RaisePropertyChanged("FirstGeneration");

            rootTagView.ExpandAll();

        }

        /// <summary>
        /// Recursive method to build the tag hierarchy from the database. 
        /// </summary>
        /// <param name="parentTag">The Tag of which to find the child Tags.</param>
        /// <returns>The parentTag populated with all of its children.</returns>
        private Tag PopulateRootTag(Tag parentTag)
        {
            //get everything from the Tags table where the parent Id is the Id of the current tag
            string query = "SELECT * FROM Tags WHERE parent = " + parentTag.Id;
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            //for each row in the result set, create a Tag object, call this method, then add it to the parent tag.
            foreach (DataRow row in table.Rows)
            {
                Tag tag = new Tag(Convert.ToInt32(row["tag_id"]), row["tag_name"].ToString(), Convert.ToInt32(row["parent"]));
                PopulateRootTag(tag);
                parentTag.Children.Add(tag);
            }

            //return the parent tag that has now been populated with all of its child tags in the database
            return parentTag;
        }

        #endregion

        /// <summary>
        /// Add a new tag to the database. Tag will be added as to the root tag by default. The user can drag it to the correct place in the heirarchy after it is added.
        /// </summary>
        /// <param name="tag">Name of tag to be added to the database.</param>
        public void addNewTag(string tag)
        {
            try
            {
                //insert into Tags the new tag name and set the parent ID to 18 (the root tag)
                string query = "INSERT INTO Tags (tag_name, parent) VALUES ('" + tag + "', 18);";
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException e)
            {
                if (e.Number == 1062)  //dupliate key or index
                {
                    MessageBox.Show("This tag already exists. Please try again with a new tag name.", "Duplicate Tag", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }

        /// <summary>
        /// Delete a tag from the database. Note that this will also delete all child tags from the database as well.
        /// </summary>
        /// <param name="tagId">ID of tag to be deleted.</param>
        public void deleteTag(int tagId)
        {
            string query = "DELETE FROM Tags WHERE tag_id = " + tagId + ";";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Update the parent ID of a tag.
        /// </summary>
        /// <param name="child">ID of child tag.</param>
        /// <param name="newParent">Name of new parent tag.</param>
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

        /// <summary>
        /// Assign a tag to an object.
        /// </summary>
        /// <param name="objectID">ID of object that is being tagged.</param>
        /// <param name="tagID">ID of tag to be assigned to object.</param>
        public void assignTagToObject(int objectID, int tagID)
        {
            try
            {
                //insert into the Object_Tag database the object and tagging, as well as the user who is doing the tagging
                string query = "INSERT INTO Object_Tag (object_id, tag_id, tagged_by, reviewed) VALUES (" + objectID + ", " + tagID + ", '" + CurrentUser + "', 0)";
                MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }

        /// <summary>
        /// Remove a tag from an object.
        /// </summary>
        /// <param name="objectID">ID of object that is being untagged.</param>
        /// <param name="tagID">ID of tag to be removed from object.</param>
        public void unassignTagFromObject(int objectID, int tagID)
        {
            try
            {
                //delete the row that contains the object ID and tag ID
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

        /// <summary>
        /// Update the tag tree to show which tags are assigned to an object.
        /// </summary>
        /// <param name="objectId">The (currently selected) object to search for tags with.</param>
        public void showAssignedTags(int objectId)
        {
            //refresh (and reset) tag tree to default state
            refreshTagTree();

            //get the tag name where the object ID exists
            string query = "SELECT tag_name, reviewed FROM Object_Tag, Tags WHERE object_id = " + objectId + " AND Object_Tag.tag_id = Tags.tag_id;";
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            //for each row in the result set, as long as it isn't the root tag, find the TagViewModel and set it checked
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

        [Obsolete("Deprecated in favor of whole file review.")]
        /// <summary>
        /// Mark a tag as reviewed.
        /// </summary>
        /// <param name="tagId">ID of tag being reviewed.</param>
        /// <param name="objectId">ID of object that tag is assigned to.</param>
        public void MarkTagAsReviewed(int tagId, int objectId)
        {
            //if the current user is allowed to review the tag
            if (verifyTagReview(tagId, objectId))
            {
                //update the database
                reviewTag(tagId, objectId);

                //update the tag tree (tag should no longer be bolded)
                showAssignedTags(objectId);
            }
            else //otherwise show error
            {
                MessageBox.Show("You cannot review a tag that you assigned.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [Obsolete("Deprectated as part of tag review. Process replaced with whole file review.")]
        /// <summary>
        /// Verify that the current user is allowed to mark a file as reviewed. Basically check that the current user is not the same user who assigned the tag to the object.
        /// </summary>
        /// <param name="tagId">ID of tag being reviewed.</param>
        /// <param name="objectId">ID of object being reviewed.</param>
        /// <returns>true if user should be allowed to mark as reviewed, false otherwise.</returns>
        private bool verifyTagReview(int tagId, int objectId)
        {
            //get the user who assigned the tag to the object
            string query = "SELECT tagged_by FROM Object_Tag WHERE object_id = " + objectId + " AND tag_id = " + tagId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            MySqlDataReader reader = cmd.ExecuteReader();

            //deny by default
            bool allowed = false;
            //if the tagging is found
            if (reader.Read())
            {
                //get the user who tagged the object
                string taggedBy = (string)reader["tagged_by"];
                //if the tagger is not the current user, set the return variable to true
                if (!taggedBy.Equals(this.CurrentUser))
                {
                    allowed = true;
                }
            }
            reader.Close();
            return allowed;

        }

        [Obsolete("Deprectated as part of tag review. Process replaced with whole file review.")]
        /// <summary>
        /// Update database that tag has been reviewed.
        /// </summary>
        /// <param name="tagId">ID of tag being reviewed.</param>
        /// <param name="objectId">ID of object being reviewed.</param>
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

        /// <summary>
        /// Method to update all of the file lists in the file tabs.
        /// </summary>
        public void refreshFileLists()
        {
            refreshUnassigned();
            refreshMyFiles();
            refreshReviewable();
            refreshComplete();
        }

        /// <summary>
        /// Get the unassigned files from the database and update the tab.
        /// </summary>
        private void refreshUnassigned()
        {
            //reset the unassigned file list variable
            unassignedFileList = new List<ObjectFile>();

            //get files that are unassigned (current user == null and not reviewed)
            string query = "SELECT * FROM Files WHERE `current_user` IS NULL AND review_ready = 0";
            MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
            DataTable table = new DataTable();
            adapter.Fill(table);

            //populate the root file object with database entries
            foreach (DataRow row in table.Rows)
            {
                //get the tags assigned to any part of the file (used to set the tag icon next to the file name)
                string query2 = "SELECT tag_id FROM `Files`, `Objects`, `Object_Tag` WHERE Files.file_id = " + Convert.ToInt32(row["file_id"]) + " AND Object_Tag.object_id = Objects.object_id AND Objects.file_id = Files.file_id";
                adapter = new MySqlDataAdapter(query2, sqlConnection);
                DataTable dt = new DataTable();
                adapter.Fill(dt);

                //get the comments associated with this file
                string query3 = "SELECT * FROM Comments WHERE file_id = " + Convert.ToInt32(row["file_id"]);
                adapter = new MySqlDataAdapter(query3, sqlConnection);
                DataTable commentTable = new DataTable();
                adapter.Fill(commentTable);

                //declare the comments list
                List<Comment> comments = new List<Comment>();

                //for each comment, create a Comment object, assign a color and add to list
                foreach (DataRow comment in commentTable.Rows)
                {
                    Comment c = new Comment();
                    c.Id = ConvertFromDBValue<int>(comment["comment_id"]);
                    c.User = ConvertFromDBValue<string>(comment["user"]);
                    c.CommentText = ConvertFromDBValue<string>(comment["comment"]);
                    c.Timestamp = ConvertFromDBValue<DateTime>(comment["timestamp"]);
                    if (colorUserMapping.ContainsKey(c.User))
                    {
                        c.Color = colorUserMapping[c.User];
                    }
                    else
                    {
                        colorUserMapping.Add(c.User, commentColors.Dequeue());
                        c.Color = colorUserMapping[c.User];
                    }
                    comments.Add(c);
                }

                //create the appropriate ObjectFile based on the number of rows in the tag result set
                if (dt.Rows.Count > 0)
                {
                    ObjectFile of = new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), true, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"]));
                    of.Comments = comments;
                    unassignedFileList.Add(of);
                }
                else
                {
                    ObjectFile of = new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), false, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"]));
                    of.Comments = comments;
                    unassignedFileList.Add(of);
                }

            }

            //update property manager
            this.RaisePropertyChanged("UnassignedFiles");
        }

        /// <summary>
        /// Get current user's files from the database and update the tab.
        /// </summary>
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

                    string query3 = "SELECT * FROM Comments WHERE file_id = " + Convert.ToInt32(row["file_id"]);
                    adapter = new MySqlDataAdapter(query3, sqlConnection);
                    DataTable commentTable = new DataTable();
                    adapter.Fill(commentTable);

                    List<Comment> comments = new List<Comment>();

                    foreach (DataRow comment in commentTable.Rows)
                    {
                        Comment c = new Comment();
                        c.Id = ConvertFromDBValue<int>(comment["comment_id"]);
                        c.User = ConvertFromDBValue<string>(comment["user"]);
                        c.CommentText = ConvertFromDBValue<string>(comment["comment"]);
                        c.Timestamp = ConvertFromDBValue<DateTime>(comment["timestamp"]);
                        if (colorUserMapping.ContainsKey(c.User))
                        {
                            c.Color = colorUserMapping[c.User];
                        }
                        else
                        {
                            colorUserMapping.Add(c.User, commentColors.Dequeue());
                            c.Color = colorUserMapping[c.User];
                        }
                        comments.Add(c);
                    }

                    if (dt.Rows.Count > 0)
                    {
                        ObjectFile of = new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), true, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"]));
                        of.Comments = comments;
                        myFileList.Add(of);
                    }
                    else
                    {
                        ObjectFile of = new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), false, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"]));
                        of.Comments = comments;
                        myFileList.Add(of);
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            this.RaisePropertyChanged("MyFiles");

        }

        /// <summary>
        /// Get the review ready files from the database and update the tab.
        /// </summary>
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

                    string query3 = "SELECT * FROM Comments WHERE file_id = " + Convert.ToInt32(row["file_id"]);
                    adapter = new MySqlDataAdapter(query3, sqlConnection);
                    DataTable commentTable = new DataTable();
                    adapter.Fill(commentTable);

                    List<Comment> comments = new List<Comment>();

                    foreach (DataRow comment in commentTable.Rows)
                    {
                        Comment c = new Comment();
                        c.Id = ConvertFromDBValue<int>(comment["comment_id"]);
                        c.User = ConvertFromDBValue<string>(comment["user"]);
                        c.CommentText = ConvertFromDBValue<string>(comment["comment"]);
                        c.Timestamp = ConvertFromDBValue<DateTime>(comment["timestamp"]);
                        if (colorUserMapping.ContainsKey(c.User))
                        {
                            c.Color = colorUserMapping[c.User];
                        }
                        else
                        {
                            colorUserMapping.Add(c.User, commentColors.Dequeue());
                            c.Color = colorUserMapping[c.User];
                        }
                        comments.Add(c);
                    }

                    if (dt.Rows.Count > 0)
                    {
                        ObjectFile of = new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), true, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"]));
                        of.Comments = comments;
                        reviewFileList.Add(of);
                    }
                    else
                    {
                        ObjectFile of = new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), false, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"]));
                        of.Comments = comments;
                        reviewFileList.Add(of);
                    }

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            this.RaisePropertyChanged("ReviewFiles");
        }

        /// <summary>
        /// Get the complete files from the database and update the tab.
        /// </summary>
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

                    string query3 = "SELECT * FROM Comments WHERE file_id = " + Convert.ToInt32(row["file_id"]);
                    adapter = new MySqlDataAdapter(query3, sqlConnection);
                    DataTable commentTable = new DataTable();
                    adapter.Fill(commentTable);

                    List<Comment> comments = new List<Comment>();

                    foreach (DataRow comment in commentTable.Rows)
                    {
                        Comment c = new Comment();
                        c.Id = ConvertFromDBValue<int>(comment["comment_id"]);
                        c.User = ConvertFromDBValue<string>(comment["user"]);
                        c.CommentText = ConvertFromDBValue<string>(comment["comment"]);
                        c.Timestamp = ConvertFromDBValue<DateTime>(comment["timestamp"]);
                        if (colorUserMapping.ContainsKey(c.User))
                        {
                            c.Color = colorUserMapping[c.User];
                        }
                        else
                        {
                            colorUserMapping.Add(c.User, commentColors.Dequeue());
                            c.Color = colorUserMapping[c.User];
                        }
                        comments.Add(c);
                    }

                    if (dt.Rows.Count > 0)
                    {
                        ObjectFile of = new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), true, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"]));
                        of.Comments = comments;
                        approvedFileList.Add(of);
                    }
                    else
                    {
                        ObjectFile of = new ObjectFile(Convert.ToInt32(row["file_id"]), ConvertFromDBValue<string>(row["file_name"]), ConvertFromDBValue<string>(row["friendly_name"]), ConvertFromDBValue<string>(row["screenshot"]), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(ConvertFromDBValue<string>(row["file_name"])), ConvertFromDBValue<string>(row["screenshot"])), false, ConvertFromDBValue<string>(row["uploaded_by"]), ConvertFromDBValue<string>(row["current_user"]), ConvertFromDBValue<string>(row["reviewed_by"]), ConvertFromDBValue<string>(row["category"]), ConvertFromDBValue<int>(row["shadows"]), ConvertFromDBValue<int>(row["zUp"]), ConvertFromDBValue<int>(row["physicsGeometry"]));
                        of.Comments = comments;
                        approvedFileList.Add(of);
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

        /// <summary>
        /// Menu Helper method to add a new model to the database. 
        /// </summary>
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
                //batch import - for each file the user selected:
                foreach (string s in ofd.FileNames)
                {
                    //if we successfully can copy all of the files to the Model Directory
                    if (CopyFiles(s))
                    {
                        //add to database
                        addModelToDatabase(Path.GetFileName(s));

                        //update the file list
                        refreshFileLists();

                        //load the model into the interface
                        LoadModel(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(s), Path.GetFileName(s)));

                        //add the objects (parts) to the database
                        addObjectsToDatabase();

                        //refresh the sub-objects (parts) list
                        refreshSubObjects(getFileIdByFileName(this.CurrentModelPath));
                    }
                }

            }
        }

        /// <summary>
        /// Copy all of the files related to an object file to the resource directory.
        /// </summary>
        /// <param name="path">Path to object file.</param>
        /// <returns>True if all files copied successfully, false otherwise.</returns>
        private bool CopyFiles(string path)
        {
            //set up resource dir (model directory + filename without extension)
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

        /// <summary>
        /// Read through the .obj file and find all listings for .mtl files.
        /// </summary>
        /// <param name="path">Path to object file.</param>
        /// <returns>List of .mtl files in the object file.</returns>
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

        /// <summary>
        /// Read through the .mtl file and find all of the art assets.
        /// </summary>
        /// <param name="path">Path to .mtl file.</param>
        /// <returns>List of all image file art assets in the .mtl file.</returns>
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

        /// <summary>
        /// Add the model to the database.
        /// </summary>
        /// <param name="filename">Name of file to be added to the database.</param>
        private void addModelToDatabase(string filename)
        {
            string query = "INSERT INTO Files (file_name, friendly_name, uploaded_by) VALUES ('" + filename + "', '" + Path.GetFileNameWithoutExtension(filename) + "', '" + this.CurrentUser + "');";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Add the sub-objects (parts) to the databse.
        /// </summary>
        private void addObjectsToDatabase()
        {
            int fileId = getFileIdByFileName(this.currentModelPath);

            //for each entry in the subobjects list
            foreach (string s in getSubObjects())
            {
                try
                {
                    //insert into the database
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

        /// <summary>
        /// Load a model into the viewport and update the subobject display.
        /// </summary>
        /// <param name="of">ObjectFile representing file to be loaded.</param>
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

        /// <summary>
        /// Load a model into the viewport and update the subobject display.
        /// </summary>
        /// <param name="filename">Path to file to be loaded.</param>
        public void LoadModel(string filename)
        {
            this.CurrentModelPath = Path.Combine(modelDirectory, Path.GetFileNameWithoutExtension(filename), filename);
            this.CurrentModel = this.Load(this.CurrentModelPath, false);
            this.ApplicationTitle = string.Format(TitleFormatString, this.CurrentModelPath);
            this.viewport.ZoomExtents(0);

            //populate sub object display
            refreshSubObjects(getFileIdByFileName(this.CurrentModelPath));
        }

        /// <summary>
        /// Asynchronously load the model into memory from the model file.
        /// </summary>
        /// <param name="model3DPath">Path to model file.</param>
        /// <param name="freeze">Whether to freeze the model.</param>
        /// <returns>Model3DGroup that represents the model in the file.</returns>
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

        /// <summary>
        /// Load the model into memory from the model file.
        /// </summary>
        /// <param name="model3DPath">Path to model file.</param>
        /// <param name="freeze">Whether to freeze the model.</param>
        /// <returns>Model3DGroup that represents the model in the file.</returns>
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

        /// <summary>
        /// Delete multiple objects from the database.
        /// </summary>
        /// <param name="files">List of files to be deleted.</param>
        public void deleteObjects(System.Collections.IList files)
        {
            //reset viewport
            this.resetView();

            //for each objectfile in the list
            foreach (ObjectFile f in files)
            {
                //delete files
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

        /// <summary>
        /// Delete an object from the database.
        /// </summary>
        /// <param name="fileName">Name of file to be deleted.</param>
        public void deleteObject(string fileName)
        {
            //reset viewport
            this.resetView();

            //delete files 
            Directory.Delete(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(fileName)), true);

            //remove from database
            string query = "DELETE FROM Files WHERE file_name = '" + fileName + "';";
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();

            //refresh file lists
            refreshFileLists();
        }

        /// <summary>
        /// Assign a list of files to the current user.
        /// </summary>
        /// <param name="files">List of ObjectFiles to be assigned to current user.</param>
        public void assignFiles(System.Collections.IList files)
        {
            foreach (ObjectFile f in files)
            {
                assignFile(f.FileId, this.CurrentUser);
            }
            this.refreshFileLists();
        }

        /// <summary>
        /// Assign a list of files to a given user.
        /// </summary>
        /// <param name="files">List of ObjectFiles to be assigned.</param>
        /// <param name="username">User to assign files to.</param>
        public void assignFiles(System.Collections.IList files, string username)
        {
            foreach (ObjectFile f in files)
            {
                assignFile(f.FileId, username);
            }
            this.refreshFileLists();
        }

        /// <summary>
        /// Assign a single file to the current user.
        /// </summary>
        /// <param name="fileId">ID of file to be assigned.</param>
        public void assignFile(int fileId)
        {
            assignFile(fileId, this.CurrentUser);
            this.refreshFileLists();
        }

        /// <summary>
        /// Assign the given file to the given username.
        /// </summary>
        /// <param name="fileId">ID of file to be assigned.</param>
        /// <param name="username">Name of user to assign file to.</param>
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

        /// <summary>
        /// Set that a file tagging is complete and is ready for review in the database. Also updates the file lists after updating the database.
        /// </summary>
        /// <param name="fileId">ID of file to mark complete.</param>
        public void markFileComplete(int fileId)
        {
            string query = "UPDATE Files SET review_ready = 1, `current_user` = NULL WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();

            this.refreshFileLists();
        }

        /// <summary>
        /// Set that a file has been reviewed and is approved. Moves file to the Approved file list.
        /// </summary>
        /// <param name="fileId">ID of file to mark reviewed.</param>
        public void approveReview(int fileId)
        {
            string query = "UPDATE Files SET `reviewed_by` = '" + this.CurrentUser + "' WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();

            this.refreshFileLists();
        }

        /// <summary>
        /// Update the friendly name of a file in the database. The friendly name is used in the ANVEL export as the model name.
        /// </summary>
        /// <param name="fileName">Name of file to update.</param>
        /// <param name="friendlyName">New string to use as friendly name.</param>
        public void setFileFriendlyName(string fileName, string friendlyName)
        {
            int fileId = getFileIdByFileName(fileName);

            if (fileId != -1)
            {
                try
                {
                    string query = "UPDATE Files SET friendly_name = '" + friendlyName + "' WHERE file_id = " + fileId;
                    MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                    cmd.ExecuteNonQuery();
                }
                catch (MySqlException e)
                {
                    if (e.Number == 1062)
                    {
                        MessageBox.Show("A file with this name already exists. Please try again with a different name.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        System.Console.WriteLine(e.Message + " \n " + e.StackTrace);
                    }

                }
            }
        }

        /// <summary>
        /// Reload the model loaded in the viewport. Loads the model again, resetting any changes.
        /// </summary>
        /// <param name="resetSubObjects">If true, reset the sub-object list as well.</param>
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


        /// <summary>
        /// Reset the viewport. Removes the model from the viewport and resets associated UI elements.
        /// </summary>
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

        /// <summary>
        /// Gets the sub-objects (parts) of the model loaded in the viewport.
        /// </summary>
        /// <returns>List of the names of the parts in the model.</returns>
        private List<string> getSubObjects()
        {
            List<string> subObjects = new List<string>();

            foreach (GeometryModel3D gm in (CurrentModel as Model3DGroup).Children)
            {
                subObjects.Add(gm.GetName());
            }

            return subObjects;
        }

        /// <summary>
        /// Refresh the sub-object (parts) list. Used to update the UI parts list.
        /// </summary>
        /// <param name="fileId">ID of file to populate sub-object list.</param>
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

        /// <summary>
        /// Highlight a part of the model given the name of the part.
        /// </summary>
        /// <param name="name">Name of part of model to highlight.</param>
        public void highlightObjectByName(string name)
        {
            //reset model but don't change sub-object list (will remove any highlighting)
            resetModel(false);

            //if there is a model loaded in the viewport
            if (CurrentModel != null)
            {
                //loop through the parts until you find one with the right name, and highlight it
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

        /// <summary>
        /// Rename a sub-object (part) of the model.
        /// </summary>
        /// <param name="oldPart">Old part name</param>
        /// <param name="newPart">New part name</param>
        public void renamePartInModel(string oldPart, string newPart)
        {
            //modify obj file
            try
            {
                //update object name in database - do this first, if this throws exception, duplicate part name
                renameObject(oldPart, newPart);

                StreamReader reader = new StreamReader(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName));
                StreamWriter writer = new StreamWriter(new FileStream(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName) + ".temp", FileMode.Create));

                string temp = "";
                //loop through obj file
                while ((temp = reader.ReadLine()) != null)
                {
                    //if we found a group tag with the old group name
                    if (temp.StartsWith("g") && temp.Contains(oldPart))
                    {
                        //write a new group tag with the new part name
                        writer.WriteLine("g " + newPart);
                    }
                    else
                    {
                        //else, just write the line to file
                        writer.WriteLine(temp);
                    }
                }

                reader.Close();
                writer.Close();

                //backup current obj
                File.Copy(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName) + ".back" + System.DateTime.Now.ToFileTime());

                //copy temp to current obj
                File.Copy(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName) + ".temp", Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName), true);

                //Delete temp file
                File.Delete(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName) + ".temp");

                //refresh subobject list
                refreshSubObjects(this.ActiveFile.FileId);
            }
            catch (MySqlException e)
            {
                if (e.Number == 1062) //duplicate value
                {
                    MessageBox.Show("A part with this name already exists. Either merge the parts or pick a new part name.", "Duplicate Part", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    System.Console.WriteLine(e.Message);
                }

            }

        }

        /// <summary>
        /// Update the object in the database with the new object name.
        /// </summary>
        /// <param name="oldName">Old part name</param>
        /// <param name="newName">New part name</param>
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

        /// <summary>
        /// Merge a list of parts as a new part name
        /// </summary>
        /// <param name="oldParts">List of the old SubObject parts</param>
        /// <param name="newName">New name for part</param>
        public void mergeParts(IList<SubObject> oldParts, string newName)
        {
            //get a list of the old names
            List<string> oldPartNames = new List<string>();
            foreach (SubObject so in oldParts)
            {
                oldPartNames.Add(so.Name);
            }

            //modify obj file
            try
            {
                //update object name in database - do this first, if this throws exception, duplicate part name
                //if the part doesn't exist, or it part of the merge list, continue
                if (doesPartExist(newName) || oldPartNames.Contains(newName))
                {
                    //find all tags assigned to old parts 
                    string query = "SELECT DISTINCT tag_id, tagged_by FROM Object_Tag WHERE object_id = " + oldParts[0].Id;
                    for (int i = 1; i < oldParts.Count; i++)
                    {
                        query += " OR object_id = " + oldParts[i].Id;
                    }

                    MySqlDataAdapter adapter = new MySqlDataAdapter(query, sqlConnection);
                    DataTable table = new DataTable();
                    adapter.Fill(table);

                    //delete old parts from database
                    query = "DELETE FROM Objects WHERE object_id = " + oldParts[0].Id;
                    for (int i = 1; i < oldParts.Count; i++)
                    {
                        query += " OR object_id = " + oldParts[i].Id;
                    }
                    MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
                    cmd.ExecuteNonQuery();

                    //insert new part into database
                    query = "INSERT INTO Objects (file_id, object_name) VALUES (" + this.ActiveFile.FileId + ", '" + newName + "');";
                    cmd = new MySqlCommand(query, sqlConnection);
                    cmd.ExecuteNonQuery();

                    int newObjectId = getObjectId(this.ActiveFile.FileId, newName);

                    //add tags to new part
                    foreach (DataRow row in table.Rows)
                    {
                        query = "INSERT INTO Object_Tag (object_id, tag_id, tagged_by) VALUES (" + newObjectId + ", " + row["tag_id"] + ", '" + row["tagged_by"] + "')";
                        cmd = new MySqlCommand(query, sqlConnection);
                        cmd.ExecuteNonQuery();
                    }

                    StreamReader reader = new StreamReader(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName));
                    StreamWriter writer = new StreamWriter(new FileStream(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName) + ".temp", FileMode.Create));

                    string temp = "";
                    //loop through the obj file
                    while ((temp = reader.ReadLine()) != null)
                    {
                        //if we found a group tag and the group name is in the old parts list
                        if (temp.StartsWith("g") && stringContainsPart(temp, oldPartNames))
                        {
                            //write a new group tag with the new part name
                            writer.WriteLine("g " + newName);
                        }
                        else
                        {
                            //write the line to file
                            writer.WriteLine(temp);
                        }
                    }

                    reader.Close();
                    writer.Close();

                    //backup current obj
                    File.Copy(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName), Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName) + ".back" + System.DateTime.Now.ToFileTime());

                    //copy temp to current obj
                    File.Copy(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName) + ".temp", Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName), true);

                    //Delete temp file
                    File.Delete(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), this.ActiveFile.FileName) + ".temp");

                    //refresh the subobjects list
                    refreshSubObjects(this.ActiveFile.FileId);
                }
            }
            catch (MySqlException e)
            {
                if (e.Number == 1062) //duplicate
                {
                    MessageBox.Show("A part with this name already exists. Either merge the parts or pick a new part name.", "Duplicate Part", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    System.Console.WriteLine(e.Message + " \n " + e.StackTrace);
                }

            }
        }

        /// <summary>
        /// Check to see if some entry in a list of strings is contained by another string.
        /// </summary>
        /// <param name="s">String to be compared with.</param>
        /// <param name="p">List of strings to be checked.</param>
        /// <returns></returns>
        private bool stringContainsPart(string s, List<string> p)
        {
            //for each string in the list
            foreach (string part in p)
            {
                //if the string contains the part, return true
                if (s.Contains(part))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Method to see if a part exists in the database. 
        /// </summary>
        /// <param name="partName">Part name to check for existence in the database.</param>
        /// <returns>True if part already exists in database, false otherwise.</returns>
        private bool doesPartExist(string partName)
        {
            string query = "SELECT * FROM Objects WHERE object_name = '" + partName + "' AND file_id = " + this.ActiveFile.FileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            MySqlDataReader reader = cmd.ExecuteReader();

            if (reader.HasRows)
            {
                reader.Close();
                return true;
            }
            else
            {
                reader.Close();
                return false;
            }
        }

        /// <summary>
        /// Get a list of all categories already in the database. Used to populate dropdown in settings panel.
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Update the category for a file in the database.
        /// </summary>
        /// <param name="fileId">ID of file to update.</param>
        /// <param name="category">New category name.</param>
        public void setCategory(int fileId, string category)
        {
            string query = "UPDATE Files SET `category` = '" + category + "' WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Update the shadows property for a file in the database.
        /// </summary>
        /// <param name="fileId">ID of file to update.</param>
        /// <param name="category">New value, 1 for true and 0 for false.</param>
        public void setShadows(int fileId, int value)
        {
            string query = "UPDATE Files SET `shadows` = " + value + " WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Update the zUp property for a file in the database.
        /// </summary>
        /// <param name="fileId">ID of file to update.</param>
        /// <param name="category">New value, 1 for true and 0 for false.</param>
        public void setZUp(int fileId, int value)
        {
            string query = "UPDATE Files SET `zUp` = " + value + " WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Update the physics property for a file in the database.
        /// </summary>
        /// <param name="fileId">ID of file to update.</param>
        /// <param name="category">New value, 0 for mesh and 1 for bounding box.</param>
        public void setPhysicsGeometry(int fileId, int value)
        {
            string query = "UPDATE Files SET `physicsGeometry` = " + value + " WHERE file_id = " + fileId;
            MySqlCommand cmd = new MySqlCommand(query, sqlConnection);
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

        private int progressMax = 100;
        public int ProgressMax
        {
            get { return progressMax; }
            set
            {
                progressMax = value;
                RaisePropertyChanged("ProgressMax");
            }
        }

        public int ProgressValue
        {
            get { return this.percentageComplete; }
            set
            {
                this.percentageComplete = value;
                this.RaisePropertyChanged("ProgressValue");
            }
        }

        #endregion

        #region Menu Commands

        public ICommand FileOpenCommand { get; set; }

        public ICommand FileExportCommand { get; set; }

        public ICommand FileExportXMLCommand { get; set; }

        public ICommand FileExportANVELCommand { get; set; }

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

        private void FileExportANVELObject()
        {
            OgreMeshExporter oxe = new OgreMeshExporter();
            oxe.ParseAndConvertFileToXml(this.CurrentModelPath, Path.Combine(Path.GetDirectoryName(this.CurrentModelPath), Path.GetFileNameWithoutExtension(this.CurrentModelPath) + ".mesh.xml"));

            OgreMaterialExporter ome = new OgreMaterialExporter();
            ome.Export(Path.Combine(this.ModelDirectory, Path.GetFileNameWithoutExtension(this.ActiveFile.FileName), Path.GetFileNameWithoutExtension(this.ActiveFile.FileName) + ".mtl"));

            Process.Start(Path.GetDirectoryName(this.CurrentModelPath));
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

                refreshFileLists();

                this.ProgressValue = 0;
                this.ProgressMax = this.approvedFileList.Count + 10;

                await WriteXML(path, this.approvedFileList);

                this.ProgressValue = 10;

                CopyAnvelFiles(path, this.approvedFileList);
            }
        }

        private async Task WriteXML(string path, List<ObjectFile> objectList)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Async = true;

            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                await writer.WriteStartDocumentAsync();

                //7-17 Removed definitions tag and tag definitions from file as this broke ANVEL loading
                //start definitions
                //await writer.WriteStartElementAsync(null, "definitions", null);

                //start objects
                await writer.WriteStartElementAsync(null, "objects", null);

                ////start default preview
                await writer.WriteStartElementAsync(null, "defaultPreview", null);
                await writer.WriteAttributeStringAsync(null, "path", null, "Icons/DefaultPreview.png");
                await writer.WriteEndElementAsync();
                ////end default preview


                //for each file in the list
                foreach (ObjectFile of in objectList)
                {
                    ////start object elements
                    await writer.WriteStartElementAsync(null, "object", null);
                    await writer.WriteAttributeStringAsync(null, "name", null, of.FriendlyName);
                    await writer.WriteAttributeStringAsync(null, "category", null, of.Category);
                    await writer.WriteAttributeStringAsync(null, "preview", null, of.Screenshot);

                    //////start representations
                    await writer.WriteStartElementAsync(null, "representations", null);
                    ////////start ogre3d
                    await writer.WriteStartElementAsync(null, "ogre3d", null);
                    await writer.WriteAttributeStringAsync(null, "mesh", null, Path.GetFileNameWithoutExtension(of.FileName) + ".mesh");
                    if (of.Shadows.Equals(0))
                    {
                        await writer.WriteAttributeStringAsync(null, "shadows", null, "off");
                    }
                    else
                    {
                        await writer.WriteAttributeStringAsync(null, "shadows", null, "on");
                    }
                    if (of.ZUp.Equals(0))
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
                    await writer.WriteAttributeStringAsync(null, "mesh", null, of.FileName);
                    await writer.WriteEndElementAsync();
                    ////////end vane

                    await writer.WriteEndElementAsync();
                    //////end representations

                    ///////////////////////////////
                    /////START PARTS          /////
                    ///////////////////////////////

                    string partQuery = "SELECT * FROM Objects WHERE file_id = " + of.FileId;
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
                    await writer.WriteAttributeStringAsync(null, "mesh", null, Path.GetFileNameWithoutExtension(of.FileName) + ".mesh");
                    if (of.ZUp.Equals(0))
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
                //await writer.WriteStartElementAsync(null, "tags", null);

                //foreach (TagViewModel tag in rootTagView.Children)
                //{
                //    await writer.WriteStartElementAsync(null, "tag", null);
                //    await writer.WriteAttributeStringAsync(null, "name", null, tag.Name);
                //    await writer.WriteEndElementAsync();
                //    await WriteTagXml(writer, tag);
                //}

                //await writer.WriteEndElementAsync();
                //end tag hierarchy

                //await writer.WriteEndElementAsync();
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

        private void CopyAnvelFiles(string pathToXml, List<ObjectFile> objectFiles)
        {
            string outputDirectory = Path.Combine(Path.GetDirectoryName(pathToXml), "BinaryAssets", "Objects", "MTT-Export");
            Directory.CreateDirectory(outputDirectory);

            int updatePercent = 90 / objectFiles.Count;

            new Thread(() =>
                {
                    foreach (ObjectFile of in objectFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(of.FileName);
                        string fromDir = Path.Combine(this.ModelDirectory, fileName);
                        string toDir = Path.Combine(outputDirectory, fileName);
                        Directory.CreateDirectory(toDir);

                        //copy mesh
                        if (File.Exists(Path.Combine(fromDir, fileName + ".mesh")))
                        {
                            File.Copy(Path.Combine(fromDir, fileName + ".mesh"), Path.Combine(toDir, fileName + ".mesh"));
                        }
                        else
                        {
                            OgreMeshExporter oxe = new OgreMeshExporter();
                            oxe.ParseAndConvertFileToXml(Path.Combine(fromDir, fileName + ".obj"), Path.Combine(fromDir, fileName + ".mesh.xml"));

                            if (File.Exists(Path.Combine(fromDir, fileName + ".mesh")))
                            {
                                File.Copy(Path.Combine(fromDir, fileName + ".mesh"), Path.Combine(toDir, fileName + ".mesh"));
                            }
                            else
                            {
                                Console.WriteLine("Error creating mesh for " + fileName);
                            }
                        }

                        //copy material
                        if (File.Exists(Path.Combine(fromDir, fileName + ".material")))
                        {
                            File.Copy(Path.Combine(fromDir, fileName + ".material"), Path.Combine(toDir, fileName + ".material"));
                        }
                        else
                        {
                            OgreMaterialExporter ome = new OgreMaterialExporter();
                            ome.Export(Path.Combine(Path.Combine(fromDir, fileName + ".mtl")));

                            if (File.Exists(Path.Combine(fromDir, fileName + ".material")))
                            {
                                File.Copy(Path.Combine(fromDir, fileName + ".material"), Path.Combine(toDir, fileName + ".material"));
                            }
                            else
                            {
                                Console.WriteLine("Error creating material for " + fileName);
                            }
                        }

                        //copy art assets (PNG & BMP)
                        var files = Directory.EnumerateFiles(fromDir, "*.*", SearchOption.TopDirectoryOnly);
                        foreach (string f in files)
                        {
                            if (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                            {
                                File.Copy(Path.Combine(fromDir, Path.GetFileName(f)), Path.Combine(toDir, Path.GetFileName(f)));
                            }
                        }

                        this.ProgressValue++;

                    }
                }).Start();

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