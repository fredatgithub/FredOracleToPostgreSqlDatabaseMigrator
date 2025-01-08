using DatabaseMigrator.Helpers;
using Newtonsoft.Json;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;

namespace DatabaseMigrator
{
  public partial class MainWindow : Window
  {
    private readonly string _oracleCredentialsFile = "id_oracle.txt";
    private readonly string _pgCredentialsFile = "id_pg.txt";
    private readonly string _logFile = "log.txt";

    public MainWindow()
    {
      InitializeComponent();
      LoadSavedCredentials();
      LoadLogs();

      // Wire up button click events
      btnTestOracle.Click += BtnTestOracle_Click;
      btnTestPostgres.Click += BtnTestPostgres_Click;
      btnLoadOracleTables.Click += BtnLoadOracleTables_Click;
      btnLoadPostgresTables.Click += BtnLoadPostgresTables_Click;

      // Wire up search text changed events
      txtOracleSearch.TextChanged += TxtOracleSearch_TextChanged;
      txtPostgresSearch.TextChanged += TxtPostgresSearch_TextChanged;

      // Wire up selection changed events
      lstOracleTables.SelectionChanged += LstOracleTables_SelectionChanged;
      lstPostgresTables.SelectionChanged += LstPostgresTables_SelectionChanged;
    }

    private void LstOracleTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateOracleSelectedCount();
    }

    private void LstPostgresTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdatePostgresSelectedCount();
    }

    private void TxtOracleSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
      FilterOracleTables();
    }

    private void TxtPostgresSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
      FilterPostgresTables();
    }

    private void FilterOracleTables()
    {
      if (lstOracleTables.ItemsSource is System.Collections.IEnumerable items)
      {
        var view = CollectionViewSource.GetDefaultView(items);
        var searchText = txtOracleSearch.Text.ToUpperInvariant();
        view.Filter = item => string.IsNullOrEmpty(searchText) || 
                             (item as TableInfo)?.TableName.ToUpperInvariant().Contains(searchText) == true;
      }
    }

    private void FilterPostgresTables()
    {
      if (lstPostgresTables.ItemsSource is System.Collections.IEnumerable items)
      {
        var view = CollectionViewSource.GetDefaultView(items);
        var searchText = txtPostgresSearch.Text.ToUpperInvariant();
        view.Filter = item => string.IsNullOrEmpty(searchText) || 
                             (item as TableInfo)?.TableName.ToUpperInvariant().Contains(searchText) == true;
      }
    }

    private async void BtnLoadOracleTables_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        btnLoadOracleTables.IsEnabled = false;
        ResetButtonColor(btnLoadOracleTables);
        LogMessage("Loading Oracle tables...");

        string connectionString = $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={txtOracleServer.Text})(PORT={txtOraclePort.Text}))(CONNECT_DATA=(SERVICE_NAME={txtOracleServiceName.Text})));User Id={txtOracleUser.Text};Password={pwdOraclePassword.Password};";

        using (var connection = new OracleConnection(connectionString))
        {
          await connection.OpenAsync();
          var tables = new List<TableInfo>();

          using (var cmd = connection.CreateCommand())
          {
            cmd.CommandText = @"
              SELECT 
                  t.table_name,
                  (SELECT COUNT(*) FROM all_objects WHERE owner = t.owner AND object_name = t.table_name) as row_count
              FROM 
                  all_tables t
              WHERE 
                  t.owner = :owner
              ORDER BY 
                  t.table_name";

            cmd.Parameters.Add(new OracleParameter("owner", txtOracleUser.Text.ToUpper()));

            using (var reader = await cmd.ExecuteReaderAsync())
            {
              while (await reader.ReadAsync())
              {
                tables.Add(new TableInfo 
                { 
                  TableName = reader.GetString(0),
                  RowCount = reader.GetInt64(1)
                });
              }
            }
          }

          lstOracleTables.ItemsSource = tables;
          LogMessage($"Successfully loaded {tables.Count} Oracle tables ✓");
          SetButtonSuccess(btnLoadOracleTables);
        }
      }
      catch (Exception ex)
      {
        LogMessage($"Failed to load Oracle tables: {ex.Message} ✗");
        SetButtonError(btnLoadOracleTables);
      }
      finally
      {
        btnLoadOracleTables.IsEnabled = true;
      }
    }

    private async void BtnLoadPostgresTables_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        btnLoadPostgresTables.IsEnabled = false;
        ResetButtonColor(btnLoadPostgresTables);
        LogMessage("Loading PostgreSQL tables...");

        string connectionString = $"Host={txtPostgresServer.Text};Port={txtPostgresPort.Text};Database={txtPostgresDatabase.Text};Username={txtPostgresUser.Text};Password={pwdPostgresPassword.Password}";

        using (var connection = new NpgsqlConnection(connectionString))
        {
          await connection.OpenAsync();
          var tables = new List<TableInfo>();

          using (var cmd = connection.CreateCommand())
          {
            var schema = txtPostgresSchema.Text;
            cmd.CommandText = @"
              SELECT 
                  t.tablename,
                  COALESCE(c.reltuples::bigint, 0) as row_count
              FROM 
                  pg_catalog.pg_tables t
                  JOIN pg_catalog.pg_class c ON c.relname = t.tablename
                  JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
              WHERE 
                  t.schemaname = @schema
              ORDER BY 
                  t.tablename";

            cmd.Parameters.AddWithValue("schema", txtPostgresSchema.Text);

            using (var reader = await cmd.ExecuteReaderAsync())
            {
              while (await reader.ReadAsync())
              {
                tables.Add(new TableInfo 
                { 
                  TableName = reader.GetString(0),
                  RowCount = reader.GetInt64(1)
                });
              }
            }
          }

          lstPostgresTables.ItemsSource = tables;
          LogMessage($"Successfully loaded {tables.Count} PostgreSQL tables ✓");
          SetButtonSuccess(btnLoadPostgresTables);
        }
      }
      catch (Exception ex)
      {
        LogMessage($"Failed to load PostgreSQL tables: {ex.Message} ✗");
        SetButtonError(btnLoadPostgresTables);
      }
      finally
      {
        btnLoadPostgresTables.IsEnabled = true;
      }
    }

    private void LoadSavedCredentials()
    {
      try
      {
        // Charger les identifiants Oracle
        if (File.Exists(_oracleCredentialsFile))
        {
          var encryptedOracle = File.ReadAllText(_oracleCredentialsFile);
          var decryptedOracle = EncryptionHelper.Decrypt(encryptedOracle);
          if (!string.IsNullOrEmpty(decryptedOracle))
          {
            var oracleCredentials = JsonConvert.DeserializeObject<DbCredentials>(decryptedOracle);
            txtOracleServer.Text = oracleCredentials.Server;
            txtOraclePort.Text = oracleCredentials.Port;
            txtOracleServiceName.Text = oracleCredentials.Database;
            txtOracleUser.Text = oracleCredentials.Username;
            pwdOraclePassword.Password = oracleCredentials.Password;
            chkSaveOracle.IsChecked = true;
          }
        }

        // Charger les identifiants PostgreSQL
        if (File.Exists(_pgCredentialsFile))
        {
          var encryptedPg = File.ReadAllText(_pgCredentialsFile);
          var decryptedPg = EncryptionHelper.Decrypt(encryptedPg);
          if (!string.IsNullOrEmpty(decryptedPg))
          {
            var pgCredentials = JsonConvert.DeserializeObject<DbCredentials>(decryptedPg);
            txtPostgresServer.Text = pgCredentials.Server;
            txtPostgresPort.Text = pgCredentials.Port;
            txtPostgresDatabase.Text = pgCredentials.Database;
            txtPostgresSchema.Text = pgCredentials.Schema;
            txtPostgresUser.Text = pgCredentials.Username;
            pwdPostgresPassword.Password = pgCredentials.Password;
            chkSavePostgres.IsChecked = true;
          }
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Error loading credentials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void SaveCredentials()
    {
      try
      {
        // Sauvegarder les identifiants Oracle
        if (chkSaveOracle.IsChecked == true)
        {
          var oracleCredentials = new DbCredentials
          {
            Server = txtOracleServer.Text,
            Port = txtOraclePort.Text,
            Database = txtOracleServiceName.Text,
            Username = txtOracleUser.Text,
            Password = pwdOraclePassword.Password
          };

          var jsonOracle = JsonConvert.SerializeObject(oracleCredentials);
          var encryptedOracle = EncryptionHelper.Encrypt(jsonOracle);
          File.WriteAllText(_oracleCredentialsFile, encryptedOracle);
        }
        else if (File.Exists(_oracleCredentialsFile))
        {
          File.Delete(_oracleCredentialsFile);
        }

        // Sauvegarder les identifiants PostgreSQL
        if (chkSavePostgres.IsChecked == true)
        {
          var pgCredentials = new DbCredentials
          {
            Server = txtPostgresServer.Text,
            Port = txtPostgresPort.Text,
            Database = txtPostgresDatabase.Text,
            Schema = txtPostgresSchema.Text,
            Username = txtPostgresUser.Text,
            Password = pwdPostgresPassword.Password
          };

          var jsonPg = JsonConvert.SerializeObject(pgCredentials);
          var encryptedPg = EncryptionHelper.Encrypt(jsonPg);
          File.WriteAllText(_pgCredentialsFile, encryptedPg);
        }
        else if (File.Exists(_pgCredentialsFile))
        {
          File.Delete(_pgCredentialsFile);
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Error saving credentials: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void LoadLogs()
    {
      try
      {
        if (File.Exists(_logFile))
        {
          txtLogs.Text = File.ReadAllText(_logFile);
          txtLogs.ScrollToEnd();
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Error loading logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void SaveLogs()
    {
      try
      {
        File.WriteAllText(_logFile, txtLogs.Text);
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Error saving logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void LogMessage(string message)
    {
      string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
      txtLogs.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
      txtLogs.ScrollToEnd();
    }

    private void ResetButtonColor(Button button)
    {
      button.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
    }

    private void SetButtonSuccess(Button button)
    {
      button.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69));
    }

    private void SetButtonError(Button button)
    {
      button.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
    }

    private async void BtnTestOracle_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        btnTestOracle.IsEnabled = false;
        ResetButtonColor(btnTestOracle);
        LogMessage("Testing Oracle connection...");

        // Build the connection string
        string connectionString = $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={txtOracleServer.Text})(PORT={txtOraclePort.Text}))(CONNECT_DATA=(SERVICE_NAME={txtOracleServiceName.Text})));User Id={txtOracleUser.Text};Password={pwdOraclePassword.Password};";

        using (var connection = new OracleConnection(connectionString))
        {
          await connection.OpenAsync();
          LogMessage("Oracle connection successful! ✓");
          SetButtonSuccess(btnTestOracle);
        }
      }
      catch (Exception ex)
      {
        LogMessage($"Oracle connection failed: {ex.Message} ✗");
        SetButtonError(btnTestOracle);
      }
      finally
      {
        btnTestOracle.IsEnabled = true;
      }
    }

    private async void BtnTestPostgres_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        btnTestPostgres.IsEnabled = false;
        ResetButtonColor(btnTestPostgres);
        LogMessage("Testing PostgreSQL connection...");

        // Build the connection string
        string connectionString = $"Host={txtPostgresServer.Text};Port={txtPostgresPort.Text};Database={txtPostgresDatabase.Text};Username={txtPostgresUser.Text};Password={pwdPostgresPassword.Password}";

        using (var connection = new NpgsqlConnection(connectionString))
        {
          await connection.OpenAsync();
          LogMessage("PostgreSQL connection successful! ✓");
          SetButtonSuccess(btnTestPostgres);
        }
      }
      catch (Exception ex)
      {
        LogMessage($"PostgreSQL connection failed: {ex.Message} ✗");
        SetButtonError(btnTestPostgres);
      }
      finally
      {
        btnTestPostgres.IsEnabled = true;
      }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      // Restaurer la position et la taille de la fenêtre
      var settings = Properties.Settings.Default;

      // Si c'est la première fois que l'application est lancée, centrer la fenêtre
      if (settings.WindowTop == 0 && settings.WindowLeft == 0)
      {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
      }
      else
      {
        // Vérifier si la fenêtre sera visible sur l'écran
        bool isVisible = false;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
          var rect = new System.Drawing.Rectangle(
              (int)settings.WindowLeft,
              (int)settings.WindowTop,
              (int)settings.WindowWidth,
              (int)settings.WindowHeight);

          if (screen.WorkingArea.IntersectsWith(rect))
          {
            isVisible = true;
            break;
          }
        }

        if (isVisible)
        {
          Top = settings.WindowTop;
          Left = settings.WindowLeft;
          Height = settings.WindowHeight;
          Width = settings.WindowWidth;
          WindowState = settings.WindowState;
        }
        else
        {
          WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
      }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
      // Sauvegarder la position et la taille de la fenêtre
      var settings = Properties.Settings.Default;

      if (WindowState == WindowState.Normal)
      {
        settings.WindowTop = Top;
        settings.WindowLeft = Left;
        settings.WindowHeight = Height;
        settings.WindowWidth = Width;
      }
      else
      {
        settings.WindowTop = RestoreBounds.Top;
        settings.WindowLeft = RestoreBounds.Left;
        settings.WindowHeight = RestoreBounds.Height;
        settings.WindowWidth = RestoreBounds.Width;
      }

      settings.WindowState = WindowState;
      settings.Save();

      // Sauvegarder les identifiants
      SaveCredentials();
      
      // Sauvegarder les logs
      SaveLogs();
    }

    private void UpdateOracleSelectedCount()
    {
      if (lstOracleTables.ItemsSource is IEnumerable<TableInfo> tables)
      {
        txtOracleSelectedCount.Text = tables.Count(t => t.IsSelected).ToString();
      }
    }

    private void UpdatePostgresSelectedCount()
    {
      if (lstPostgresTables.ItemsSource is IEnumerable<TableInfo> tables)
      {
        txtPostgresSelectedCount.Text = tables.Count(t => t.IsSelected).ToString();
      }
    }

    public class DbCredentials
    {
      public string Server { get; set; }
      public string Port { get; set; }
      public string Database { get; set; }
      public string Schema { get; set; }
      public string Username { get; set; }
      public string Password { get; set; }
    }

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
}
