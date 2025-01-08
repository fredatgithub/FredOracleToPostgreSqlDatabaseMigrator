using System.Windows;

namespace DatabaseMigrator.Views
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow(Window owner)
        {
            InitializeComponent();
            Owner = owner;

            // Centrer la fenêtre par rapport à la fenêtre principale
            Loaded += (s, e) =>
            {
                Left = owner.Left + (owner.Width - Width) / 2;
                Top = owner.Top + (owner.Height - Height) / 2;
            };
        }
    }
}
