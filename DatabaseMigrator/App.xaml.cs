using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DatabaseMigrator
{
    public partial class App : Application
    {
        public App()
        {
            Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"An error occurred: {e.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
