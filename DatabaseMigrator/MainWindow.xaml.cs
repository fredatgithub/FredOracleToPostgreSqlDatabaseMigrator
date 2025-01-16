using DatabaseMigrator.Helpers;
using DatabaseMigrator.Models;
using DatabaseMigrator.Properties;
using DatabaseMigrator.Views;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace DatabaseMigrator
{
  public partial class MainWindow: Window
  {
    private readonly string _oracleCredentialsFileTemplate = "id_oracle-{profilName}.txt";
    private readonly string _pgCredentialsFileTemplate = "id_pg-{profilName}.txt";
    private string _oracleCredentialsFile = "id_oracle.txt";
    private string _pgCredentialsFile = "id_pg.txt";
    private readonly string _logFile = "log.txt";
    private readonly IOracleService _oracleService;
    private readonly IPostgresService _postgresService;
    private const float DefaultWindowTop = 0.00f;
    private const float DefaultWindowLeft = 0.00f;

    public MainWindow()
    {
      InitializeComponent();
      _oracleService = new OracleService();
      _postgresService = new PostgresService();
      LoadProfilCombobox();
      LoadLastProfilUsed();
      LoadCredentials();
      LoadLogs();

      // Wire up button click events
      btnTestOracle.Click += BtnTestOracle_Click;
      btnTestPostgres.Click += BtnTestPostgres_Click;
      btnLoadOracleTables.Click += BtnLoadOracleTables_Click;
      btnLoadPostgresTables.Click += BtnLoadPostgresTables_Click;
      btnLoadOracleStoredProcs.Click += BtnLoadOracleStoredProcs_Click;
      btnLoadPostgresStoredProcs.Click += BtnLoadPostgresStoredProcs_Click;

      // Wire up search text changed events
      txtOracleSearch.TextChanged += TxtOracleSearch_TextChanged;
      txtPostgresSearch.TextChanged += TxtPostgresSearch_TextChanged;

      // Wire up selection changed events
      lstOracleTables.SelectionChanged += LstOracleTables_SelectionChanged;
      lstPostgresTables.SelectionChanged += LstPostgresTables_SelectionChanged;
      lstOracleStoredProcs.SelectionChanged += LstOracleStoredProcs_SelectionChanged;
      lstPostgresStoredProcs.SelectionChanged += LstPostgresStoredProcs_SelectionChanged;
    }

    private void LoadLastProfilUsed()
    {
      var lastOracleProfil = Settings.Default.OracleSelectedProfil;
      var lastPostgresProfil = Settings.Default.PostgresqlSelectedProfil;
      if (!string.IsNullOrEmpty(lastOracleProfil))
      {
        cboOracleConnectionProfile.SelectedItem = lastOracleProfil;
        _oracleCredentialsFile = _oracleCredentialsFileTemplate.Replace("{profilName}", lastOracleProfil);
      }

      if (!string.IsNullOrEmpty(lastPostgresProfil))
      {
        cboPostgresqlConnectionProfile.SelectedItem = lastPostgresProfil;
        _pgCredentialsFile = _pgCredentialsFileTemplate.Replace("{profilName}", lastPostgresProfil);
      }
    }

    private void LoadProfilCombobox()
    {
      LoadProfilForDatabase("id_oracle-*.txt", cboOracleConnectionProfile);
      LoadProfilForDatabase("id_pg-*.txt", cboPostgresqlConnectionProfile);

      LoadProfileFiles("id_oracle-*.txt", cboOracleLConnectionProfileFile);
      LoadProfileFiles("id_pg-*.txt", cboPostgresConnectionProfileFile);
    }

    private void LoadProfileFiles(string pattern, ComboBox comboBox)
    {
      var profils = GetProfilFile(pattern);
      profils = GetProfilNameFromFilename(profils);
      comboBox.Items.Clear();
      foreach (var item in profils)
      {
        comboBox.Items.Add(item);
      }

      if (comboBox.Items.Count == 0)
      {
        comboBox.IsEnabled = false;
      }
    }

    private static void LoadProfilForDatabase(string databaseNamePattern, ComboBox comboBox)
    {
      var profils = GetProfilFile(databaseNamePattern);
      profils = GetProfilNameFromFilename(profils);
      comboBox.Items.Clear();
      foreach (var item in profils)
      {
        comboBox.Items.Add(item);
      }

      if (databaseNamePattern.ToLower().Contains("oracle"))
      {
        if (!comboBox.Items.Contains(Settings.Default.OracleProfil1))
        {
          profils.Add(Settings.Default.OracleProfil1);
        }

        if (!comboBox.Items.Contains(Settings.Default.OracleProfil2))
        {
          profils.Add(Settings.Default.OracleProfil2);
        }

        if (!comboBox.Items.Contains(Settings.Default.OracleProfil3))
        {
          profils.Add(Settings.Default.OracleProfil3);
        }

        if (!comboBox.Items.Contains(Settings.Default.OracleProfil4))
        {
          profils.Add(Settings.Default.OracleProfil4);
        }
      }
      else
      {
        if (!comboBox.Items.Contains(Settings.Default.PostgresqlProfil1))
        {
          profils.Add(Settings.Default.PostgresqlProfil1);
        }

        if (!comboBox.Items.Contains(Settings.Default.PostgresqlProfil2))
        {
          profils.Add(Settings.Default.PostgresqlProfil2);
        }

        if (!comboBox.Items.Contains(Settings.Default.PostgresqlProfil3))
        {
          profils.Add(Settings.Default.PostgresqlProfil3);
        }

        if (!comboBox.Items.Contains(Settings.Default.PostgresqlProfil4))
        {
          profils.Add(Settings.Default.PostgresqlProfil4);
        }
      }

      comboBox.Items.Clear();
      foreach (var profil in profils)
      {
        comboBox.Items.Add(profil);
      }
    }

    private static List<string> GetProfilFile(string pattern)
    {
      var result = GetAllFiles(pattern);
      return result;
    }

    private static List<string> GetProfilNameFromFilename(List<string> list)
    {
      var result = new List<string>();
      foreach (var item in list)
      {
        var profil = item.Split('-')[1].Split('.')[0];
        result.Add(profil);
      }

      return result;
    }

    private static List<string> GetAllFiles(string pattern)
    {
      var result = new List<string>();
      try
      {
        result = Directory.GetFiles(".", pattern, SearchOption.TopDirectoryOnly).ToList();
      }
      catch (Exception)
      {
        return result;
      }

      return result;
    }

    private void LstOracleTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateOracleSelectedCount();
    }

    private void LstPostgresTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdatePostgresSelectedCount();
    }

    private void LstOracleStoredProcs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdateOracleStoredProcsSelectedCount();
    }

    private void LstPostgresStoredProcs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      UpdatePostgresStoredProcsSelectedCount();
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
      if (lstOracleTables.ItemsSource is IEnumerable items)
      {
        var view = CollectionViewSource.GetDefaultView(items);
        var searchText = txtOracleSearch.Text.ToUpperInvariant();
        view.Filter = item => string.IsNullOrEmpty(searchText) || (item as TableInfo)?.TableName.ToUpperInvariant().Contains(searchText) == true;
      }
    }

    private void FilterPostgresTables()
    {
      if (lstPostgresTables.ItemsSource is IEnumerable items)
      {
        var view = CollectionViewSource.GetDefaultView(items);
        var searchText = txtPostgresSearch.Text.ToUpperInvariant();
        view.Filter = item => string.IsNullOrEmpty(searchText) || (item as TableInfo)?.TableName.ToUpperInvariant().Contains(searchText) == true;
      }
    }

#pragma warning disable EC84 // Avoid async void methods
    private async void BtnLoadOracleTables_Click(object sender, RoutedEventArgs e)
#pragma warning restore EC84 // Avoid async void methods
    {
      try
      {
        var loadingWindow = new LoadingWindow(this);
        loadingWindow.Show();

        var tables = new List<TableInfo>();
        using (var connection = new OracleConnection(GetOracleConnectionString()))
        {
          await connection.OpenAsync();

          using (var cmd = connection.CreateCommand())
          {
            // First, get table list
            cmd.CommandText = @"
              SELECT table_name 
              FROM all_tables 
              WHERE owner = :owner 
              ORDER BY table_name";

            cmd.Parameters.Add(new OracleParameter("owner", txtOracleUser.Text.ToUpper()));

            using (var reader = await cmd.ExecuteReaderAsync())
            {
              while (await reader.ReadAsync())
              {
                tables.Add(new TableInfo { TableName = reader.GetString(0), RowCount = 0 });
              }
            }

            // then count table lines for each table
            foreach (var table in tables)
            {
              cmd.CommandText = $"SELECT COUNT(*) FROM \"{txtOracleUser.Text.ToUpper()}\".\"{table.TableName}\"";
              table.RowCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }
          }

          lstOracleTables.ItemsSource = tables;
          LogMessage($"Successfully loaded {tables.Count} Oracle tables ");
          SetButtonSuccess(btnLoadOracleTables);

          CompareTables();
        }

        loadingWindow.Close();
      }
      catch (Exception exception)
      {
        LogMessage($"Failed to load Oracle tables: {exception.Message}");
        SetButtonError(btnLoadOracleTables);
      }
    }

#pragma warning disable EC84 // Avoid async void methods
    private async void BtnLoadPostgresTables_Click(object sender, RoutedEventArgs e)
#pragma warning restore EC84 // Avoid async void methods
    {
      try
      {
        var loadingWindow = new LoadingWindow(this);
        loadingWindow.Show();

        var tables = new List<TableInfo>();
        using (var connection = new NpgsqlConnection(GetPostgresConnectionString()))
        {
          await connection.OpenAsync();

          using (var cmd = connection.CreateCommand())
          {
            // First, get table list
            cmd.CommandText = @"
              SELECT tablename 
              FROM pg_catalog.pg_tables 
              WHERE schemaname = @schema 
              ORDER BY tablename";

            cmd.Parameters.AddWithValue("schema", txtPostgresSchema.Text);

            using (var reader = await cmd.ExecuteReaderAsync())
            {
              while (await reader.ReadAsync())
              {
                tables.Add(new TableInfo { TableName = reader.GetString(0), RowCount = 0 });
              }
            }

            // then count table lines for each table
            foreach (var table in tables)
            {
              cmd.CommandText = $"SELECT COUNT(*) FROM {txtPostgresSchema.Text}.\"{table.TableName}\"";
              table.RowCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }
          }

          lstPostgresTables.ItemsSource = tables;
          LogMessage($"Successfully loaded {tables.Count} PostgreSQL tables ");
          SetButtonSuccess(btnLoadPostgresTables);

          CompareTables();
        }

        loadingWindow.Close();
      }
      catch (Exception exception)
      {
        LogMessage($"Failed to load PostgreSQL tables: {exception.Message}");
        SetButtonError(btnLoadPostgresTables);
      }
    }

    private void CompareTables()
    {
      if (lstOracleTables.ItemsSource == null || lstPostgresTables.ItemsSource == null)
        return;

      var oracleTables = lstOracleTables.ItemsSource.Cast<TableInfo>();
      var postgresTables = lstPostgresTables.ItemsSource.Cast<TableInfo>();

      // Init colors
      foreach (var table in oracleTables.Concat(postgresTables))
      {
        table.Background = null;
      }

      // Compare tables
      foreach (var oracleTable in oracleTables)
      {
        var postgresTable = postgresTables.FirstOrDefault(t => t.TableName.Equals(oracleTable.TableName, StringComparison.OrdinalIgnoreCase));
        if (postgresTable != null)
        {
          var color = oracleTable.RowCount == postgresTable.RowCount
            ? new SolidColorBrush(Colors.LightGreen)
            : new SolidColorBrush(Colors.LightPink);

          oracleTable.Background = color;
          postgresTable.Background = color;
        }
      }
    }

    private void LoadCredentials()
    {
      try
      {
        // Load Oracle ID
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

        // Load PostgreSQL ID
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
      catch (Exception exception)
      {
        MessageBox.Show($"Error loading credentials: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void SaveCredentials()
    {
      try
      {
        // Save Oracle ID
        if (chkSaveOracle.IsChecked == true && cboOracleConnectionProfile.SelectedIndex != -1)
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
          File.WriteAllText(GetSelectedProfilforOracle(cboOracleConnectionProfile.SelectedValue.ToString()), encryptedOracle);
          Settings.Default.OracleSelectedProfil = cboOracleConnectionProfile.SelectedValue.ToString();
          Settings.Default.Save();
        }

        // Save PostgreSQL ID
        if (chkSavePostgres.IsChecked == true && cboPostgresqlConnectionProfile.SelectedIndex != -1)
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
          File.WriteAllText(GetSelectedProfilforPostgresql(cboPostgresqlConnectionProfile.SelectedValue.ToString()), encryptedPg);
          Settings.Default.PostgresqlSelectedProfil = cboPostgresqlConnectionProfile.SelectedValue.ToString();
          Settings.Default.Save();
        }
      }
      catch (Exception exception)
      {
        MessageBox.Show($"Error saving credentials: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private string GetSelectedProfilforPostgresql(string profilName)
    {
      _pgCredentialsFile = _pgCredentialsFileTemplate.Replace("{profilName}", profilName);
      return _pgCredentialsFile;
    }

    private string GetSelectedProfilforOracle(string profilName)
    {
      _oracleCredentialsFile = _oracleCredentialsFileTemplate.Replace("{profilName}", profilName);
      return _oracleCredentialsFile;
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
      catch (Exception exception)
      {
        MessageBox.Show($"Error loading logs: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void SaveLogs()
    {
      try
      {
        File.WriteAllText(_logFile, txtLogs.Text);
      }
      catch (Exception exception)
      {
        MessageBox.Show($"Error saving logs: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void LogMessage(string message)
    {
      string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
      Dispatcher.Invoke(() =>
      {
        txtLogs.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
        txtLogs.ScrollToEnd();
      });
    }

    private static void ResetButtonColor(Button button)
    {
      button.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
    }

    private static void SetButtonSuccess(Button button)
    {
      button.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69));
    }

    private static void SetButtonError(Button button)
    {
      button.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
    }

#pragma warning disable EC84 // Avoid async void methods
    private async void BtnTestOracle_Click(object sender, RoutedEventArgs e)
#pragma warning restore EC84 // Avoid async void methods
    {
      try
      {
        btnTestOracle.IsEnabled = false;
        ResetButtonColor(btnTestOracle);
        LogMessage("Testing Oracle connection...");

        using (var connection = new OracleConnection(GetOracleConnectionString()))
        {
          await connection.OpenAsync();
          LogMessage("Oracle connection successful! ");
          SetButtonSuccess(btnTestOracle);
        }
      }
      catch (Exception exception)
      {
        LogMessage($"Oracle connection failed: {exception.Message} ");
        SetButtonError(btnTestOracle);
      }
      finally
      {
        btnTestOracle.IsEnabled = true;
      }
    }

#pragma warning disable EC84 // Avoid async void methods
    private async void BtnTestPostgres_Click(object sender, RoutedEventArgs e)
#pragma warning restore EC84 // Avoid async void methods
    {
      try
      {
        btnTestPostgres.IsEnabled = false;
        ResetButtonColor(btnTestPostgres);
        LogMessage("Testing PostgreSQL connection...");

        using (var connection = new NpgsqlConnection(GetPostgresConnectionString()))
        {
          await connection.OpenAsync();
          LogMessage("PostgreSQL connection successful! ");
          SetButtonSuccess(btnTestPostgres);
        }
      }
      catch (Exception exception)
      {
        LogMessage($"PostgreSQL connection failed: {exception.Message} ");
        SetButtonError(btnTestPostgres);
      }
      finally
      {
        btnTestPostgres.IsEnabled = true;
      }
    }

    private string GetOracleConnectionString()
    {
      string oracleServer = string.Empty;
      string oraclePort = string.Empty;
      string oracleServiceName = string.Empty;
      string oracleUser = string.Empty;
      string oraclePassword = string.Empty;

      Application.Current.Dispatcher.Invoke(() =>
      {
        oracleServer = txtOracleServer.Text;
        oraclePort = txtOraclePort.Text;
        oracleServiceName = txtOracleServiceName.Text;
        oracleUser = txtOracleUser.Text;
        oraclePassword = pwdOraclePassword.Password;
      });

      return $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={oracleServer})(PORT={oraclePort}))(CONNECT_DATA=(SERVICE_NAME={oracleServiceName})));User Id={oracleUser};Password={oraclePassword};";
    }

    private string GetPostgresConnectionString()
    {
      string server = string.Empty;
      string port = string.Empty;
      string database = string.Empty;
      string user = string.Empty;
      string password = string.Empty;

      Application.Current.Dispatcher.Invoke(() =>
      {
        server = txtPostgresServer.Text;
        port = txtPostgresPort.Text;
        database = txtPostgresDatabase.Text;
        user = txtPostgresUser.Text;
        password = pwdPostgresPassword.Password;
      });

      return $"Host={server};Port={port};Database={database};Username={user};Password={password}";

    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      // Restore window position and size
      var settings = Settings.Default;

      // If it's the first time the application is launched, center the window
      if (settings.WindowTop == DefaultWindowTop && settings.WindowLeft == DefaultWindowLeft)
      {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
      }
      else
      {
        // Check if the window will be visible on the screen
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
      // Save window position and size
      var settings = Settings.Default;

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

      // Save ID
      SaveCredentials();

      // Save logs
      SaveLogs();
    }

#pragma warning disable EC84 // Avoid async void methods
    private async void BtnMigrateTables_Click(object sender, RoutedEventArgs e)
#pragma warning restore EC84 // Avoid async void methods
    {
      try
      {
        var selectedOracleTables = lstOracleTables.ItemsSource.Cast<TableInfo>().Where(t => t.IsSelected).ToList();
        if (selectedOracleTables.Count == 0)
        {
          MessageBox.Show("Please select at least one table from the source database to migrate.", "No tables selected", MessageBoxButton.OK, MessageBoxImage.Hand);
          return;
        }

        var loadingWindow = new LoadingWindow(this) { Title = "Copying Data ..." };
        loadingWindow.Show();

        try
        {
          foreach (var table in selectedOracleTables)
          {
            try
            {
              loadingWindow.lblStatus.Content = $"Copying table {table.TableName}...";
              await Task.Run(() => MigrateTable(table));
              LogMessage($"Successfully copied table {table.TableName}");
            }
            catch (Exception exception)
            {
              LogMessage($"Error copying table {table.TableName}: {exception.Message}");
              await Dispatcher.InvokeAsync(() =>
                MessageBox.Show($"Error copying table {table.TableName}: {exception.Message}",
                  "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
          }

          await Dispatcher.InvokeAsync(() =>
            MessageBox.Show("Copy completed. Check the log for details.",
              "Copy Complete", MessageBoxButton.OK, MessageBoxImage.Information));
        }
        finally
        {
          loadingWindow.Close();
        }
      }
      catch (Exception exception)
      {
        LogMessage($"Copy error: {exception.Message}");
        MessageBox.Show($"Copy error: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void MigrateTable(TableInfo targetTable)
    {
      LoadingWindow loadingWindow = null;
      using (var oracleConnection = new OracleConnection(GetOracleConnectionString()))
      using (var postgresConnection = new NpgsqlConnection(GetPostgresConnectionString()))
      {
        try
        {
          oracleConnection.Open();
          postgresConnection.Open();

          // Truncate target table and drop foreign key constraints
          using (var cmd = new NpgsqlCommand())
          {
            cmd.Connection = postgresConnection;
            string schemaTable = string.Empty;

            // Récupérer les valeurs UI de manière thread-safe
            Application.Current.Dispatcher.Invoke(() =>
            {
              schemaTable = $"{txtPostgresSchema.Text}.{targetTable.TableName}";
            });

            // Get all foreign key constraints for this table
            cmd.CommandText = @"
              SELECT 
                  tc.constraint_name,
                  ccu.table_schema as foreign_table_schema,
                  ccu.table_name as foreign_table_name
              FROM information_schema.table_constraints tc
              JOIN information_schema.constraint_column_usage ccu 
                  ON tc.constraint_name = ccu.constraint_name
              WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = @schema
              AND tc.table_name = @tablename";

            string schema = string.Empty;
            Application.Current.Dispatcher.Invoke(() =>
            {
              schema = txtPostgresSchema.Text;
            });

            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("tablename", targetTable.TableName);

            var constraints = new List<(string name, string schema, string table)>();
            using (var reader = cmd.ExecuteReader())
            {
              while (reader.Read())
              {
                constraints.Add((
                  reader.GetString(0),
                  reader.GetString(1),
                  reader.GetString(2)
                ));
              }
            }
            cmd.Parameters.Clear();

            // Drop each foreign key constraint
            foreach (var constraint in constraints)
            {
              cmd.CommandText = $"ALTER TABLE {schemaTable} DROP CONSTRAINT {constraint.name}";
#pragma warning disable EC72 // Don't execute SQL commands in loops
              cmd.ExecuteNonQuery();
#pragma warning restore EC72 // Don't execute SQL commands in loops
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
              LogMessage($"Dropped {constraints.Count} foreign key constraints for table {schemaTable}");
            });

            // Disable user triggers
            cmd.CommandText = $"ALTER TABLE {schemaTable} DISABLE TRIGGER USER";
            cmd.ExecuteNonQuery();
            Application.Current.Dispatcher.Invoke(() =>
            {
              LogMessage($"Disabled user triggers for table {schemaTable}");
            });

            // Truncate the table
            cmd.CommandText = $"TRUNCATE TABLE {schemaTable} RESTART IDENTITY CASCADE";
            cmd.ExecuteNonQuery();
            Application.Current.Dispatcher.Invoke(() =>
            {
              LogMessage($"Table {schemaTable} truncated successfully");
            });

            try
            {
              // Get data from Oracle
              var selectCommand = $"SELECT * FROM {targetTable.TableName}";
              using (var oracleCmd = new OracleCommand(selectCommand, oracleConnection))
              using (var reader = oracleCmd.ExecuteReader())
              {
                // Get column names and types
                var schemaInfo = reader.GetSchemaTable();
                var columns = new List<string>();
                var columnTypes = new List<Type>();
                foreach (DataRow row in schemaInfo.Rows)
                {
                  columns.Add(row["ColumnName"].ToString());
                  columnTypes.Add((Type)row["DataType"]);
                }

                // Prepare insert statement
                var columnList = string.Join(",", columns);
                var paramList = string.Join(",", columns.Select(c => $"@{c}"));
                var insertCommand = $"INSERT INTO {schemaTable} ({columnList}) VALUES ({paramList})";

                // Batch insert data
                using (var transaction = postgresConnection.BeginTransaction())
                {
                  try
                  {
                    loadingWindow = new LoadingWindow(this) { Title = "Copying Data ..." };
                    loadingWindow.Show();
                    using (var insertCmd = new NpgsqlCommand(insertCommand, postgresConnection, transaction))
                    {
                      // Add parameters with correct types
                      for (int i = 0; i < columns.Count; i++)
                      {
                        var npgsqlType = GetNpgsqlType(columnTypes[i]);
                        insertCmd.Parameters.Add(new NpgsqlParameter($"@{columns[i]}", npgsqlType));
                      }

                      int rowCount = 0;
                      while (reader.Read())
                      {
                        for (int i = 0; i < columns.Count; i++)
                        {
                          var value = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                          insertCmd.Parameters[i].Value = value;
                        }

#pragma warning disable EC72 // Don't execute SQL commands in loops
                        insertCmd.ExecuteNonQuery();
#pragma warning restore EC72 // Don't execute SQL commands in loops
                        rowCount++;

                        if (rowCount % 1000 == 0)
                        {
                          Application.Current.Dispatcher.Invoke(() =>
                          {
                            LogMessage($"Inserted {rowCount} rows into {schemaTable}");
                          });
                        }
                      }

                      transaction.Commit();
                      Application.Current.Dispatcher.Invoke(() =>
                      {
                        LogMessage($"Successfully migrated {rowCount} rows from {targetTable.TableName} to PostgreSQL");
                      });
                    }
                  }
                  catch (Exception exception)
                  {
                    transaction.Rollback();
                    loadingWindow?.Close();
                    throw new Exception($"Error while inserting data into {schemaTable}: {exception.Message}");
                  }
                }
              }
            }
            finally
            {
              // Re-enable user triggers
              cmd.CommandText = $"ALTER TABLE {schemaTable} ENABLE TRIGGER USER";
              cmd.ExecuteNonQuery();

              // Recreate each foreign key constraint
              foreach (var constraint in constraints)
              {
                try
                {
                  // Get the constraint definition
                  cmd.CommandText = $@"
                    SELECT pg_get_constraintdef(oid) 
                    FROM pg_constraint 
                    WHERE conname = @constraintname";
                  cmd.Parameters.AddWithValue("constraintname", constraint.name);
                  string constraintDef = "";
#pragma warning disable EC72 // Don't execute SQL commands in loops
                  using (var reader = cmd.ExecuteReader())
                  {
                    if (reader.Read())
                    {
                      constraintDef = reader.GetString(0);
                    }
                  }
#pragma warning restore EC72 // Don't execute SQL commands in loops
                  cmd.Parameters.Clear();

                  if (!string.IsNullOrEmpty(constraintDef))
                  {
                    // Recreate the constraint
                    cmd.CommandText = $"ALTER TABLE {schemaTable} ADD CONSTRAINT {constraint.name} {constraintDef}";
#pragma warning disable EC72 // Don't execute SQL commands in loops
                    cmd.ExecuteNonQuery();
#pragma warning restore EC72 // Don't execute SQL commands in loops
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                      LogMessage($"Recreated constraint {constraint.name}");
                    });
                  }
                }
                catch (Exception exception)
                {
                  Application.Current.Dispatcher.Invoke(() =>
                  {
                    LogMessage($"Warning: Could not recreate constraint {constraint.name}: {exception.Message}");
                  });
                  loadingWindow?.Close();
                }
              }

              Application.Current.Dispatcher.Invoke(() =>
              {
                LogMessage($"Finished recreating constraints for table {schemaTable}");
              });
              loadingWindow?.Close();
            }
          }
        }
        catch (Exception exception)
        {
          Application.Current.Dispatcher.Invoke(() =>
          {
            LogMessage($"Error migrating table {targetTable.TableName}: {exception.Message}");
          });
          loadingWindow?.Close();
          MessageBox.Show($"Error migrating table {targetTable.TableName}: {exception.Message}", "Migration Error", MessageBoxButton.OK, MessageBoxImage.Error);
          return;
        }
      }

      loadingWindow?.Close();
      MessageBox.Show("Migration completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private NpgsqlDbType GetNpgsqlType(Type type)
    {
      if (DatabaseHelper.GetNpgsqlType(type) == NpgsqlDbType.Unknown)
      {
        // To debug, let's display the not supported type
        Application.Current.Dispatcher.Invoke(() =>
        {
          LogMessage($"not supported type detected: {type.FullName}");
        });

        throw new ArgumentException($"Not supported type: {type.FullName}", nameof(type));
      }

      return DatabaseHelper.GetNpgsqlType(type);
    }

    public void UpdateOracleSelectedCount()
    {
      if (lstOracleTables.ItemsSource is IEnumerable<TableInfo> tables)
      {
        var selectedCount = tables.Count(t => t.IsSelected);
        txtOracleSelectedCount.Text = selectedCount.ToString();
        txtOracleTableLabel.Text = $" table{StringHelper.Plural(selectedCount)}";
      }
    }

    public void UpdatePostgresSelectedCount()
    {
      if (lstPostgresTables.ItemsSource is IEnumerable<TableInfo> tables)
      {
        var selectedCount = tables.Count(t => t.IsSelected);
        txtPostgresSelectedCount.Text = selectedCount.ToString();
        txtPostgresTableLabel.Text = $" table{StringHelper.Plural(selectedCount)}";
      }
    }

    private void UpdateOracleStoredProcsSelectedCount()
    {
      if (lstOracleStoredProcs.ItemsSource is IEnumerable<StoredProcedureItem> procedures)
      {
        var selectedCount = procedures.Count(p => p.IsSelected);
        txtOracleSelectedProcsCount.Text = selectedCount.ToString();
        txtOracleProcsLabel.Text = $" stored procedure{StringHelper.Plural(selectedCount)}";
      }
    }

    private void UpdatePostgresStoredProcsSelectedCount()
    {
      if (lstPostgresStoredProcs.ItemsSource is IEnumerable<StoredProcedureItem> procedures)
      {
        var selectedCount = procedures.Count(p => p.IsSelected);
        txtPostgresSelectedProcsCount.Text = selectedCount.ToString();
        txtPostgresProcsLabel.Text = $" stored procedure{StringHelper.Plural(selectedCount)}";
      }
    }

#pragma warning disable EC84 // Avoid async void methods
    private async void BtnLoadOracleStoredProcs_Click(object sender, RoutedEventArgs e)
#pragma warning restore EC84 // Avoid async void methods
    {
      try
      {
        btnLoadOracleStoredProcs.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;

        var procedures = await Task.Run(() =>
        {
          return _oracleService.GetStoredProcedures();
        });

        lstOracleStoredProcs.ItemsSource = procedures.Select(p => new StoredProcedureItem
        {
          ProcedureName = p,
          IsSelected = false
        }).OrderBy(p => p.ProcedureName).ToList();

        LogMessage($"Loaded {procedures.Count} Oracle stored procedures.");
      }
      catch (Exception exception)
      {
        MessageBox.Show($"Error loading Oracle stored procedures: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        LogMessage($"Error loading Oracle stored procedures: {exception.Message}");
      }
      finally
      {
        btnLoadOracleStoredProcs.IsEnabled = true;
        Mouse.OverrideCursor = null;
      }
    }

#pragma warning disable EC84 // Avoid async void methods
    private async void BtnLoadPostgresStoredProcs_Click(object sender, RoutedEventArgs e)
#pragma warning restore EC84 // Avoid async void methods
    {
      try
      {
        btnLoadPostgresStoredProcs.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;

        var procedures = await Task.Run(() =>
        {
          return _postgresService.GetStoredProcedures();
        });

        lstPostgresStoredProcs.ItemsSource = procedures.Select(p => new StoredProcedureItem
        {
          ProcedureName = p,
          IsSelected = false
        }).OrderBy(p => p.ProcedureName).ToList();

        LogMessage($"Loaded {procedures.Count} PostgreSQL stored procedures.");
      }
      catch (Exception exception)
      {
        MessageBox.Show($"Error loading PostgreSQL stored procedures: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        LogMessage($"Error loading PostgreSQL stored procedures: {exception.Message}");
      }
      finally
      {
        btnLoadPostgresStoredProcs.IsEnabled = true;
        Mouse.OverrideCursor = null;
      }
    }

    private void ChkSavePostgres_Checked(object sender, RoutedEventArgs e)
    {
      if (chkSavePostgres.IsChecked == true && cboPostgresqlConnectionProfile.SelectedIndex == -1)
      {
        MessageBox.Show("You have to select a profile name for the PostgreSQL connection", "No profile choosen", MessageBoxButton.OK, MessageBoxImage.Hand);
        chkSavePostgres.IsChecked = false;
      }
    }

    private void ChkSaveOracle_Checked(object sender, RoutedEventArgs e)
    {
      if (chkSaveOracle.IsChecked == true && cboOracleConnectionProfile.SelectedIndex == -1)
      {
        MessageBox.Show("You have to select a profile name for the Oracle connection", "No profile choosen", MessageBoxButton.OK, MessageBoxImage.Hand);
        chkSaveOracle.IsChecked = false;
      }
    }

    private void BtnLoadOracleConnection_Click(object sender, RoutedEventArgs e)
    {
      if (cboOracleLConnectionProfileFile.SelectedIndex == -1)
      {
        MessageBox.Show("You have to select a profile name for the Oracle connection", "No profil choosen", MessageBoxButton.OK, MessageBoxImage.Hand);
        return;
      }

      var profileName = cboOracleLConnectionProfileFile.SelectedValue.ToString();
      profileName = ChangeProfileNameToProfileFilenameForOracle(profileName);
      var encryptedOracle = File.ReadAllText(profileName);
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

    private void BtnLoadPostgresqlConnection_Click(object sender, RoutedEventArgs e)
    {
      if (cboPostgresqlConnectionProfile.SelectedIndex == -1)
      {
        MessageBox.Show("You have to select a profile name for the PostgreSQL connection", "No profile choosen", MessageBoxButton.OK, MessageBoxImage.Hand);
        return;
      }

      var profileName = cboPostgresConnectionProfileFile.SelectedValue.ToString();
      profileName = ChangeProfileNameToProfileFilenameForPostgresql(profileName);
      var encryptedPg = File.ReadAllText(profileName);
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
      }
    }

    private string ChangeProfileNameToProfileFilenameForOracle(string profileName)
    {
      return _oracleCredentialsFileTemplate.Replace("{profilName}", profileName);
    }

    private string ChangeProfileNameToProfileFilenameForPostgresql(string profileName)
    {
      return _pgCredentialsFileTemplate.Replace("{profilName}", profileName);
    }
  }
}
