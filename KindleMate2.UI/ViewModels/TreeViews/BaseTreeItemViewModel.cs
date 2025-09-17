using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KindleMate2.UI.ViewModels.TreeViews {
    public abstract class BaseTreeItemViewModel : INotifyPropertyChanged {
        private string _name;
        public string Name {
            get => _name;
            set {
                if (_name != value) {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public ObservableCollection<BaseTreeItemViewModel> Children { get; set; }
            = new ObservableCollection<BaseTreeItemViewModel>();

        private bool _isExpanded;
        public bool IsExpanded {
            get => _isExpanded;
            set {
                if (_isExpanded != value) {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected {
            get => _isSelected;
            set {
                if (_isSelected != value) {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
