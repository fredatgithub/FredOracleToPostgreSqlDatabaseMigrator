using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace DatabaseMigrator.Models
{
    public class TableInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string TableName { get; set; }
        public long RowCount { get; set; }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    // Déclencher la mise à jour du compteur
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var mainWindow = Application.Current.MainWindow;
                        if (mainWindow != null)
                        {
                            var lstOracleTables = mainWindow.FindName("lstOracleTables") as ListView;
                            if (lstOracleTables != null && lstOracleTables.Items.Contains(this))
                            {
                                dynamic window = mainWindow;
                                window.UpdateOracleSelectedCount();
                            }
                            else
                            {
                                dynamic window = mainWindow;
                                window.UpdatePostgresSelectedCount();
                            }
                        }
                    }));
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
