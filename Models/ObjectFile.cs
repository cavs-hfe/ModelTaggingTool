using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ModelViewer
{
    public class ObjectFile
    {
        public const int SHADOWS_FALSE = 0;
        public const int SHADOWS_TRUE = 1;
        public const int Z_UP = 1;
        public const int Y_UP = 0;
        public const int PHYSICS_MESH = 0;
        public const int PHYSICS_BOUNDING_BOX = 1;

        readonly List<ObjectFile> children = new List<ObjectFile>();
        public IList<ObjectFile> Children
        {
            get
            {
                return children;
            }
        }

        public string FileName { get; set; }
        public string FriendlyName { get; set; }
        public int FileId { get; set; }
        public string Category { get; set; }

        public string Screenshot { get; set; }
        public bool HasScreenshot { get; set; }
        public string FullScreenshotPath { get; set; }
        public ImageSource ScreenshotImageSource 
        {
            get 
            {
                BitmapImage image = new BitmapImage();

                try
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    image.UriSource = new Uri(this.FullScreenshotPath, UriKind.Absolute);
                    image.EndInit();
                }
                catch
                {
                    return null;
                }

                return image;
            } 
        }



        public bool HasTags { get; set; }

        public string UploadedBy { get; set; }
        public string CurrentUser { get; set; }
        public string ReviewedBy { get; set; }

        public int Shadows { get; set; }
        public int ZUp { get; set; }
        public int PhysicsGeometry { get; set; }

        public string Comments { get; set; }

        public ObjectFile(string fileName, string friendlyName)
            : this(-1, fileName, friendlyName, "", "", false, "", "", "", "", "", 0, 0, 0) { }

        public ObjectFile(int fileId, string fileName, string friendlyName, string screenshot, string fullScreenshotPath, bool hasTags, string uploadedBy, string currentUser, string reviewedBy, string comments, string category, int shadows, int zUp, int physicsGeometry)
        {
            this.FileId = fileId;
            this.FileName = fileName;
            this.FriendlyName = friendlyName;
            this.Screenshot = screenshot;
            if (screenshot != "")
            {
                this.HasScreenshot = true;
                this.FullScreenshotPath = fullScreenshotPath;
            }
            else
            {
                this.HasScreenshot = false;
            }
            this.HasTags = hasTags;
            this.UploadedBy = uploadedBy;
            this.CurrentUser = currentUser;
            this.ReviewedBy = reviewedBy;
            this.Comments = comments;
            this.Category = category;
            this.Shadows = shadows;
            this.ZUp = zUp;
            this.PhysicsGeometry = physicsGeometry;
        }

    }
}
