using System;
using System.Threading;
using System.Windows.Threading;

namespace DatabaseMigrator
{
    internal class Program
    {
        [STAThread]
        static void Main()
        {
            var app = new App();
            app.InitializeComponent();
            app.Dispatcher.Thread.SetApartmentState(ApartmentState.STA);
            app.Run();
        }
    }
}
