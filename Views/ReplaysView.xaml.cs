using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using ReplaysApp.Models;
using ReplaysApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ReplaysApp.Views
{
    public partial class ReplaysView : UserControl
    {
        private const int FPS = 30;
        private const int BUFFER_SEGUNDOS = 30;
        private readonly int _tamanhoMaximoBuffer;

        private VideoCapture _captura;
        private Mat _frame;
        private DispatcherTimer _cameraTimer;
        private Queue<Mat> _bufferDeFrames;
        
        private DispatcherTimer _playbackTimer;
        
        private ReplaysViewModel _viewModel => DataContext as ReplaysViewModel;

        public ReplaysView()
        {
            InitializeComponent();
            Loaded += JanelaCarregada;
            Unloaded += JanelaDescarregada;
            
            Focusable = true;
            _tamanhoMaximoBuffer = FPS * BUFFER_SEGUNDOS;

            _playbackTimer = new DispatcherTimer();
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(100);
            _playbackTimer.Tick += PlaybackTimer_Tick;
        }

        private void JanelaCarregada(object sender, RoutedEventArgs e)
        {
            _captura = new VideoCapture(0);
            if (!_captura.IsOpened()) { MessageBox.Show("Câmera não encontrada."); return; }

            _frame = new Mat();
            _bufferDeFrames = new Queue<Mat>(_tamanhoMaximoBuffer);
            _cameraTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000 / FPS) };
            _cameraTimer.Tick += AtualizarFrame;
            _cameraTimer.Start();

            // Adiciona um listener para a propriedade IsPlaying no ViewModel
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            this.Focus();
        }

        // Este método observa as mudanças no ViewModel
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Se a propriedade IsPlaying mudou, nós damos o comando Play/Pause no MediaElement
            if (e.PropertyName == nameof(ReplaysViewModel.IsPlaying))
            {
                if (_viewModel.IsPlaying)
                {
                    MediaPlayer.Play();
                    _playbackTimer.Start();
                }
                else
                {
                    MediaPlayer.Pause();
                    _playbackTimer.Stop();
                }
            }
        }
        
        #region Lógica do Player de Vídeo
        private void MediaPlayer_OnMediaOpened(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan && _viewModel != null)
            {
                _viewModel.PlaybackDuration = MediaPlayer.NaturalDuration.TimeSpan;
            }
        }

        private void MediaPlayer_OnMediaEnded(object sender, RoutedEventArgs e)
        {
            _playbackTimer.Stop();
            // Reseta a posição para o início
            MediaPlayer.Position = TimeSpan.Zero;
            _viewModel?.VoltarParaCameraCommand.Execute(null);
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_viewModel != null && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                _viewModel.PlaybackProgress = MediaPlayer.Position;
            }
        }

        // Permite que o usuário arraste a barra de progresso
        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (sender is Slider slider)
            {
                MediaPlayer.Position = TimeSpan.FromSeconds(slider.Value);
                _viewModel.PlaybackProgress = MediaPlayer.Position;
            }
        }
        #endregion

        #region Lógica do Atalho, Câmera e Botões
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Space)
            {
                SalvarReplayBotao_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void AtualizarFrame(object sender, EventArgs e)
        {
            if (_captura == null || !_captura.IsOpened()) return;
            
            _captura.Read(_frame);
            if (_frame.Empty()) return;

            _bufferDeFrames.Enqueue(_frame.Clone());
            if (_bufferDeFrames.Count > _tamanhoMaximoBuffer)
            {
                _bufferDeFrames.Dequeue().Dispose();
            }

            if (_viewModel != null)
                _viewModel.ImagemDaCamera = BitmapSourceConverter.ToBitmapSource(_frame);
        }

        private void SalvarReplayBotao_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.SalvarReplayCommand.CanExecute(_bufferDeFrames) ?? false)
            {
                _viewModel.SalvarReplayCommand.Execute(_bufferDeFrames);
            }
        }
        
        private void JanelaDescarregada(object sender, RoutedEventArgs e)
        {
            _cameraTimer?.Stop();
            _playbackTimer?.Stop();
            _captura?.Release();
            _captura?.Dispose();
            
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (_bufferDeFrames != null)
            {
                foreach (var frame in _bufferDeFrames) { frame.Dispose(); }
                _bufferDeFrames.Clear();
            }
            _frame?.Dispose();
        }
        #endregion
    }
}