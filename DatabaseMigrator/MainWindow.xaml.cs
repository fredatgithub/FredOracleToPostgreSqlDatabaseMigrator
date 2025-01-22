using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DatabaseMigrator.Helpers;
using DatabaseMigrator.Models;
using DatabaseMigrator.Properties;
using DatabaseMigrator.Views;
using Newtonsoft.Json;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace DatabaseMigrator
{
  public partial class MainWindow: Window
  {
    private readonly string _oracleCredentialsFileTemplate = "id_oracle-{profilName}.txt";
    private readonly string _pgCredentialsFileTemplate = "id_pg-{profilName}.txt";
    private string _oracleCredentialsFile = "id_oracle.txt";
    private string _pgCredentialsFile = "id_pg.txt";
    private readonly string _logFile = "log.txt";
    private IOracleService _oracleService;
    private IPostgresService _postgresService;
    private const float DefaultWindowTop = 0.00f;
    private const float DefaultWindowLeft = 0.00f;

    public MainWindow()
    {
      InitializeComponent();

      // Wire up button click events
      btnTestOracle.Click += BtnTestOracle_Click;
      btnTestPostgres.Click += BtnTestPostgres_Click;
      btnLoadOracleTables.Click += BtnLoadOracleTables_Click;
      btnLoadPostgresTables.Click += BtnLoadPostgresTables_Click;
      btnLoadOracleStoredProcs.Click += BtnLoadOracleStoredProcs_Click;
      btnLoadPostgresStoredProcs.Click += BtnLoadPostgresStoredProcs_Click;
      btnMigrateStoredProcs.Click += BtnMigrateStoredProcs_Click;

      // Wire up search text changed events
      txtOracleSearch.TextChanged += TxtOracleSearch_TextChanged;
      txtPostgresSearch.TextChanged += TxtPostgresSearch_TextChanged;
      txtOracleStoredProcsSearch.TextChanged += TxtOracleStoredProcsSearch_TextChanged;
      txtPostgresStoredProcsSearch.TextChanged += TxtPostgresStoredProcsSearch_TextChanged;

      // Wire up selection changed events
      lstOracleTables.SelectionChanged += LstOracleTables_SelectionChanged;
      lstPostgresTables.SelectionChanged += LstPostgresTables_SelectionChanged;
      lstOracleStoredProcs.SelectionChanged += LstOracleStoredProcs_SelectionChanged;
      lstPostgresStoredProcs.SelectionChanged += LstPostgresStoredProcs_SelectionChanged;

      LoadProfilCombobox();
      LoadLastProfilUsed();
      LoadCredentials();
      LoadLogs();

      // Initialize services after credentials are loaded
      InitializeServices();
    }

    private async void InitializeServices()
    {
      try
      {
        var oracleConnectionString = GetOracleConnectionString();
        var postgresConnectionString = GetPostgresConnectionString();
        var postgresSchema = txtPostgresSchema.Text?.ToLower();

        // Vérifier que les chaînes de connexion sont valides
        if (string.IsNullOrEmpty(oracleConnectionString))
        {
          throw new ArgumentException("Oracle connection string is empty. Please configure Oracle connection settings.");
        }

        if (string.IsNullOrEmpty(postgresConnectionString))
        {
          throw new ArgumentException("PostgreSQL connection string is empty. Please configure PostgreSQL connection settings.");
        }

        if (string.IsNullOrEmpty(postgresSchema))
        {
          throw new ArgumentException("PostgreSQL schema is empty. Please enter a schema name.");
        }

        _oracleService = new OracleService(oracleConnectionString);
        _postgresService = new PostgresService(postgresConnectionString, postgresSchema);
      }
      catch (Exception exception)
      {
        MessageBox.Show($"Error initializing database services: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        LogMessage($"Error initializing database services: {exception.Message}");
      }
    }

    private string GetPostgresConnectionString()
    {
      try
      {
        var server = string.Empty;
        var port = string.Empty;
        var database = string.Empty;
        Dispatcher.Invoke(() =>
        {
          server = txtPostgresServer.Text;
          port = txtPostgresPort.Text;
          database = txtPostgresDatabase.Text;
        });

        var builder = new NpgsqlConnectionStringBuilder();
        var credentials = LoadCredentials(_pgCredentialsFile);

        if (credentials == null || string.IsNullOrEmpty(credentials.Username) || string.IsNullOrEmpty(credentials.Password))
        {
          throw new ArgumentException("PostgreSQL credentials are not configured.");
        }

        builder.Host = server;
        builder.Port = int.Parse(port);
        builder.Database = database;
        builder.Username = credentials.Username;
        builder.Password = credentials.Password;

        // Vérifier les champs obligatoires
        if (string.IsNullOrEmpty(server))
        {
          throw new ArgumentException("PostgreSQL host is required.");
        }

        if (string.IsNullOrEmpty(database))
        {
          throw new ArgumentException("PostgreSQL database is required.");
        }

        if (!int.TryParse(port, out int portNumber) || portNumber <= 0)
        {
          throw new ArgumentException("Invalid PostgreSQL port number.");
        }

        return builder.ConnectionString;
      }
      catch (Exception exception)
      {
        throw new ArgumentException($"Error building PostgreSQL connection string: {exception.Message}");
      }
    }

    private string GetOracleConnectionString()
    {
      try
      {
        var server = string.Empty;
        var port = string.Empty;
        var serviceName = string.Empty;
        Dispatcher.Invoke(() =>
        {
          server = txtOracleServer.Text;
          port = txtOraclePort.Text;
          serviceName = txtOracleServiceName.Text;
        });

        var builder = new OracleConnectionStringBuilder();
        var credentials = LoadCredentials(_oracleCredentialsFile);

        if (credentials == null || string.IsNullOrEmpty(credentials.Username) || string.IsNullOrEmpty(credentials.Password))
        {
          throw new ArgumentException("Oracle credentials are not configured.");
        }

        // Format : "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XE)));User Id=system;Password=toto"
        builder.DataSource = $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={server})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={serviceName})))";
        builder.UserID = credentials.Username;
        builder.Password = credentials.Password;

        // Verify compulsory fields
        if (string.IsNullOrEmpty(server))
        {
          throw new ArgumentException("Oracle host is required.");
        }

        if (string.IsNullOrEmpty(serviceName))
        {
          throw new ArgumentException("Oracle service name is required.");
        }

        if (!int.TryParse(port, out int portNumber) || portNumber <= 0)
        {
          throw new ArgumentException("Invalid Oracle port number.");
        }

        return builder.ConnectionString;
      }
      catch (Exception exception)
      {
        throw new ArgumentException($"Error building Oracle connection string: {exception.Message}");
      }
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

    private static void LoadProfileFiles(string pattern, ComboBox comboBox)
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
            cmd.CommandTimeout = 0;
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
            cmd.CommandTimeout = 0;
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
      {
        return;
      }

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

    private static DbCredentials LoadCredentials(string filename)
    {
      try
      {
        if (File.Exists(filename))
        {
          var encrypted = File.ReadAllText(filename);
          var decrypted = EncryptionHelper.Decrypt(encrypted);
          if (!string.IsNullOrEmpty(decrypted))
          {
            return JsonConvert.DeserializeObject<DbCredentials>(decrypted);
          }
        }
        return null;
      }
      catch (Exception exception)
      {
        MessageBox.Show($"Error loading credentials from {filename}: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        return null;
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
        SaveLogs();
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
        List<TableInfo> selectedOracleTables = null;
        await Dispatcher.InvokeAsync(() =>
        {
          selectedOracleTables = lstOracleTables.ItemsSource.Cast<TableInfo>().Where(t => t.IsSelected).ToList();
        });

        if (selectedOracleTables.Count == 0)
        {
          MessageBox.Show("Please select at least one table from the source database to migrate.", "No tables selected", MessageBoxButton.OK, MessageBoxImage.Hand);
          return;
        }

        // Réorganiser les tables en fonction des dépendances
        var orderedTables = await Task.Run(() => GetTablesInDependencyOrder(selectedOracleTables));

        // Afficher l'ordre de migration prévu
        var migrationOrder = string.Join("\n", orderedTables.Select((t, i) => $"{i + 1}. {t.TableName}"));
        var result = MessageBox.Show(
            $"Les tables seront migrées dans l'ordre suivant :\n\n{migrationOrder}\n\nVoulez-vous continuer ?",
            "Ordre de migration",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information
        );

        if (result != MessageBoxResult.Yes)
        {
          return;
        }

        var loadingWindow = new LoadingWindow(this) { Title = "Copying Data ..." };
        loadingWindow.Show();

        try
        {
          foreach (var table in orderedTables)
          {
            try
            {
              await Dispatcher.InvokeAsync(() =>
              {
                loadingWindow.lblStatus.Content = $"Copying table {table.TableName}...";
              });

              await Dispatcher.InvokeAsync(() => MigrateTable(table));
              LogMessage($"Successfully copied table {table.TableName}");
            }
            catch (Exception exception)
            {
              LogMessage($"Error copying table {table.TableName}: {exception.Message}");
              await Dispatcher.InvokeAsync(() =>
              {
                MessageBox.Show($"Error copying table {table.TableName}: {exception.Message}",
                              "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
              });
            }
          }

          await Dispatcher.InvokeAsync(() =>
          {
            MessageBox.Show("Copy completed. Check the log for details.",
                      "Copy Complete", MessageBoxButton.OK, MessageBoxImage.Information);
          });
        }
        finally
        {
          await Dispatcher.InvokeAsync(() =>
          {
            loadingWindow.Close();
          });
        }
      }
      catch (Exception exception)
      {
        LogMessage($"Copy error: {exception.Message}");
        await Dispatcher.InvokeAsync(() =>
        {
          MessageBox.Show($"Copy error: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        });
      }
    }

    private List<TableInfo> GetTablesInDependencyOrder(List<TableInfo> tables)
    {
      var orderedTables = new List<TableInfo>();
      var dependencies = new Dictionary<string, HashSet<string>>();
      var processedTables = new HashSet<string>();
      string schema = string.Empty;

      // Get schema in a thread-safe manner
      Application.Current.Dispatcher.Invoke(() =>
      {
        schema = txtPostgresSchema.Text;
      });

      OracleConnection sourceConnection = null;
      NpgsqlConnection targetConnection = null;

      try
      {
        sourceConnection = new OracleConnection(GetOracleConnectionString());
        targetConnection = new NpgsqlConnection(GetPostgresConnectionString());

        sourceConnection.Open();
        targetConnection.Open();

        // Get dependencies for each table
        foreach (var table in tables)
        {
          using (var cmd = targetConnection.CreateCommand())
          {
            cmd.CommandTimeout = 0;
            // First, get table list
            cmd.CommandText = @"
                WITH RECURSIVE fk_tree AS (
                    -- Requête de base : obtenir toutes les dépendances directes
                    SELECT DISTINCT
                        tc.table_name as dependent_table,
                        ccu.table_name as referenced_table,
                        tc.constraint_name,
                        1 as level
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.constraint_column_usage ccu 
                        ON tc.constraint_name = ccu.constraint_name
                        AND ccu.table_schema = tc.table_schema
                    WHERE tc.constraint_type = 'FOREIGN KEY'
                        AND tc.table_schema = @schema
                        AND tc.table_name = @tablename

                    UNION ALL

                    -- Partie récursive : obtenir les dépendances indirectes
                    SELECT DISTINCT
                        t.dependent_table,
                        ccu.table_name,
                        tc.constraint_name,
                        t.level + 1
                    FROM fk_tree t
                    JOIN information_schema.table_constraints tc
                        ON tc.table_name = t.referenced_table
                        AND tc.constraint_type = 'FOREIGN KEY'
                        AND tc.table_schema = @schema
                    JOIN information_schema.constraint_column_usage ccu 
                        ON tc.constraint_name = ccu.constraint_name
                        AND ccu.table_schema = tc.table_schema
                )
                SELECT DISTINCT referenced_table, level
                FROM fk_tree
                ORDER BY level DESC";

            cmd.Parameters.AddWithValue("tablename", table.TableName.ToLower());
            cmd.Parameters.AddWithValue("schema", schema);

            dependencies[table.TableName] = new HashSet<string>();
            using (var reader = cmd.ExecuteReader())
            {
              while (reader.Read())
              {
                var referencedTable = reader.GetString(0).ToUpper();
                dependencies[table.TableName].Add(referencedTable);
                LogMessage($"Found dependency: {table.TableName} -> {referencedTable} (level {reader.GetInt32(1)})");
              }
            }
          }
        }
      }
      catch (Exception exception)
      {
        LogMessage($"Error getting dependencies: {exception.Message}");
        throw;
      }
      finally
      {
        sourceConnection?.Dispose();
        targetConnection?.Dispose();
      }

      // Fonction récursive pour ajouter les tables dans le bon ordre
      void ProcessTable(string tableName, HashSet<string> processingStack = null)
      {
        if (processedTables.Contains(tableName))
        {
          return;
        }

        processingStack = processingStack ?? new HashSet<string>();

        // Detect circular dependencies
        if (processingStack.Contains(tableName))
        {
          LogMessage($"Warning: Circular dependency detected for table {tableName}");
          return;
        }

        processingStack.Add(tableName);

        if (dependencies.ContainsKey(tableName))
        {
          foreach (var dep in dependencies[tableName])
          {
            if (tables.Any(t => t.TableName.Equals(dep, StringComparison.OrdinalIgnoreCase)))
            {
              ProcessTable(dep, processingStack);
            }
            else
            {
              // Si une table dépendante n'est pas sélectionnée, afficher un avertissement
              LogMessage($"Warning: Table {tableName} depends on {dep} but {dep} is not selected for migration");
            }
          }
        }

        processingStack.Remove(tableName);
        processedTables.Add(tableName);

        var tableInfo = tables.First(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
        if (!orderedTables.Contains(tableInfo))
        {
          orderedTables.Add(tableInfo);
          LogMessage($"Added {tableInfo.TableName} to migration queue");
        }
      }

      // Add all the tables in order
      foreach (var table in tables)
      {
        ProcessTable(table.TableName);
      }

      // Display the final migration order
      var migrationOrder = string.Join(" -> ", orderedTables.Select(t => t.TableName));
      LogMessage($"Migration order: {migrationOrder}");

      return orderedTables;
    }

    private void MigrateTable(TableInfo targetTable)
    {
      LoadingWindow loadingWindow = null;
      string schema = txtPostgresSchema.Text;
      OracleConnection sourceConnection = null;
      NpgsqlConnection targetConnection = null;

      try
      {
        sourceConnection = new OracleConnection(GetOracleConnectionString());
        targetConnection = new NpgsqlConnection(GetPostgresConnectionString());

        sourceConnection.Open();
        targetConnection.Open();

        // Désactiver temporairement les contraintes de clé étrangère
        using (var disableCmd = new NpgsqlCommand($@"
            SELECT tc.constraint_name
            FROM information_schema.table_constraints tc
            WHERE tc.constraint_type = 'FOREIGN KEY'
            AND tc.table_schema = @schema
            AND tc.table_name = @tablename", targetConnection))
        {
          disableCmd.Parameters.AddWithValue("schema", schema);
          disableCmd.Parameters.AddWithValue("tablename", targetTable.TableName.ToLower());

          var constraints = new List<string>();
          using (var reader = disableCmd.ExecuteReader())
          {
            while (reader.Read())
            {
              constraints.Add(reader.GetString(0));
            }
          }

          // Désactiver chaque contrainte de clé étrangère
          foreach (var constraint in constraints)
          {
            using (var alterCmd = new NpgsqlCommand($"ALTER TABLE {schema}.{targetTable.TableName.ToLower()} ALTER CONSTRAINT \"{constraint}\" DEFERRABLE INITIALLY DEFERRED", targetConnection))
            {
              alterCmd.ExecuteNonQuery();
            }
          }
        }

        // Vider la table cible
        using (var truncateCmd = new NpgsqlCommand($"TRUNCATE TABLE {schema}.{targetTable.TableName.ToLower()} CASCADE", targetConnection))
        {
          truncateCmd.ExecuteNonQuery();
        }

        // Réinitialiser la séquence si elle existe
        using (var cmd = new NpgsqlCommand(@"
            SELECT pg_get_serial_sequence(@schema || '.' || @tablename, column_name) as sequence_name
            FROM information_schema.columns 
            WHERE table_schema = @schema 
            AND table_name = @tablename 
            AND column_default LIKE 'nextval%'", targetConnection))
        {
          cmd.Parameters.AddWithValue("schema", schema);
          cmd.Parameters.AddWithValue("tablename", targetTable.TableName.ToLower());

          using (var reader = cmd.ExecuteReader())
          {
            if (reader.Read())
            {
              string sequenceName = reader.GetString(0);
              if (!string.IsNullOrEmpty(sequenceName))
              {
                using (var seqCmd = new NpgsqlCommand($"ALTER SEQUENCE {sequenceName} RESTART WITH 1", targetConnection))
                {
                  seqCmd.ExecuteNonQuery();
                }
              }
            }
          }
        }

        // Récupérer la structure de la table
        var columnsQuery = $@"SELECT column_name, data_type, data_length, data_precision, data_scale, nullable, column_id
                            FROM all_tab_columns 
                            WHERE table_name = '{targetTable.TableName}' 
                            ORDER BY column_id";

        var columns = new List<ColumnInfo>();
        var processedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = new OracleCommand(columnsQuery, sourceConnection))
        {
          using (var reader = cmd.ExecuteReader())
          {
            while (reader.Read())
            {
              var columnName = reader.GetString(0);
              if (!processedColumns.Contains(columnName))
              {
                processedColumns.Add(columnName);
                columns.Add(new ColumnInfo
                {
                  ColumnName = columnName,
                  DataType = reader.GetString(1),
                  Length = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                  Precision = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                  Scale = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                  IsNullable = reader.GetString(5) == "Y"
                });
              }
              else
              {
                var columnId = reader.GetInt32(6);
                LogMessage($"Warning: Duplicate column {columnName} found in table {targetTable.TableName} at position {columnId}");

                // Get the table's constraints to understand the relationships
                using (var constraintCmd = new OracleCommand($@"
                    SELECT a.constraint_name, a.constraint_type, a.r_constraint_name,
                           b.column_name, b.position
                    FROM all_constraints a
                    JOIN all_cons_columns b ON a.constraint_name = b.constraint_name
                    WHERE a.table_name = '{targetTable.TableName}'
                    AND b.column_name = '{columnName}'", sourceConnection))
                {
                  using (var constraintReader = constraintCmd.ExecuteReader())
                  {
                    while (constraintReader.Read())
                    {
                      var constraintInfo = $"Constraint: {constraintReader.GetString(0)}, " +
                                         $"Type: {constraintReader.GetString(1)}, " +
                                         $"Position: {constraintReader.GetInt32(4)}";
                      LogMessage($"Related constraint for duplicate column {columnName}: {constraintInfo}");
                    }
                  }
                }
              }
            }
          }
        }

        // Log table structure information
        LogMessage($"Table {targetTable.TableName} structure:");
        foreach (var col in columns)
        {
          LogMessage($"Column: {col.ColumnName}, Type: {col.DataType}, Nullable: {col.IsNullable}");
        }

        // Construire la requête de sélection
        var columnList = string.Join(", ", columns.Select(c => c.ColumnName));
        var selectQuery = $"SELECT {columnList} FROM {targetTable.TableName}";

        // Construire la requête d'insertion
        var columnListPg = string.Join(", ", columns.Select(c => c.ColumnName.ToLower()));
        var paramList = string.Join(", ", columns.Select(c => $"@{c.ColumnName.ToLower()}"));
        var insertQuery = $"INSERT INTO {schema}.{targetTable.TableName.ToLower()} ({columnListPg}) VALUES ({paramList})";

        // Si la table a une auto-référence, on doit d'abord insérer avec NULL pour la référence
        bool hasAutoReference = false;
        string autoReferenceColumn = null;
        using (var cmd = new NpgsqlCommand(@"
          SELECT DISTINCT
            kcu.column_name
          FROM information_schema.table_constraints tc
          JOIN information_schema.key_column_usage kcu 
              ON tc.constraint_name = kcu.constraint_name
              AND kcu.table_schema = tc.table_schema
          WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema = @schema
              AND tc.table_name = @tablename", targetConnection))
        {
          cmd.Parameters.AddWithValue("schema", schema);
          cmd.Parameters.AddWithValue("tablename", targetTable.TableName.ToLower());

          using (var reader = cmd.ExecuteReader())
          {
            if (reader.Read())
            {
              hasAutoReference = true;
              autoReferenceColumn = reader.GetString(0).ToUpper();
              LogMessage($"Table {targetTable.TableName} has auto-reference on column {autoReferenceColumn}");
            }
          }
        }

        if (hasAutoReference)
        {
          insertQuery = $@"INSERT INTO {schema}.{targetTable.TableName.ToLower()} ({columnListPg}) 
                         VALUES ({string.Join(", ", columns.Select(c =>
                             c.ColumnName.Equals(autoReferenceColumn, StringComparison.OrdinalIgnoreCase)
                             ? "NULL"
                             : $"@{c.ColumnName.ToLower()}"))})"
                         ;
        }

        // Lire les données source
        using (var selectCmd = new OracleCommand(selectQuery, sourceConnection))
        using (var reader = selectCmd.ExecuteReader())
        {
          var insertedRows = new List<Dictionary<string, object>>();

          while (reader.Read())
          {
            var rowData = new Dictionary<string, object>();
            using (var insertCmd = new NpgsqlCommand(insertQuery, targetConnection))
            {
              foreach (var column in columns)
              {
                var value = reader[column.ColumnName];
                if (value == DBNull.Value)
                {
                  insertCmd.Parameters.AddWithValue($"@{column.ColumnName.ToLower()}", DBNull.Value);
                }
                else
                {
                  insertCmd.Parameters.AddWithValue($"@{column.ColumnName.ToLower()}", value);
                }
                rowData[column.ColumnName] = value;
              }

              try
              {
                insertCmd.ExecuteNonQuery();
                insertedRows.Add(rowData);
              }
              catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "23503") // Foreign key violation
              {
                var details = $"Foreign key violation in table {targetTable.TableName}:\n";
                details += $"Error: {pgEx.Message}\n";
                details += $"Detail: {pgEx.Detail}\n";
                details += "This usually means the referenced record in the parent table doesn't exist.";
                LogMessage(details);

                // Log the data that caused the violation
                var rowDataStr = string.Join(", ", rowData.Select(kv => $"{kv.Key}={kv.Value}"));
                LogMessage($"Row data that caused violation: {rowDataStr}");

                throw;
              }
              catch (Exception exception)
              {
                LogMessage($"Error inserting row in {targetTable.TableName}: {exception.Message}");
                throw;
              }
            }
          }

          // Si la table a une auto-référence, mettre à jour les références maintenant
          if (hasAutoReference && insertedRows.Any())
          {
            LogMessage($"Updating self-references for table {targetTable.TableName}");
            foreach (var row in insertedRows)
            {
              if (row[autoReferenceColumn] != DBNull.Value)
              {
                var updateQuery = $@"
                  UPDATE {schema}.{targetTable.TableName.ToLower()}
                  SET {autoReferenceColumn.ToLower()} = @parentid
                  WHERE ";

                // Trouver la clé primaire
                using (var cmd = new NpgsqlCommand(@"
                  SELECT DISTINCT kcu.column_name
                  FROM information_schema.table_constraints tc
                  JOIN information_schema.key_column_usage kcu 
                      ON tc.constraint_name = kcu.constraint_name
                      AND kcu.table_schema = tc.table_schema
                  WHERE tc.constraint_type = 'PRIMARY KEY'
                      AND tc.table_schema = @schema
                      AND tc.table_name = @tablename", targetConnection))
                {
                  cmd.Parameters.AddWithValue("schema", schema);
                  cmd.Parameters.AddWithValue("tablename", targetTable.TableName.ToLower());

                  using (var pkReader = cmd.ExecuteReader())
                  {
                    if (pkReader.Read())
                    {
                      var pkColumn = pkReader.GetString(0).ToUpper();
                      updateQuery += $"{pkColumn.ToLower()} = @id";

                      using (var updateCmd = new NpgsqlCommand(updateQuery, targetConnection))
                      {
                        updateCmd.Parameters.AddWithValue("@parentid", row[autoReferenceColumn]);
                        updateCmd.Parameters.AddWithValue("@id", row[pkColumn]);
                        updateCmd.ExecuteNonQuery();
                      }
                    }
                  }
                }
              }
            }
          }
        }

        // Réactiver les contraintes de clé étrangère
        using (var enableCmd = new NpgsqlCommand($@"
            SELECT tc.constraint_name
            FROM information_schema.table_constraints tc
            WHERE tc.constraint_type = 'FOREIGN KEY'
            AND tc.table_schema = @schema
            AND tc.table_name = @tablename", targetConnection))
        {
          enableCmd.Parameters.AddWithValue("schema", schema);
          enableCmd.Parameters.AddWithValue("tablename", targetTable.TableName.ToLower());

          var constraints = new List<string>();
          using (var reader = enableCmd.ExecuteReader())
          {
            while (reader.Read())
            {
              constraints.Add(reader.GetString(0));
            }
          }

          // Réactiver chaque contrainte de clé étrangère
          foreach (var constraint in constraints)
          {
            using (var alterCmd = new NpgsqlCommand($"ALTER TABLE {schema}.{targetTable.TableName.ToLower()} VALIDATE CONSTRAINT \"{constraint}\"", targetConnection))
            {
              alterCmd.ExecuteNonQuery();
            }
          }
        }

        // Réactiver les triggers utilisateur
        using (var enableTriggersCmd = new NpgsqlCommand($"ALTER TABLE {schema}.{targetTable.TableName.ToLower()} ENABLE TRIGGER USER", targetConnection))
        {
          enableTriggersCmd.ExecuteNonQuery();
        }
      }
      catch (Exception exception)
      {
        // S'assurer de réactiver les contraintes même en cas d'erreur
        if (targetConnection != null && targetConnection.State == ConnectionState.Open)
        {
          try
          {
            using (var enableCmd = new NpgsqlCommand($@"
                    SELECT tc.constraint_name
                    FROM information_schema.table_constraints tc
                    WHERE tc.constraint_type = 'FOREIGN KEY'
                    AND tc.table_schema = @schema
                    AND tc.table_name = @tablename", targetConnection))
            {
              enableCmd.Parameters.AddWithValue("schema", schema);
              enableCmd.Parameters.AddWithValue("tablename", targetTable.TableName.ToLower());

              var constraints = new List<string>();
              using (var reader = enableCmd.ExecuteReader())
              {
                while (reader.Read())
                {
                  constraints.Add(reader.GetString(0));
                }
              }

              // Réactiver chaque contrainte de clé étrangère
              foreach (var constraint in constraints)
              {
                using (var alterCmd = new NpgsqlCommand($"ALTER TABLE {schema}.{targetTable.TableName.ToLower()} VALIDATE CONSTRAINT \"{constraint}\"", targetConnection))
                {
                  alterCmd.ExecuteNonQuery();
                }
              }
            }

            // Réactiver les triggers utilisateur
            using (var enableTriggersCmd = new NpgsqlCommand($"ALTER TABLE {schema}.{targetTable.TableName.ToLower()} ENABLE TRIGGER USER", targetConnection))
            {
              enableTriggersCmd.ExecuteNonQuery();
            }
          }
          catch
          {
            // Ignore errors when reactivating triggers
          }
        }

        LogMessage($"Error copying table {targetTable.TableName}: {exception.Message}");
        throw;
      }
      finally
      {
        if (sourceConnection != null)
        {
          if (sourceConnection.State == ConnectionState.Open)
          {
            sourceConnection.Close();
          }

          sourceConnection.Dispose();
        }

        if (targetConnection != null)
        {
          if (targetConnection.State == ConnectionState.Open)
          {
            targetConnection.Close();
          }

          targetConnection.Dispose();
        }
      }
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
      var selectedCount = lstOracleStoredProcs.Items.Cast<ListBoxItem>()
        .Count(item => ((CheckBox)((StackPanel)item.Content).Children[0]).IsChecked == true);

      txtOracleSelectedStoredProcsCount.Text = selectedCount.ToString();
      txtOracleStoredProcsLabel.Text = $" stored procedure{StringHelper.Plural(selectedCount)}";
    }

    private void UpdatePostgresStoredProcsSelectedCount()
    {
      var selectedCount = lstPostgresStoredProcs.Items.Cast<ListBoxItem>()
        .Count(item => ((CheckBox)((StackPanel)item.Content).Children[0]).IsChecked == true);

      txtPostgresSelectedStoredProcsCount.Text = selectedCount.ToString();
      txtPostgresStoredProcsLabel.Text = $" stored procedure{StringHelper.Plural(selectedCount)}";
    }

    private async void BtnLoadOracleStoredProcs_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        var loadingWindow = new LoadingWindow(this) { Title = "Loading Stored Procedures..." };
        loadingWindow.Show();

        ResetButtonColor(btnLoadOracleStoredProcs);
        lstOracleStoredProcs.Items.Clear();

        InitializeServices();
        if (_oracleService == null)
        {
          loadingWindow.Close();
          return;
        }

        var storedProcs = await Task.Run(() => _oracleService.GetStoredProcedures());

        foreach (var proc in storedProcs)
        {
          var item = new ListBoxItem
          {
            Content = new StackPanel { Orientation = Orientation.Horizontal }
          };

          var checkBox = new CheckBox { Margin = new Thickness(5, 0, 5, 0) };
          checkBox.Checked += (s, evt) => UpdateOracleStoredProcsSelectedCount();
          checkBox.Unchecked += (s, evt) => UpdateOracleStoredProcsSelectedCount();
          var nameBlock = new TextBlock { Text = proc.Name };

          ((StackPanel)item.Content).Children.Add(checkBox);
          ((StackPanel)item.Content).Children.Add(nameBlock);

          if (proc.Type == "PACKAGE" && proc.PackageProcedures?.Any() == true)
          {
            for (int i = 0; i < proc.PackageProcedures.Count; i++)
            {
              var newItem = new ListBoxItem();
              var newStackPanel = new StackPanel { Orientation = Orientation.Horizontal };
              var checkBox2 = new CheckBox { Margin = new Thickness(5, 0, 5, 0) };
              checkBox2.Checked += (s, evt) => UpdateOracleStoredProcsSelectedCount();
              checkBox2.Unchecked += (s, evt) => UpdateOracleStoredProcsSelectedCount();
              var packageBlock = new TextBlock
              {
                Text = proc.Name,
                Margin = new Thickness(5, 0, 0, 0),
              };

              var proceduresBlock = new TextBlock
              {
                Text = $"_{proc.PackageProcedures[i]}",
                Margin = new Thickness(0, 0, 5, 0),
              };

              newStackPanel.Children.Add(checkBox2);
              newStackPanel.Children.Add(packageBlock);
              newStackPanel.Children.Add(proceduresBlock);
              newItem.Content = newStackPanel;
              lstOracleStoredProcs.Items.Add(newItem);
            }
          }
          else
          {
            lstOracleStoredProcs.Items.Add(item);
          }
        }

        SetButtonSuccess(btnLoadOracleStoredProcs);
        UpdateOracleStoredProcsSelectedCount();
        CompareStoredProcedures();
        loadingWindow.Close();
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Error loading stored procedures: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        SetButtonError(btnLoadOracleStoredProcs);
      }
    }

    private void FilterOracleStoredProcs()
    {
      if (lstOracleStoredProcs.Items.Count == 0)
      {
        return;
      }

      var view = CollectionViewSource.GetDefaultView(lstOracleStoredProcs.Items);
      var searchText = txtOracleStoredProcsSearch.Text.ToLower();
      view.Filter = item =>
      {
        if (item is ListBoxItem listBoxItem && listBoxItem.Tag is OracleProcStockUnit proc)
        {
          var matchName = proc.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
          var matchType = proc.Type.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
          var matchProcedures = proc.PackageProcedures?.Any(p => p.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0) ?? false;
          return matchName || matchType || matchProcedures;
        }
        return false;
      };
    }

    private void BtnMigrateStoredProcs_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        var selectedProcs = lstOracleStoredProcs.Items
          .Cast<ListBoxItem>()
          .Where(item => ((CheckBox)((StackPanel)item.Content).Children[0]).IsChecked == true)
          .ToList();

        if (!selectedProcs.Any())
        {
          MessageBox.Show("Please select at least one stored procedure to migrate.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        foreach (var proc in selectedProcs)
        {
          MigrateStoredProcedure(proc.Content.ToString());
        }

        MessageBox.Show("Migration of selected stored procedures completed.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
      }
      catch (Exception exception)
      {
        MessageBox.Show($"Error during stored procedures migration: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        LogMessage($"Error during stored procedures migration: {exception.Message}");
      }
    }

    private void TxtOracleStoredProcsSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
      FilterOracleStoredProcs();
    }

    private void TxtPostgresStoredProcsSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
      FilterPostgresStoredProcs();
    }

    private void FilterPostgresStoredProcs()
    {
      if (lstPostgresStoredProcs.ItemsSource == null)
      {
        return;
      }

      var searchText = txtPostgresStoredProcsSearch.Text.ToLower();
      var procedures = lstPostgresStoredProcs.ItemsSource.Cast<StoredProcedureItem>().ToList();

      if (string.IsNullOrWhiteSpace(searchText))
      {
        lstPostgresStoredProcs.ItemsSource = procedures;
      }
      else
      {
        var filtered = procedures.Where(p => p.ProcedureName.ToLower().Contains(searchText)).ToList();
        lstPostgresStoredProcs.ItemsSource = filtered;
      }

      UpdatePostgresStoredProcsSelectedCount();
    }

    private void CompareStoredProcedures()
    {
      var oracleProcs = lstOracleStoredProcs.Items.Cast<ListBoxItem>();
      var postgresProcs = lstPostgresStoredProcs.Items.Cast<ListBoxItem>();

      // Reset backgrounds
      foreach (ListBoxItem item in lstOracleStoredProcs.Items)
      {
        item.Background = null;
      }
      foreach (ListBoxItem item in lstPostgresStoredProcs.Items)
      {
        item.Background = null;
      }

      // Compare procedures
      foreach (var oracleItem in oracleProcs)
      {
        var oracleStackPanel = (StackPanel)oracleItem.Content;
        var oracleName = ((TextBlock)oracleStackPanel.Children[1]).Text.ToLower();
        if (oracleStackPanel.Children.Count >= 3)
        {
          oracleName += ((TextBlock)oracleStackPanel.Children[2]).Text.ToLower();
        }

        var postgresItem = postgresProcs.FirstOrDefault(p =>
        {
          var stackPanel = (StackPanel)p.Content;
          var name = ((TextBlock)stackPanel.Children[1]).Text.ToLower();
          var test = name.ToLower() == oracleName.ToLower();
          return name.Equals(oracleName, StringComparison.CurrentCultureIgnoreCase);
        });

        if (postgresItem != null)
        {
          var color = new SolidColorBrush(Colors.LightGreen);
          oracleItem.Background = color;
          postgresItem.Background = color;
        }
        else
        {
          oracleItem.Background = new SolidColorBrush(Colors.LightPink);
        }
      }

      // Mark PostgreSQL procedures that don't exist in Oracle
      foreach (var postgresItem in postgresProcs)
      {
        if (postgresItem.Background == null)
        {
          postgresItem.Background = new SolidColorBrush(Colors.LightPink);
        }
      }
    }

    private async Task MigrateStoredProcedure(string procedureName)
    {
      OracleConnection sourceConnection = null;
      NpgsqlConnection targetConnection = null;

      try
      {
        sourceConnection = new OracleConnection(GetOracleConnectionString());
        targetConnection = new NpgsqlConnection(GetPostgresConnectionString());

        await sourceConnection.OpenAsync();
        await targetConnection.OpenAsync();

        var schema = txtPostgresSchema.Text.ToLower();

        // Get source code for the Oracle procedure
        string oracleSource = await GetOracleProcedureSource(procedureName, sourceConnection);

        // Convert Oracle code to PostgreSQL code 
        string postgresSource = ConvertOracleToPostgresProcedure(oracleSource, procedureName, schema);

        // Delete the procedure if it already exist
        await DropExistingProcedure(procedureName, targetConnection, schema);

        // Create the new procedure
        using (var createCmd = new NpgsqlCommand(postgresSource, targetConnection))
        {
          await createCmd.ExecuteNonQueryAsync();
        }

        LogMessage($"Successfully migrated stored procedure {procedureName}");
      }
      catch (Exception exception)
      {
        LogMessage($"Error migrating stored procedure {procedureName}: {exception.Message}");
        throw;
      }
      finally
      {
        if (sourceConnection != null)
        {
          if (sourceConnection.State == ConnectionState.Open)
          {
            sourceConnection.Close();
          }
          sourceConnection.Dispose();
        }

        if (targetConnection != null)
        {
          if (targetConnection.State == ConnectionState.Open)
          {
            await targetConnection.CloseAsync();
          }

          await targetConnection.DisposeAsync();
        }
      }
    }

    private async Task<string> GetOracleProcedureSource(string procedureName, OracleConnection connection)
    {
      var query = @"
        SELECT text
        FROM all_source 
        WHERE name = :procName
        AND type IN ('PROCEDURE', 'FUNCTION', 'PACKAGE', 'PACKAGE BODY')
        ORDER BY type, line";

      using (var cmd = new OracleCommand(query, connection))
      {
        cmd.Parameters.Add(new OracleParameter("procName", procedureName.Contains(".") ? procedureName.Split('.')[0] : procedureName));

        var source = new StringBuilder();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
          while (await reader.ReadAsync())
          {
            source.Append(reader.GetString(0));
          }
        }

        return source.ToString();
      }
    }

    private async Task DropExistingProcedure(string procedureName, NpgsqlConnection connection, string schema)
    {
      try
      {
        var simpleName = procedureName.Contains(".") ? procedureName.Split('.')[1] : procedureName;
        var query = $"DROP PROCEDURE IF EXISTS {schema}.{simpleName.ToLower()}";
        using (var cmd = new NpgsqlCommand(query, connection))
        {
          await cmd.ExecuteNonQueryAsync();
        }
      }
      catch (Exception exception)
      {
        LogMessage($"Warning: Could not drop existing procedure {procedureName}: {exception.Message}");
      }
    }

    private string ConvertOracleToPostgresProcedure(string oracleSource, string procedureName, string schema)
    {
      // Conversion de base du code Oracle en PostgreSQL
      var postgresSource = oracleSource
        .Replace("BEGIN", "BEGIN")  // Garder BEGIN tel quel
        .Replace("END;", "END;")    // Garder END tel quel
        .Replace(":=", "=")         // Remplacer l'opérateur d'affectation
        .Replace("SYSDATE", "CURRENT_TIMESTAMP") // Remplacer SYSDATE
        .ToLower();                 // PostgreSQL préfère le minuscule

      var simpleName = procedureName.Contains(".") ? procedureName.Split('.')[1] : procedureName;

      // Ajuster la déclaration de la procédure
      var match = Regex.Match(postgresSource, @"(?:procedure|function)\s+" + simpleName.ToLower() + @"\s*\((.*?)\)", RegexOptions.IgnoreCase);
      if (match.Success)
      {
        var parameters = match.Groups[1].Value;
        // Convertir les paramètres Oracle en format PostgreSQL
        parameters = ConvertParameters(parameters);

        postgresSource = $"CREATE OR REPLACE PROCEDURE {schema}.{simpleName.ToLower()}({parameters})\nLANGUAGE plpgsql\nAS $$\n{postgresSource}\n$$;";
      }

      return postgresSource;
    }

    private string ConvertParameters(string parameters)
    {
      if (string.IsNullOrWhiteSpace(parameters))
      {
        return "";
      }

      var paramList = parameters.Split(',')
          .Select(p => p.Trim())
          .Select(p =>
          {
            // Convertir les types de données Oracle en types PostgreSQL
            return p.Replace("VARCHAR2", "VARCHAR")
                   .Replace("NUMBER", "NUMERIC")
                   .Replace("DATE", "TIMESTAMP")
                   .Replace("CLOB", "TEXT")
                   .Replace("IN OUT", "INOUT")
                   .ToLower();
          });

      return string.Join(", ", paramList);
    }

    private void ChkSavePostgres_Checked(object sender, RoutedEventArgs e)
    {
    }

    private void ChkSaveOracle_Checked(object sender, RoutedEventArgs e)
    {
    }

    private void ChkShowOracleStoredProcs_Checked(object sender, RoutedEventArgs e)
    {
      if (lstOracleStoredProcs.Items.Count > 0)
      {
        lstOracleStoredProcs.Visibility = Visibility.Visible;
      }
    }

    private void ChkShowOracleStoredProcs_Unchecked(object sender, RoutedEventArgs e)
    {
      lstOracleStoredProcs.Visibility = Visibility.Collapsed;
    }

    private void BtnLoadOracleConnection_Click(object sender, RoutedEventArgs e)
    {
      if (cboOracleLConnectionProfileFile.SelectedIndex == -1)
      {
        MessageBox.Show("You have to select a profile name for the Oracle connection", "No profile chosen", MessageBoxButton.OK, MessageBoxImage.Hand);
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
      if (cboPostgresConnectionProfileFile.SelectedIndex == -1)
      {
        MessageBox.Show("You have to select a profile name for the PostgreSQL connection", "No profile chosen", MessageBoxButton.OK, MessageBoxImage.Hand);
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
        chkSavePostgres.IsChecked = true;
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

    private void BtnLoadPostgresStoredProcs_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        ResetButtonColor(btnLoadPostgresStoredProcs);
        var storedProcs = _postgresService.GetStoredProcedures();
        lstPostgresStoredProcs.Items.Clear();

        foreach (var proc in storedProcs)
        {
          var item = new ListBoxItem
          {
            Content = new StackPanel { Orientation = Orientation.Horizontal }
          };

          var checkBox = new CheckBox { Margin = new Thickness(5, 0, 5, 0) };
          checkBox.Checked += (s, evt) => UpdatePostgresStoredProcsSelectedCount();
          checkBox.Unchecked += (s, evt) => UpdatePostgresStoredProcsSelectedCount();
          var nameBlock = new TextBlock { Text = proc };

          ((StackPanel)item.Content).Children.Add(checkBox);
          ((StackPanel)item.Content).Children.Add(nameBlock);

          lstPostgresStoredProcs.Items.Add(item);
        }

        SetButtonSuccess(btnLoadPostgresStoredProcs);
        UpdatePostgresStoredProcsSelectedCount();
        CompareStoredProcedures();
      }
      catch (Exception exception)
      {
        SetButtonError(btnLoadPostgresStoredProcs);
        MessageBox.Show($"Error loading PostgreSQL stored procedures: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        LogMessage($"Error loading PostgreSQL stored procedures: {exception.Message}");
      }
    }
  }
}
