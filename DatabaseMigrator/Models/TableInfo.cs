using System;
using System.ComponentModel;
using System.Windows;

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
                        if (Application.Current.MainWindow is MainWindow mainWindow)
                        {
                            // Mettre à jour le compteur approprié en fonction de la ListView
                            var listView = mainWindow.lstOracleTables;
                            var countText = mainWindow.txtOracleSelectedCount;
                            
                            if (listView.Items.Contains(this))
                            {
                                mainWindow.UpdateOracleSelectedCount();
                            }
                            else
                            {
                                mainWindow.UpdatePostgresSelectedCount();
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
