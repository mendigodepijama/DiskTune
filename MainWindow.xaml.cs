using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using DiskTune.Models;
using DiskTune.Services;

namespace DiskTune;

public partial class MainWindow : Window
{
    private readonly NativeDiskScanner _scanner = new();
    private List<DiskDriveInfo> _disks = new();
    private bool _isScanning = false;

    // Sincronização e Throttle de UI (evita travamento da UI thread com 2500 dispatches)
    private readonly object _progressLock = new();
    private readonly List<ScanBlockResult> _pendingBlocks = new();
    private ScanProgressEventArgs? _latestProgress;
    private DispatcherTimer? _uiThrottleTimer;

    public MainWindow()
    {
        InitializeComponent();

        _scanner.ProgressChanged += Scanner_ProgressChanged;
        _scanner.ScanCompleted += Scanner_ScanCompleted;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        InitializeThrottle();
    }

    private void InitializeThrottle()
    {
        _uiThrottleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS para renderização visual suave sem congelamentos
        };
        _uiThrottleTimer.Tick += (s, e) => FlushProgressToUI();
        _uiThrottleTimer.Start();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshDiskList();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isScanning)
        {
            _scanner.Cancel();
        }

        _uiThrottleTimer?.Stop();

        // Desinscrição explícita de eventos para prevenir memory leaks
        _scanner.ProgressChanged -= Scanner_ProgressChanged;
        _scanner.ScanCompleted -= Scanner_ScanCompleted;
    }

    private void RefreshDiskList()
    {
        _disks = DiskEnumerator.GetPhysicalDrives();
        CmbDisks.ItemsSource = _disks;

        if (_disks.Count > 0)
        {
            // Tenta selecionar um disco secundário se houver, ou o primeiro
            var target = _disks.FirstOrDefault(d => d.AssociatedPartitions.Contains("G:")) ?? _disks[0];
            CmbDisks.SelectedItem = target;
        }
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (CmbDisks.SelectedItem is not DiskDriveInfo selectedDisk)
        {
            MessageBox.Show("Por favor, selecione um disco físico para verificar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isScanning = true;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        CmbDisks.IsEnabled = false;
        ChkQuickScan.IsEnabled = false;

        lock (_progressLock)
        {
            _pendingBlocks.Clear();
            _latestProgress = null;
        }

        CanvasGrid.ClearBlocks();
        PrgTotal.Value = 0;
        TxtGoodCount.Text = "0 (100%)";
        TxtDamagedCount.Text = "0";
        TxtStatus.Text = $"Verificando {selectedDisk.Model}...";

        int totalBlocks = CanvasGrid.TotalBlocks;
        bool quick = ChkQuickScan.IsChecked == true;

        await _scanner.RunScanAsync(selectedDisk, totalBlocks: totalBlocks, timeoutMs: 1500, quickScan: quick);
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        if (_isScanning)
        {
            _scanner.Cancel();
            TxtStatus.Text = "Interrompendo a verificação...";
        }
    }

    private void Scanner_ProgressChanged(object? sender, ScanProgressEventArgs e)
    {
        // Enfileiramento não-bloqueante na thread de background
        lock (_progressLock)
        {
            _pendingBlocks.Add(e.BlockResult);
            _latestProgress = e;
        }
    }

    private void FlushProgressToUI()
    {
        List<ScanBlockResult> blocksToDraw;
        ScanProgressEventArgs? progress;

        lock (_progressLock)
        {
            if (_pendingBlocks.Count == 0 && _latestProgress == null) return;

            blocksToDraw = new List<ScanBlockResult>(_pendingBlocks);
            _pendingBlocks.Clear();

            progress = _latestProgress;
            _latestProgress = null;
        }

        // 1. Desenha em lote todos os blocos pendentes no canvas
        foreach (var block in blocksToDraw)
        {
            CanvasGrid.UpdateBlock(block.BlockIndex, block.Health);
        }

        // 2. Atualiza os indicadores de texto com o estado mais recente
        if (progress != null)
        {
            TxtSpeed.Text = $"{progress.CurrentSpeedMBps:F1} MB/s";
            TxtPosition.Text = $"{DiskDriveInfo.FormatBytes(progress.ScannedBytes)} / {DiskDriveInfo.FormatBytes(progress.TotalBytes)}";
            TxtTime.Text = $"{progress.ElapsedTime:mm\\:ss} / {(progress.EstimatedTimeRemaining.TotalHours < 24 ? progress.EstimatedTimeRemaining.ToString(@"mm\:ss") : "--:--")}";

            int totalProcessed = progress.GoodCount + progress.SlowCount + progress.DamagedCount;
            double healthPercent = totalProcessed > 0 ? (progress.GoodCount / (double)totalProcessed) * 100.0 : 100.0;
            TxtGoodCount.Text = $"{progress.GoodCount} ({healthPercent:F1}%)";
            TxtDamagedCount.Text = $"{progress.DamagedCount}";

            PrgTotal.Value = progress.PercentComplete;
            TxtStatus.Text = $"Escaneando bloco {progress.BlockResult.BlockIndex + 1} de {CanvasGrid.TotalBlocks} ({progress.PercentComplete:F1}%)";
        }
    }

    private void Scanner_ScanCompleted(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            FlushProgressToUI();
            _isScanning = false;
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            CmbDisks.IsEnabled = true;
            ChkQuickScan.IsEnabled = true;
            TxtStatus.Text = message;
        });
    }
}
