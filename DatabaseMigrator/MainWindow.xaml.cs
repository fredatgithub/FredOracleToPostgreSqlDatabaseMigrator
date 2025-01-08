using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using DatabaseMigrator.Helpers;
using Newtonsoft.Json;
using MessageBox = System.Windows.MessageBox;

namespace DatabaseMigrator
{
    public partial class MainWindow : Window
    {
        private readonly string _oracleCredentialsFile = "id_oracle.txt";
        private readonly string _pgCredentialsFile = "id_pg.txt";

        public MainWindow()
        {
            InitializeComponent();
            LoadSavedCredentials();
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
        }

        public class DbCredentials
        {
            public string Server { get; set; }
            public string Port { get; set; }
            public string Database { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
