using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace ModelViewer
{
    public class SubObjectViewModel : INotifyPropertyChanged
    {
        readonly SubObject subObject;
        readonly SubObjectViewModel parent;
        readonly ReadOnlyCollection<SubObjectViewModel> children;

        bool isExpanded;
        bool isSelected;
        bool isChecked;

        public SubObjectViewModel(SubObject subObject)
            : this(subObject, null)
        {

        }

        private SubObjectViewModel(SubObject subObject, SubObjectViewModel parent)
        {
            this.subObject = subObject;
            this.parent = parent;

            children = new ReadOnlyCollection<SubObjectViewModel>((from child in subObject.Children select new SubObjectViewModel(child, this)).ToList<SubObjectViewModel>());
        }

        public ReadOnlyCollection<SubObjectViewModel> Children
        {
            get { return this.children; }
        }

        public string Name
        {
            get { return this.subObject.Name; }
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
            foreach (SubObjectViewModel obj in this.Children)
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

        public SubObjectViewModel GetSelectedItem()
        {
            SubObjectViewModel sovm = null;
            if (this.IsSelected)
            {
                sovm = this;
            }
            else
            {
                foreach (SubObjectViewModel s in this.Children)
                {
                    if (s.IsSelected)
                    {
                        sovm = s;
                        break;
                    }
                }
            }
            return sovm;
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

        public SubObjectViewModel Parent
        {
            get { return parent; }
        }

        public bool NameContainsText(string text)
        {
            if (String.IsNullOrEmpty(text) || String.IsNullOrEmpty(this.Name))
                return false;

            return this.Name.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) > -1;
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
