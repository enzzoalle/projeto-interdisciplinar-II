using ReplaysApp.ViewModels;
using System.Windows;

namespace ReplaysApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}