using System;
using System.Threading;

namespace DatabaseMigrator
{
  internal static class Program
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
