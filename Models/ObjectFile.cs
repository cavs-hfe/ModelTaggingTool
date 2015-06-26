using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelViewer
{
    public class ObjectFile
    {
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

        public string Screenshot { get; set; }
        public bool HasScreenshot { get; set; }

        public bool HasTags { get; set; }

        public string UploadedBy { get; set; }
        public string CurrentUser { get; set; }
        public string ReviewedBy { get; set; }

        

        public ObjectFile(string fileName, string friendlyName) 
            : this(-1, fileName, friendlyName, "", false, "", "", "") { }

        public ObjectFile(int fileId, string fileName, string friendlyName, string screenshot, bool hasTags, string uploadedBy, string currentUser, string reviewedBy)
        {
            this.FileId = fileId;
            this.FileName = fileName;
            this.FriendlyName = friendlyName;
            this.Screenshot = screenshot;
            if (screenshot != "")
            {
                this.HasScreenshot = true;
            }
            else
            {
                this.HasScreenshot = false;
            }
            this.HasTags = hasTags;
            this.UploadedBy = uploadedBy;
            this.CurrentUser = currentUser;
            this.ReviewedBy = reviewedBy;
        }

    }
}
