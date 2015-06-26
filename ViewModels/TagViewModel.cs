using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace ModelViewer
{
    public class TagViewModel : INotifyPropertyChanged
    {
        readonly Tag tag;
        readonly TagViewModel parent;
        readonly ReadOnlyCollection<TagViewModel> children;

        bool isExpanded;
        bool isSelected;
        bool isChecked;
        bool isEnabled = true;

        bool isReviewed = true;

        public TagViewModel(Tag tag)
            : this(tag, null)
        {

        }

        private TagViewModel(Tag tag, TagViewModel parent)
        {
            this.tag = tag;
            this.parent = parent;

            children = new ReadOnlyCollection<TagViewModel>((from child in tag.Children select new TagViewModel(child, this)).ToList<TagViewModel>());
        }

        public ReadOnlyCollection<TagViewModel> Children
        {
            get { return this.children; }
        }

        public string Name
        {
            get { return this.tag.Name; }
        }

        public int Id
        {
            get { return this.tag.Id; }
        }

        public int ParentId
        {
            get { return this.tag.ParentId; }
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
            foreach (TagViewModel obj in this.Children)
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

                //uncheck all the way down to leaf
                if (!isChecked && children != null)
                {
                    foreach (TagViewModel child in children)
                    {
                        child.IsChecked = false;
                    }
                }
            }
        }

        public void Check(string tagName)
        {
            if (this.Name.Equals(tagName))
            {
                this.IsChecked = true;
            }
            else
            {
                foreach (TagViewModel t in this.Children)
                {
                    t.Check(tagName);
                }
            }
        }

        public bool IsEnabled
        {
            get { return isEnabled; }
            set
            {
                if (value != isEnabled)
                {
                    isEnabled = value;
                    this.OnPropertyChanged("IsEnabled");

                    //enable or disable all the way down to leaf
                    if (children != null)
                    {
                        foreach (TagViewModel child in children)
                        {
                            child.IsEnabled = isEnabled;
                        }
                    }
                }
            }
        }

        public bool IsReviewed
        {
            get { return isReviewed; }
            set
            {
                if (value != isReviewed)
                {
                    isReviewed = value;
                    this.OnPropertyChanged("IsReviewed");
                }
            }
        }

        public TagViewModel GetTagViewModelByName(string tagName)
        {

            if (this.Name.Equals(tagName))
            {
                return this;
            }
            foreach (TagViewModel t in this.Children)
            {
                TagViewModel m = t.GetTagViewModelByName(tagName);
                if (m != null)
                {
                    return m;
                }
            }
            return null;

        }

        public TagViewModel Parent
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