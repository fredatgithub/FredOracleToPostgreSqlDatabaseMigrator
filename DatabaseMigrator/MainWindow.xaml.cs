using DatabaseMigrator.Helpers;
using Newtonsoft.Json;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Data;
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

    private void LstOracleTables_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      txtOracleSelectedCount.Text = lstOracleTables.SelectedItems.Count.ToString();
    }

    private void LstPostgresTables_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      txtPostgresSelectedCount.Text = lstPostgresTables.SelectedItems.Count.ToString();
    }

    private void TxtOracleSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
      FilterOracleTables();
    }

    private void TxtPostgresSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
      FilterPostgresTables();
    }

    private void FilterOracleTables()
    {
      if (lstOracleTables.ItemsSource is System.Collections.IEnumerable items)
      {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(items);
        var searchText = txtOracleSearch.Text.ToUpperInvariant();
        view.Filter = item => string.IsNullOrEmpty(searchText) || 
                             (item as TableInfo)?.TableName.ToUpperInvariant().Contains(searchText) == true;
      }
    }

    private void FilterPostgresTables()
    {
      if (lstPostgresTables.ItemsSource is System.Collections.IEnumerable items)
      {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(items);
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
                  table_name, 
                  num_rows 
              FROM 
                  all_tables 
              WHERE 
                  owner = :owner 
              ORDER BY 
                  table_name";
            
            cmd.Parameters.Add(new OracleParameter("owner", txtOracleUser.Text.ToUpper()));

            using (var reader = await cmd.ExecuteReaderAsync())
            {
              while (await reader.ReadAsync())
              {
                tables.Add(new TableInfo
                {
                  TableName = reader.GetString(0),
                  RowCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
                });
              }
            }
          }

          lstOracleTables.ItemsSource = tables;
          LogMessage($"Loaded {tables.Count} Oracle tables ✓");
        }
      }
      catch (Exception ex)
      {
        LogMessage($"Failed to load Oracle tables: {ex.Message} ✗");
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
        LogMessage("Loading PostgreSQL tables...");

        string connectionString = $"Host={txtPostgresServer.Text};Port={txtPostgresPort.Text};Database={txtPostgresDatabase.Text};Username={txtPostgresUser.Text};Password={pwdPostgresPassword.Password}";

        using (var connection = new NpgsqlConnection(connectionString))
        {
          await connection.OpenAsync();

          var tables = new List<TableInfo>();
          using (var cmd = connection.CreateCommand())
          {
            cmd.CommandText = @"
              SELECT 
                  tablename,
                  n_live_tup
              FROM 
                  pg_catalog.pg_tables t
                  JOIN pg_catalog.pg_stat_user_tables s ON s.relname = t.tablename
              WHERE 
                  schemaname = 'public'
              ORDER BY 
                  tablename";

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
          LogMessage($"Loaded {tables.Count} PostgreSQL tables ✓");
        }
      }
      catch (Exception ex)
      {
        LogMessage($"Failed to load PostgreSQL tables: {ex.Message} ✗");
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

    private async void BtnTestOracle_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        btnTestOracle.IsEnabled = false;
        LogMessage("Testing Oracle connection...");

        // Build the connection string
        string connectionString = $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={txtOracleServer.Text})(PORT={txtOraclePort.Text}))(CONNECT_DATA=(SERVICE_NAME={txtOracleServiceName.Text})));User Id={txtOracleUser.Text};Password={pwdOraclePassword.Password};";

        using (var connection = new OracleConnection(connectionString))
        {
          await connection.OpenAsync();
          LogMessage("Oracle connection successful! ✓");
        }
      }
      catch (Exception ex)
      {
        LogMessage($"Oracle connection failed: {ex.Message} ✗");
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
        LogMessage("Testing PostgreSQL connection...");

        // Build the connection string
        string connectionString = $"Host={txtPostgresServer.Text};Port={txtPostgresPort.Text};Database={txtPostgresDatabase.Text};Username={txtPostgresUser.Text};Password={pwdPostgresPassword.Password}";

        using (var connection = new NpgsqlConnection(connectionString))
        {
          await connection.OpenAsync();
          LogMessage("PostgreSQL connection successful! ✓");
        }
      }
      catch (Exception ex)
      {
        LogMessage($"PostgreSQL connection failed: {ex.Message} ✗");
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
        WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
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
          WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
        }
      }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
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

    public class DbCredentials
    {
      public string Server { get; set; }
      public string Port { get; set; }
      public string Database { get; set; }
      public string Username { get; set; }
      public string Password { get; set; }
    }

    public class TableInfo
    {
      public string TableName { get; set; }
      public long RowCount { get; set; }
    }
  }
}
