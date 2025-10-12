using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ReplaysApp.Models
{
    public class Replay : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _nome;
        public string Nome
        {
            get => _nome;
            set { _nome = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayInfo)); }
        }

        public string CaminhoArquivo { get; set; }
        public DateTime DataGravacao { get; set; }

        public string DisplayInfo => $"{Nome} - {DataGravacao:dd/MM/yyyy HH:mm:ss}";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}