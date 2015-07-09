using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ModelViewer.Models
{
    public class Comment
    {
        public int Id { get; set; }
        public string User { get; set; }
        public string CommentText { get; set; }
        public DateTime Timestamp { get; set; }
        public Brush Color { get; set; }

        public Comment() : this("", "", DateTime.Now, Brushes.Black) { }

        public Comment(string user, string comment, DateTime timestamp, Brush color)
        {
            this.User = user;
            this.CommentText = comment;
            this.Timestamp = timestamp;
            this.Color = color;
        }

    }
}
