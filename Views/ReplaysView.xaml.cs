using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using ReplaysApp.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ReplaysApp.Views
{
    public partial class ReplaysView : UserControl
    {
        private const int FPS = 30;
        private const int BUFFER_SEGUNDOS = 30;
        private readonly int _tamanhoMaximoBuffer = FPS * BUFFER_SEGUNDOS;

        private VideoCapture _captura;
        private Mat _frame;
        private DispatcherTimer _timer;
        private Queue<Mat> _bufferDeFrames;
        
        private ReplaysViewModel _viewModel => DataContext as ReplaysViewModel;

        public ReplaysView()
        {
            InitializeComponent();
            Loaded += JanelaCarregada;
            Unloaded += JanelaDescarregada;
        }
        
        private void JanelaCarregada(object sender, RoutedEventArgs e)
        {
            _captura = new VideoCapture(0);
            if (!_captura.IsOpened()) { MessageBox.Show("Câmera não encontrada."); return; }

            _frame = new Mat();
            _bufferDeFrames = new Queue<Mat>(_tamanhoMaximoBuffer);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000 / FPS) };
            _timer.Tick += AtualizarFrame;
            _timer.Start();
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
            _timer?.Stop();
            _captura?.Release();
            _captura?.Dispose();
            
            if (_bufferDeFrames != null)
            {
                foreach (var frame in _bufferDeFrames) { frame.Dispose(); }
                _bufferDeFrames.Clear();
            }
            _frame?.Dispose();
        }
    }
}