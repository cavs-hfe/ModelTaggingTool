using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelViewer
{
    public class SubObject
    {
        readonly List<SubObject> children = new List<SubObject>();
        public IList<SubObject> Children
        {
            get
            {
                return children;
            }
        }

        public string Name { get; set; }
        public int Id { get; set; }

        public SubObject(int id, string name)
        {
            this.Id = id;
            this.Name = name;
        }

    }
}
