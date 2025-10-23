using Npgsql;
using ReplaysApp.Commands;
using ReplaysApp.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;

namespace ReplaysApp.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        public event Action<User> LoginSucceeded;
        public string Username { get; set; }
        public string Password { get; set; }
        public ICommand LoginCommand { get; }
        public ICommand RegisterCommand { get; }
        public User LoggedInUser { get; private set; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand<object>(Login, CanLoginOrRegister);
            RegisterCommand = new RelayCommand<object>(Register, CanLoginOrRegister);
        }
        
        private bool CanLoginOrRegister(object parameter)
        {
            return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
        }

        private void Login(object parameter)
        {
            try
            {
                using (var conn = new NpgsqlConnection(App.Configuration.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();
                    var sql = "SELECT id, nome, isAdmin FROM Users WHERE nome = @nome AND senha = @senha";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("nome", Username);
                        cmd.Parameters.AddWithValue("senha", Password);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                LoggedInUser = new User
                                {
                                    Id = reader.GetInt32(0), 
                                    Nome = reader.GetString(1),
                                    isAdmin = reader.GetBoolean(2)
                                };
                                LoginSucceeded?.Invoke(LoggedInUser);
                            }
                            else
                            {
                                MessageBox.Show("Usuário ou senha inválidos.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Erro de banco de dados: {ex.Message}"); }
        }

        private void Register(object parameter)
        {
            try
            {
                using (var conn = new NpgsqlConnection(App.Configuration.GetConnectionString("DefaultConnection")))
                {
                    conn.Open();
                    var checkSql = "SELECT COUNT(*) FROM Users WHERE nome = @nome";
                    using (var checkCmd = new NpgsqlCommand(checkSql, conn))
                    {
                        checkCmd.Parameters.AddWithValue("nome", Username);
                        if ((long)checkCmd.ExecuteScalar() > 0)
                        {
                            MessageBox.Show("Este nome de usuário já existe.");
                            return;
                        }
                    }

                    var insertSql = "INSERT INTO Users (nome, senha) VALUES (@nome, @senha)";
                    using (var insertCmd = new NpgsqlCommand(insertSql, conn))
                    {
                        insertCmd.Parameters.AddWithValue("nome", Username);
                        insertCmd.Parameters.AddWithValue("senha", Password);
                        insertCmd.ExecuteNonQuery();
                        MessageBox.Show("Usuário registrado com sucesso! Por favor, faça o login.");
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Erro de banco de dados: {ex.Message}"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}