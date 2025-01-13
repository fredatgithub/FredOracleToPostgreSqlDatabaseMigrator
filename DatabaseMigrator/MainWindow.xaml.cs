using DatabaseMigrator.Helpers;
using DatabaseMigrator.Models;
using DatabaseMigrator.Properties;
using DatabaseMigrator.Views;
using Newtonsoft.Json;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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
  public partial class MainWindow : Window
  {
    private readonly string _oracleCredentialsFileTemplate = "id_oracle-{profilName}.txt";
    private readonly string _pgCredentialsFileTemplate = "id_pg-{profilName}.txt";
    private string _oracleCredentialsFile = "id_oracle.txt";
    private string _pgCredentialsFile = "id_pg.txt";
    private readonly string _logFile = "log.txt";
    private IOracleService _oracleService;
    private IPostgresService _postgresService;

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
        cboOracleConnectionProfil.SelectedItem = lastOracleProfil;
        _oracleCredentialsFile = _oracleCredentialsFileTemplate.Replace("{profilName}", lastOracleProfil);
      }

      if (!string.IsNullOrEmpty(lastPostgresProfil))
      {
        cboPostgresqlConnectionProfil.SelectedItem = lastPostgresProfil;
        _pgCredentialsFile = _pgCredentialsFileTemplate.Replace("{profilName}", lastPostgresProfil);
      }
    }

    private void LoadProfilCombobox()
    {
      LoadProfilForDatabase("id_oracle-*.txt", cboOracleConnectionProfil);
      LoadProfilForDatabase("id_pg-*.txt", cboPostgresqlConnectionProfil);
    }

    private void LoadProfilForDatabase(string databaseName, ComboBox comboBox)
    {
      var profils = GetProfilFile(databaseName);
      comboBox.Items.Clear();
      foreach (var profil in profils)
      {
        var profilWithoutExtension = Path.GetFileNameWithoutExtension(profil);
        var array = profilWithoutExtension.Split('-');
        if (array.Length >= 2)
        {
          var profilName = array[1];
          comboBox.Items.Add(profilName);
        }
      }
    }

    private List<string> GetProfilFile(string pattern)
    {
      var result = GetAllFiles(pattern);
      result = RemoveFirstCharacters(result, 2);
      return result;
    }

    private static List<string> RemoveFirstCharacters(List<string> list, int nbCharacters = 0)
    {
      return list.Select(ligne => ligne.Length > nbCharacters ? ligne.Substring(nbCharacters) : string.Empty).ToList();
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
          LogMessage($"Successfully loaded {tables.Count} Oracle tables ✓");
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
          LogMessage($"Successfully loaded {tables.Count} PostgreSQL tables ✓");
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
        if (chkSaveOracle.IsChecked == true && cboOracleConnectionProfil.SelectedIndex != -1)
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

        // Save PostgreSQL ID
        if (chkSavePostgres.IsChecked == true && cboPostgresqlConnectionProfil.SelectedIndex != -1)
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
      catch (Exception exception)
      {
        MessageBox.Show($"Error saving credentials: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        using (var connection = new OracleConnection(GetOracleConnectionString()))
        {
          await connection.OpenAsync();
          LogMessage("Oracle connection successful! ✓");
          SetButtonSuccess(btnTestOracle);
        }
      }
      catch (Exception exception)
      {
        LogMessage($"Oracle connection failed: {exception.Message} ✗");
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
          LogMessage("PostgreSQL connection successful! ✓");
          SetButtonSuccess(btnTestPostgres);
        }
      }
      catch (Exception exception)
      {
        LogMessage($"PostgreSQL connection failed: {exception.Message} ✗");
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

    private void UpdateOracleSelectedCount()
    {
      if (lstOracleTables.ItemsSource is IEnumerable<TableInfo> tables)
      {
        var selectedCount = tables.Count(t => t.IsSelected);
        txtOracleSelectedCount.Text = selectedCount.ToString();
        txtOracleTableLabel.Text = $" table{Plural(selectedCount)}";
      }
    }

    private void UpdatePostgresSelectedCount()
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

    private async void BtnLoadOracleStoredProcs_Click(object sender, RoutedEventArgs e)
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

    private async void BtnLoadPostgresStoredProcs_Click(object sender, RoutedEventArgs e)
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

        LogMessage($"Loaded {procedures.Count()} PostgreSQL stored procedures.");
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

    private void ChkSavePostgres_Checked(object sender, RoutedEventArgs e)
    {
      if (chkSavePostgres.IsChecked == true && cboPostgresqlConnectionProfil.SelectedIndex == -1)
      {
        MessageBox.Show("You have to select a profile name for the PostgreSQL connection", "No profile choosen", MessageBoxButton.OK, MessageBoxImage.Hand);
        chkSavePostgres.IsChecked = false;
      }
    }

    private void ChkSaveOracle_Checked(object sender, RoutedEventArgs e)
    {
      if (chkSaveOracle.IsChecked == true && cboOracleConnectionProfil.SelectedIndex == -1)
      {
        MessageBox.Show("You have to select a profile name for the Oracle connection", "No profile choosen", MessageBoxButton.OK, MessageBoxImage.Hand);
        chkSaveOracle.IsChecked = false;
      }
    }
  }
}
