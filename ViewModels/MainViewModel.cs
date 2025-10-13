using ReplaysApp.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ReplaysApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private object _currentViewModel;
        public object CurrentViewModel
        {
            get => _currentViewModel;
            set { _currentViewModel = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            ShowLoginView();
        }
        
        private void ShowLoginView()
        {
            var loginViewModel = new LoginViewModel();
            loginViewModel.LoginSucceeded += OnLoginSucceeded;
            CurrentViewModel = loginViewModel;
        }

        private void OnLoginSucceeded(User loggedInUser)
        {
            CurrentViewModel = new ReplaysViewModel(loggedInUser, OnLogoutRequest);
        }

        private void OnLogoutRequest()
        {
            ShowLoginView();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}