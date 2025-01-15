using System.ComponentModel;

namespace DatabaseMigrator.Models
{
  public class StoredProcedureItem : INotifyPropertyChanged
  {
    private bool _isSelected;
    public string ProcedureName { get; set; }

    public bool IsSelected
    {
      get => _isSelected;
      set
      {
        if (_isSelected != value)
        {
          _isSelected = value;
          OnPropertyChanged(nameof(IsSelected));
        }
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}
