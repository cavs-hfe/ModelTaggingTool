using System.Collections.Generic;

namespace ModelViewer
{
    public class Tag
    {
        readonly List<Tag> children = new List<Tag>();
        public IList<Tag> Children
        {
            get
            {
                return children;
            }
        }
        public string Name { get; set; }
        public int Id { get; set; }
        public int ParentId { get; set; }

        public Tag(int id, string name, int parentId)
        {
            this.Id = id;
            this.Name = name;
            this.ParentId = parentId;
        }
    }
}
