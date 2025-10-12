using Microsoft.Extensions.Configuration;
using Npgsql;
using OpenCvSharp;
using ReplaysApp.Commands;
using ReplaysApp.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ReplaysApp.ViewModels
{
    public class ReplaysViewModel : INotifyPropertyChanged
    {
        private readonly User _usuarioLogado;
        private readonly string _pastaDosReplays;

        public ObservableCollection<Replay> Replays { get; set; }
        private BitmapSource _imagemDaCamera;
        public BitmapSource ImagemDaCamera
        {
            get => _imagemDaCamera;
            set { _imagemDaCamera = value; OnPropertyChanged(); }
        }

        public ICommand SalvarReplayCommand { get; }
        public ICommand RenomearReplayCommand { get; }
        public ICommand CopiarReplayCommand { get; }
        public ICommand ExcluirReplayCommand { get; }

        public ReplaysViewModel(User usuarioLogado)
        {
            _usuarioLogado = usuarioLogado ?? throw new ArgumentNullException(nameof(usuarioLogado));
            _pastaDosReplays = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ReplaysApp");
            Directory.CreateDirectory(_pastaDosReplays);

            Replays = new ObservableCollection<Replay>();
            CarregarReplaysDoBanco();

            SalvarReplayCommand = new RelayCommand<Queue<Mat>>(SalvarReplay);
            RenomearReplayCommand = new RelayCommand<Replay>(RenomearReplay);
            CopiarReplayCommand = new RelayCommand<Replay>(CopiarReplay);
            ExcluirReplayCommand = new RelayCommand<Replay>(ExcluirReplay);
        }

        public void SalvarReplay(Queue<Mat> bufferDeFrames)
        {
            if (bufferDeFrames == null || bufferDeFrames.Count == 0)
            {
                MessageBox.Show("Buffer de gravação está vazio. Aguarde alguns segundos.");
                return;
            }

            var framesParaSalvar = new List<Mat>(bufferDeFrames);
            var agora = DateTime.Now;
            var nomeArquivo = $"replay_{agora:yyyyMMdd_HHmmss}.mp4";
            var caminhoCompleto = Path.Combine(_pastaDosReplays, nomeArquivo);
            const int FPS = 30;

            var tamanhoDoFrame = new OpenCvSharp.Size(framesParaSalvar[0].Width, framesParaSalvar[0].Height);
            using (var writer = new VideoWriter(caminhoCompleto, FourCC.FromString("avc1"), FPS, tamanhoDoFrame))
            {
                foreach (var frame in framesParaSalvar)
                {
                    writer.Write(frame);
                }
            }

            var novoReplay = new Replay
            {
                Nome = $"Replay de {agora:G}",
                CaminhoArquivo = caminhoCompleto,
                DataGravacao = agora
            };

            try
            {
                using (var conexao = new NpgsqlConnection(App.Configuration.GetConnectionString("DefaultConnection")))
                {
                    conexao.Open();
                    var sql = "INSERT INTO Replays (nome, caminho_arquivo, data_gravacao, duracao_segundos, user_id) VALUES (@nome, @caminho, @data, @duracao, @userId) RETURNING id";
                    using (var comando = new NpgsqlCommand(sql, conexao))
                    {
                        comando.Parameters.AddWithValue("nome", novoReplay.Nome);
                        comando.Parameters.AddWithValue("caminho", novoReplay.CaminhoArquivo);
                        comando.Parameters.AddWithValue("data", novoReplay.DataGravacao);
                        comando.Parameters.AddWithValue("duracao", framesParaSalvar.Count / FPS);
                        comando.Parameters.AddWithValue("userId", _usuarioLogado.Id);
                        var novoId = comando.ExecuteScalar();
                        if (novoId != null) novoReplay.Id = Convert.ToInt32(novoId);
                    }
                }
                Replays.Add(novoReplay);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar no banco de dados: {ex.Message}");
            }
        }

        public void CarregarReplaysDoBanco()
        {
            Replays.Clear();
            try
            {
                using (var conexao = new NpgsqlConnection(App.Configuration.GetConnectionString("DefaultConnection")))
                {
                    conexao.Open();
                    var sql = "SELECT id, nome, caminho_arquivo, data_gravacao FROM Replays WHERE user_id = @userId ORDER BY data_gravacao DESC";
                    using (var comando = new NpgsqlCommand(sql, conexao))
                    {
                        comando.Parameters.AddWithValue("userId", _usuarioLogado.Id);
                        using (var reader = comando.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var replay = new Replay
                                {
                                    Id = reader.GetInt32(0),
                                    Nome = reader.GetString(1),
                                    CaminhoArquivo = reader.GetString(2),
                                    DataGravacao = reader.GetDateTime(3)
                                };
                                Replays.Add(replay);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar histórico do banco: {ex.Message}");
            }
        }

        #region Lógica dos Botões da Lista

        private void RenomearReplay(Replay replayParaRenomear)
        {
            if (replayParaRenomear == null) return;

            var inputBox = new Views.InputBoxView("Digite o novo nome para o replay:", replayParaRenomear.Nome);

            if (inputBox.ShowDialog() == true)
            {
                string novoNome = inputBox.ResponseText;

                try
                {
                    using (var conexao = new NpgsqlConnection(App.Configuration.GetConnectionString("DefaultConnection")))
                    {
                        conexao.Open();
                        var sql = "UPDATE Replays SET nome = @novoNome WHERE id = @replayId";
                        using (var comando = new NpgsqlCommand(sql, conexao))
                        {
                            comando.Parameters.AddWithValue("novoNome", novoNome);
                            comando.Parameters.AddWithValue("replayId", replayParaRenomear.Id);
                            comando.ExecuteNonQuery();
                        }
                    }
                    replayParaRenomear.Nome = novoNome;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao renomear o replay: {ex.Message}");
                }
            }
        }

        private void CopiarReplay(Replay replayParaCopiar)
        {
            if (replayParaCopiar == null) return;

            try
            {
                string pastaDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                string nomeArquivo = $"{replayParaCopiar.Nome}.mp4";

                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    nomeArquivo = nomeArquivo.Replace(c.ToString(), "");
                }

                string caminhoDestino = Path.Combine(pastaDownloads, nomeArquivo);

                File.Copy(replayParaCopiar.CaminhoArquivo, caminhoDestino, true);
                MessageBox.Show($"Replay salvo com sucesso em sua pasta de Downloads!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao copiar o arquivo: {ex.Message}");
            }
        }

        private void ExcluirReplay(Replay replayParaExcluir)
        {
            if (replayParaExcluir == null) return;

            var resultado = MessageBox.Show(
                $"Você tem certeza que deseja excluir o replay '{replayParaExcluir.Nome}'?\n\nEsta ação não pode ser desfeita.",
                "Confirmar Exclusão",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resultado == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                using (var conexao = new NpgsqlConnection(App.Configuration.GetConnectionString("DefaultConnection")))
                {
                    conexao.Open();
                    var sql = "DELETE FROM Replays WHERE id = @replayId";
                    using (var comando = new NpgsqlCommand(sql, conexao))
                    {
                        comando.Parameters.AddWithValue("replayId", replayParaExcluir.Id);
                        comando.ExecuteNonQuery();
                    }
                }

                if (File.Exists(replayParaExcluir.CaminhoArquivo))
                {
                    File.Delete(replayParaExcluir.CaminhoArquivo);
                }

                Replays.Remove(replayParaExcluir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro ao excluir o replay: {ex.Message}");
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}