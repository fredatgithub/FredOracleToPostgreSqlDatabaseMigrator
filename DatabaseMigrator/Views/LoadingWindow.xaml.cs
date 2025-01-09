using System.Windows;

namespace DatabaseMigrator.Views
{
  public partial class LoadingWindow : Window
  {
    public LoadingWindow(Window owner)
    {
      InitializeComponent();
      Owner = owner;

      // Center the window vs the main window
      Loaded += (s, e) =>
      {
        Left = owner.Left + (owner.Width - Width) / 2;
        Top = owner.Top + (owner.Height - Height) / 2;
      };
    }
  }
}
