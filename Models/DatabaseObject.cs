using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SQLAtlas.Models
{
    public class DatabaseObject : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _schemaName = string.Empty;
        private string _fullName = string.Empty;
        private string _type = string.Empty;
        private string? _definition;
        private int _itemCount;

        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string SchemaName { get => _schemaName; set { _schemaName = value; OnPropertyChanged(); } }
        public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); } }
        public string Type { get => _type; set { _type = value; OnPropertyChanged(); } }
        public string? Definition { get => _definition; set { _definition = value; OnPropertyChanged(); } }
        public int ItemCount { get => _itemCount; set { _itemCount = value; OnPropertyChanged(); } }

        public string TypeDescription { get => Type; set => Type = value; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}