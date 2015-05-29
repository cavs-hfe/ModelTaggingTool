using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelViewer
{
    public class ObjectFileViewModel : INotifyPropertyChanged
    {
        readonly ObjectFile objectFile;
        readonly ObjectFileViewModel parent;
        readonly ReadOnlyCollection<ObjectFileViewModel> children;

        bool isExpanded;
        bool isSelected;
        bool isChecked;

        public ObjectFileViewModel(ObjectFile objectFile)
            : this(objectFile, null)
        {

        }

        private ObjectFileViewModel(ObjectFile objectFile, ObjectFileViewModel parent)
        {
            this.objectFile = objectFile;
            this.parent = parent;

            children = new ReadOnlyCollection<ObjectFileViewModel>((from child in objectFile.Children select new ObjectFileViewModel(child, this)).ToList<ObjectFileViewModel>());
        }

        public ReadOnlyCollection<ObjectFileViewModel> Children
        {
            get { return this.children; }
        }

        public string FileName
        {
            get { return this.objectFile.FileName; }
        }

        public string FriendlyName
        {
            get { return this.objectFile.FriendlyName; }
        }

        public int ObjectId
        {
            get { return this.objectFile.ObjectId; }
        }

        /// <summary>
        /// Gets/sets whether the TreeViewItem 
        /// associated with this object is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get { return isExpanded; }
            set
            {
                if (value != isExpanded)
                {
                    isExpanded = value;
                    this.OnPropertyChanged("IsExpanded");
                }

                // Expand all the way up to the root.
                if (isExpanded && parent != null)
                    parent.IsExpanded = true;
            }
        }

        public void ExpandAll()
        {
            this.IsExpanded = true;
            foreach (ObjectFileViewModel obj in this.Children)
            {
                obj.IsExpanded = true;
                obj.ExpandAll();
            }
        }

        /// <summary>
        /// Gets/sets whether the TreeViewItem 
        /// associated with this object is selected.
        /// </summary>
        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (value != isSelected)
                {
                    isSelected = value;
                    this.OnPropertyChanged("IsSelected");
                }
            }
        }

        /// <summary>
        /// Gets/sets whether the TreeViewItem 
        /// associated with this object is checked.
        /// </summary>
        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                if (value != isChecked)
                {
                    isChecked = value;
                    this.OnPropertyChanged("IsChecked");
                }

                // Expand all the way up to the root.
                if (isChecked && parent != null)
                {
                    parent.IsChecked = true;
                }
            }
        }

        public ObjectFileViewModel Parent
        {
            get { return parent; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
