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
        public int ObjectId { get; set; }

        public ObjectFile(string fileName, string friendlyName)
        {
            this.FileName = fileName;
            this.FriendlyName = friendlyName;
        }

    }
}
