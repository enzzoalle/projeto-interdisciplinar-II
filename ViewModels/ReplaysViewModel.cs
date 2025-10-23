using Microsoft.Extensions.Configuration;
using Npgsql;
using OpenCvSharp;
using ReplaysApp.Commands;
using ReplaysApp.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ReplaysApp.ViewModels
{
    public class ReplaysViewModel : INotifyPropertyChanged
    {
        private readonly User _usuarioLogado;
        private readonly string _pastaDosReplays;
        public bool isAdmin => _usuarioLogado?.isAdmin ?? false;

        public ObservableCollection<Replay> Replays { get; set; }

        private BitmapSource _imagemDaCamera;
        public BitmapSource ImagemDaCamera
        {
            get => _imagemDaCamera;
            set { _imagemDaCamera = value; OnPropertyChanged(); }
        }

        #region Propriedades do Playback
        private Replay _replaySelecionado;
        public Replay ReplaySelecionado
        {
            get => _replaySelecionado;
            set
            {
                _replaySelecionado = value;
                OnPropertyChanged();

                IsPlaying = false; 
                if (_replaySelecionado != null && File.Exists(_replaySelecionado.CaminhoArquivo))
                {
                    PlaybackSource = new Uri(_replaySelecionado.CaminhoArquivo);
                    IsPlaybackVisible = true;
                    IsPlaying = true;
                }
                else
                {
                    VoltarParaCamera();
                }
                OnPropertyChanged(nameof(PlaybackSource));
                OnPropertyChanged(nameof(IsPlaybackVisible));
            }
        }

        public Uri PlaybackSource { get; private set; }
        public bool IsPlaybackVisible { get; private set; } = false;

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayPauseButtonContent)); }
        }
        public string PlayPauseButtonContent => IsPlaying ? "Pause" : "Play";
        
        private TimeSpan _playbackProgress;
        public TimeSpan PlaybackProgress
        {
            get => _playbackProgress;
            set { _playbackProgress = value; OnPropertyChanged(); }
        }

        private TimeSpan _playbackDuration;
        public TimeSpan PlaybackDuration
        {
            get => _playbackDuration;
            set { _playbackDuration = value; OnPropertyChanged(); }
        }
        #endregion

        public ICommand SalvarReplayCommand { get; }
        public ICommand RenomearReplayCommand { get; }
        public ICommand CopiarReplayCommand { get; }
        public ICommand ExcluirReplayCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand VoltarParaCameraCommand { get; }


        public ReplaysViewModel(User usuarioLogado, Action onLogoutRequest)
        {
            _usuarioLogado = usuarioLogado ?? throw new ArgumentNullException(nameof(usuarioLogado));
            _pastaDosReplays = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ReplaysApp");
            Directory.CreateDirectory(_pastaDosReplays);

            Replays = new ObservableCollection<Replay>();
            
            SalvarReplayCommand = new AsyncRelayCommand<Queue<Mat>>(SalvarReplay);
            RenomearReplayCommand = new AsyncRelayCommand<Replay>(RenomearReplay);
            ExcluirReplayCommand = new AsyncRelayCommand<Replay>(ExcluirReplay);
            CopiarReplayCommand = new RelayCommand<Replay>(CopiarReplay);
            LogoutCommand = new RelayCommand<object>(_ => onLogoutRequest());
            PlayPauseCommand = new RelayCommand<object>(_ => IsPlaying = !IsPlaying);
            VoltarParaCameraCommand = new RelayCommand<object>(_ => VoltarParaCamera());

            if (isAdmin)
            {
                Task.Run(CarregarReplaysDoBanco);
            }
            else
            {
                return;
            }
        }

        private void VoltarParaCamera()
        {
            IsPlaybackVisible = false;
            PlaybackSource = null;
            IsPlaying = false;
            
            _replaySelecionado = null; 
            OnPropertyChanged(nameof(ReplaySelecionado));
            OnPropertyChanged(nameof(PlaybackSource));
            OnPropertyChanged(nameof(IsPlaybackVisible));
        }

        public async Task SalvarReplay(Queue<Mat> bufferDeFrames)
        {
            if (bufferDeFrames == null || bufferDeFrames.Count == 0)
            {
                MessageBox.Show("Buffer de gravação está vazio. Aguarde alguns segundos.");
                return;
            }

            var framesParaSalvar = new List<Mat>(bufferDeFrames);
            var agora = DateTime.Now;
            var nomeBaseArquivo = $"replay_{agora:yyyyMMdd_HHmmss}";
            var caminhoVideo = Path.Combine(_pastaDosReplays, $"{nomeBaseArquivo}.mp4");
            var caminhoThumbnail = Path.Combine(_pastaDosReplays, $"{nomeBaseArquivo}.jpg");
            const int FPS = 30;

            await Task.Run(() =>
            {
                var tamanhoDoFrame = new OpenCvSharp.Size(framesParaSalvar[0].Width, framesParaSalvar[0].Height);
                using (var writer = new VideoWriter(caminhoVideo, FourCC.FromString("avc1"), FPS, tamanhoDoFrame))
                {
                    foreach (var frame in framesParaSalvar)
                    {
                        writer.Write(frame);
                    }
                }
            });

            using (var videoCapture = new VideoCapture(caminhoVideo))
            using (var frameThumbnail = new Mat())
            {
                videoCapture.Read(frameThumbnail);
                if (!frameThumbnail.Empty())
                {
                    frameThumbnail.SaveImage(caminhoThumbnail, new ImageEncodingParam(ImwriteFlags.JpegQuality, 85));
                }
            }

            var novoReplay = new Replay
            {
                Nome = $"Replay de {agora:G}",
                CaminhoArquivo = caminhoVideo,
                DataGravacao = agora,
                CaminhoThumbnail = caminhoThumbnail
            };

            try
            {
                using (var conexao = new NpgsqlConnection(App.Configuration.GetConnectionString("DefaultConnection")))
                {
                    await conexao.OpenAsync();
                    var sql = "INSERT INTO Replays (nome, caminho_arquivo, data_gravacao, duracao_segundos, user_id, caminho_thumbnail) VALUES (@nome, @caminho, @data, @duracao, @userId, @thumbnail) RETURNING id";
                    using (var comando = new NpgsqlCommand(sql, conexao))
                    {
                        comando.Parameters.AddWithValue("nome", novoReplay.Nome);
                        comando.Parameters.AddWithValue("caminho", novoReplay.CaminhoArquivo);
                        comando.Parameters.AddWithValue("data", novoReplay.DataGravacao);
                        comando.Parameters.AddWithValue("duracao", framesParaSalvar.Count / FPS);
                        comando.Parameters.AddWithValue("userId", _usuarioLogado.Id);
                        comando.Parameters.AddWithValue("thumbnail", novoReplay.CaminhoThumbnail);
                        var novoId = await comando.ExecuteScalarAsync();
                        if (novoId != null) novoReplay.Id = Convert.ToInt32(novoId);
                    }
                }
                Application.Current.Dispatcher.Invoke(() => Replays.Add(novoReplay));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar no banco de dados: {ex.Message}");
            }
        }

        public async Task CarregarReplaysDoBanco()
        {
            var replaysCarregados = new List<Replay>();
            try
            {
                using (var conexao = new NpgsqlConnection(App.Configuration.GetConnectionString("DefaultConnection")))
                {
                    await conexao.OpenAsync();
                    var sql = "SELECT id, nome, caminho_arquivo, data_gravacao, caminho_thumbnail FROM Replays WHERE user_id = @userId ORDER BY data_gravacao DESC";
                    using (var comando = new NpgsqlCommand(sql, conexao))
                    {
                        comando.Parameters.AddWithValue("userId", _usuarioLogado.Id);
                        using (var reader = await comando.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                replaysCarregados.Add(new Replay
                                {
                                    Id = reader.GetInt32(0),
                                    Nome = reader.GetString(1),
                                    CaminhoArquivo = reader.GetString(2),
                                    DataGravacao = reader.GetDateTime(3),
                                    CaminhoThumbnail = reader.IsDBNull(4) ? null : reader.GetString(4)
                                });
                            }
                        }
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Replays.Clear();
                    foreach (var replay in replaysCarregados) Replays.Add(replay);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar histórico do banco: {ex.Message}");
            }
        }

        #region Lógica dos Botões da Lista

        private async Task RenomearReplay(Replay replayParaRenomear)
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
                        await conexao.OpenAsync();
                        var sql = "UPDATE Replays SET nome = @novoNome WHERE id = @replayId";
                        using (var comando = new NpgsqlCommand(sql, conexao))
                        {
                            comando.Parameters.AddWithValue("novoNome", novoNome);
                            comando.Parameters.AddWithValue("replayId", replayParaRenomear.Id);
                            await comando.ExecuteNonQueryAsync();
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

        private async Task ExcluirReplay(Replay replayParaExcluir)
        {
            if (replayParaExcluir == null) return;

            var resultado = MessageBox.Show($"Você tem certeza que deseja excluir o replay '{replayParaExcluir.Nome}'?", "Confirmar Exclusão", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (resultado == MessageBoxResult.No) return;

            try
            {
                using (var conexao = new NpgsqlConnection(App.Configuration.GetConnectionString("DefaultConnection")))
                {
                    await conexao.OpenAsync();
                    var sql = "DELETE FROM Replays WHERE id = @replayId";
                    using (var comando = new NpgsqlCommand(sql, conexao))
                    {
                        comando.Parameters.AddWithValue("replayId", replayParaExcluir.Id);
                        await comando.ExecuteNonQueryAsync();
                    }
                }
                
                if (File.Exists(replayParaExcluir.CaminhoArquivo))
                {
                    await Task.Run(() => File.Delete(replayParaExcluir.CaminhoArquivo));
                }
                
                Application.Current.Dispatcher.Invoke(() => Replays.Remove(replayParaExcluir));
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