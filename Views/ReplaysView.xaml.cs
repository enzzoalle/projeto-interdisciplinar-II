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
        private bool _isDragging;
        
        private ReplaysViewModel _viewModel => DataContext as ReplaysViewModel;
        private System.Windows.Window _ownerWindow;

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

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            _ownerWindow = System.Windows.Window.GetWindow(this);
            if (_ownerWindow != null)
            {
                _ownerWindow.PreviewKeyDown += Window_PreviewKeyDown;
            }

            this.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (_viewModel != null && _viewModel.IsCameraVisible)
                {
                    SalvarReplayBotao_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
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
            MediaPlayer.Position = TimeSpan.Zero;
            _viewModel?.VoltarParaCameraCommand.Execute(null);
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_viewModel != null && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                if (!_isDragging)
                    _viewModel.PlaybackProgress = MediaPlayer.Position;
            }
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;
            SeekToSliderValue();
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging) return;
            SeekToSliderValue();
        }

        private void SeekToSliderValue()
        {
            try
            {
                if (MediaPlayer == null || MediaPlayer.Source == null) return;
                if (!MediaPlayer.NaturalDuration.HasTimeSpan) return;

                double seconds = 0;
                if (this.FindName("ProgressSlider") is Slider progress)
                {
                    seconds = progress.Value;
                }
                else
                {
                    seconds = MediaPlayer.Position.TotalSeconds;
                }

                var max = MediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                if (double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0;
                if (seconds < 0) seconds = 0;
                if (seconds > max) seconds = max;

                MediaPlayer.Position = TimeSpan.FromSeconds(seconds);
                if (_viewModel != null)
                    _viewModel.PlaybackProgress = MediaPlayer.Position;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro ao fazer seek: " + ex.Message);
            }
        }
        #endregion

        #region Lógica do Atalho, Câmera e Botões
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Space)
            {
                if (_viewModel != null && _viewModel.IsCameraVisible)
                {
                    SalvarReplayBotao_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
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
            if (_ownerWindow != null)
            {
                _ownerWindow.PreviewKeyDown -= Window_PreviewKeyDown;
                _ownerWindow = null;
            }

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