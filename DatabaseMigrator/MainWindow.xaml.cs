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
      //var array = GetProfilNameFromFilename(result);
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
        view.Filter = item => string.IsNullOrEmpty(searchText) ||
                             (item as TableInfo)?.TableName.ToUpperInvariant().Contains(searchText) == true;
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

    private async void BtnLoadOracleTables_Click(object sender, RoutedEventArgs e)
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

    private async void BtnLoadPostgresTables_Click(object sender, RoutedEventArgs e)
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
      txtLogs.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
      txtLogs.ScrollToEnd();
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

    private async void BtnTestOracle_Click(object sender, RoutedEventArgs e)
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

    private async void BtnTestPostgres_Click(object sender, RoutedEventArgs e)
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
      return $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={txtOracleServer.Text})(PORT={txtOraclePort.Text}))(CONNECT_DATA=(SERVICE_NAME={txtOracleServiceName.Text})));User Id={txtOracleUser.Text};Password={pwdOraclePassword.Password};";
    }

    private string GetPostgresConnectionString()
    {
      return $"Host={txtPostgresServer.Text};Port={txtPostgresPort.Text};Database={txtPostgresDatabase.Text};Username={txtPostgresUser.Text};Password={pwdPostgresPassword.Password}";
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
      // Restore window position and size
      var settings = Properties.Settings.Default;

      // If it's the first time the application is launched, center the window
      if (settings.WindowTop == 0 && settings.WindowLeft == 0)
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

    private static string Plural(int count)
    {
      return count > 1 ? "s" : "";
    }

    public void UpdateOracleSelectedCount()
    {
      if (lstOracleTables.ItemsSource is IEnumerable<TableInfo> tables)
      {
        var selectedCount = tables.Count(t => t.IsSelected);
        txtOracleSelectedCount.Text = selectedCount.ToString();
        txtOracleTableLabel.Text = $" table{Plural(selectedCount)}";
      }
    }

    public void UpdatePostgresSelectedCount()
    {
      if (lstPostgresTables.ItemsSource is IEnumerable<TableInfo> tables)
      {
        var selectedCount = tables.Count(t => t.IsSelected);
        txtPostgresSelectedCount.Text = selectedCount.ToString();
        txtPostgresTableLabel.Text = $" table{Plural(selectedCount)}";
      }
    }

    private void UpdateOracleStoredProcsSelectedCount()
    {
      if (lstOracleStoredProcs.ItemsSource is IEnumerable<StoredProcedureItem> procedures)
      {
        var selectedCount = procedures.Count(p => p.IsSelected);
        txtOracleSelectedProcsCount.Text = selectedCount.ToString();
        txtOracleProcsLabel.Text = $" stored procedure{Plural(selectedCount)}";
      }
    }

    private void UpdatePostgresStoredProcsSelectedCount()
    {
      if (lstPostgresStoredProcs.ItemsSource is IEnumerable<StoredProcedureItem> procedures)
      {
        var selectedCount = procedures.Count(p => p.IsSelected);
        txtPostgresSelectedProcsCount.Text = selectedCount.ToString();
        txtPostgresProcsLabel.Text = $" stored procedure{Plural(selectedCount)}";
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

        var procedures = await Task.Run(() => _oracleService.GetStoredProcedures());

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

        var procedures = await Task.Run(() => _postgresService.GetStoredProcedures());

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

    public class StoredProcedureItem: INotifyPropertyChanged
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

    private void BtnMigrateTables_Click(object sender, RoutedEventArgs e)
    {
      var selectedOracleTables = lstOracleTables.ItemsSource.Cast<TableInfo>().Where(t => t.IsSelected).ToList();
      var allPostgresTables = lstPostgresTables.ItemsSource.Cast<TableInfo>().ToList();
      if (selectedOracleTables.Count == 0)
      {
        MessageBox.Show("Please select at least one table from the source database to migrate.", "No tables selected", MessageBoxButton.OK, MessageBoxImage.Hand);
        return;
      }

      // Make sure the target table has not the same number of records as the source table
      foreach (var table in selectedOracleTables)
      {
        if (table.RowCount == 0)
        {
          MessageBox.Show($"There is not record in {table.TableName} table to copy in the source database to migrate.", "No record to copy", MessageBoxButton.OK, MessageBoxImage.Hand);
          return;
        }

        var targetTable = allPostgresTables.FirstOrDefault(t => t.TableName.Equals(table.TableName, StringComparison.OrdinalIgnoreCase));
        if (table.RowCount == targetTable.RowCount)
        {
          MessageBox.Show($"The number of record for the source table is equal to the target table.", "Similar table records", MessageBoxButton.OK, MessageBoxImage.Hand);
          return;
        }

        if (targetTable.RowCount > 0)
        {
          MessageBox.Show($"The target table {targetTable.TableName} is not empty, it will be emptied before migrating the data.", "Target table not empty", MessageBoxButton.OK, MessageBoxImage.Hand);
        }

        // log table name
        LogMessage($"Migrating table {table.TableName} from Oracle to PostgreSQL...");

        try
        {
          using (var oracleConnection = new OracleConnection(GetOracleConnectionString()))
          using (var postgresConnection = new NpgsqlConnection(GetPostgresConnectionString()))
          {
            oracleConnection.Open();
            postgresConnection.Open();

            // Truncate target table and disable foreign key constraints
            using (var cmd = new NpgsqlCommand())
            {
              cmd.Connection = postgresConnection;
              
              // Disable foreign key constraints for this table
              cmd.CommandText = $"ALTER TABLE {targetTable.TableName} DISABLE TRIGGER ALL";
              cmd.ExecuteNonQuery();
              
              // Truncate the table
              cmd.CommandText = $"TRUNCATE TABLE {targetTable.TableName} RESTART IDENTITY CASCADE";
              cmd.ExecuteNonQuery();
              LogMessage($"Table {targetTable.TableName} truncated successfully");

              try
              {
                // Get data from Oracle
                var selectCommand = $"SELECT * FROM {table.TableName}";
                using (var oracleCmd = new OracleCommand(selectCommand, oracleConnection))
                using (var reader = oracleCmd.ExecuteReader())
                {
                  // Get column names and types
                  var schemaTable = reader.GetSchemaTable();
                  var columns = new List<string>();
                  var columnTypes = new List<Type>();
                  foreach (DataRow row in schemaTable.Rows)
                  {
                    columns.Add(row["ColumnName"].ToString());
                    columnTypes.Add((Type)row["DataType"]);
                  }

                  // Prepare insert statement
                  var columnList = string.Join(",", columns);
                  var paramList = string.Join(",", columns.Select(c => $"@{c}"));
                  var insertCommand = $"INSERT INTO {targetTable.TableName} ({columnList}) VALUES ({paramList})";

                  // Batch insert data
                  using (var transaction = postgresConnection.BeginTransaction())
                  {
                    try
                    {
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

                          insertCmd.ExecuteNonQuery();
                          rowCount++;

                          if (rowCount % 1000 == 0)
                          {
                            LogMessage($"Inserted {rowCount} rows into {targetTable.TableName}");
                          }
                        }

                        transaction.Commit();
                        LogMessage($"Successfully migrated {rowCount} rows from {table.TableName} to PostgreSQL");
                      }
                    }
                    catch (Exception exception)
                    {
                      transaction.Rollback();
                      throw new Exception($"Error while inserting data into {targetTable.TableName}: {exception.Message}");
                    }
                  }
                }
              }
              finally
              {
                // Re-enable foreign key constraints
                cmd.CommandText = $"ALTER TABLE {targetTable.TableName} ENABLE TRIGGER ALL";
                cmd.ExecuteNonQuery();
                LogMessage($"Re-enabled constraints for table {targetTable.TableName}");
              }
            }
          }
        }
        catch (Exception exception)
        {
          LogMessage($"Error migrating table {table.TableName}: {exception.Message}");
          MessageBox.Show($"Error migrating table {table.TableName}: {exception.Message}", "Migration Error", MessageBoxButton.OK, MessageBoxImage.Error);
          return;
        }
      }

      MessageBox.Show("Migration completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private NpgsqlDbType GetNpgsqlType(Type type)
    {
      if (type == typeof(int) || type == typeof(Int32))
        return NpgsqlDbType.Integer;

      if (type == typeof(long) || type == typeof(Int64))
        return NpgsqlDbType.Bigint;

      if (type == typeof(string) || type == typeof(char))
        return NpgsqlDbType.Text;

      if (type == typeof(DateTime))
        return NpgsqlDbType.Timestamp;

      if (type == typeof(bool))
        return NpgsqlDbType.Boolean;

      if (type == typeof(byte[]))
        return NpgsqlDbType.Bytea;

      if (type == typeof(float) || type == typeof(Single))
        return NpgsqlDbType.Real;

      if (type == typeof(double))
        return NpgsqlDbType.Double;

      if (type == typeof(decimal))
        return NpgsqlDbType.Numeric;

      if (type == typeof(short) || type == typeof(Int16))
        return NpgsqlDbType.Smallint;

      if (type == typeof(byte))
        return NpgsqlDbType.Smallint;

      if (type == typeof(Guid))
        return NpgsqlDbType.Uuid;

      if (type == typeof(TimeSpan))
        return NpgsqlDbType.Interval;

      if (type == typeof(DateTimeOffset))
        return NpgsqlDbType.TimestampTz;

      // Pour déboguer, affichons le type non supporté
      LogMessage($"Type non supporté détecté : {type.FullName}");
      throw new ArgumentException($"Type non supporté : {type.FullName}", nameof(type));
    }
  }
}
