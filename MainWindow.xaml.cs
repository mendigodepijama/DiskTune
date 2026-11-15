using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    private void RefreshDiskList(string? preferredDeviceId = null)
    {
        try
        {
            _disks = DiskEnumerator.GetPhysicalDrives();
            CmbDisks.ItemsSource = _disks;

            if (_disks.Count > 0)
            {
                // Tenta manter o disco que já estava selecionado
                DiskDriveInfo? target = null;
                if (!string.IsNullOrEmpty(preferredDeviceId))
                {
                    target = _disks.FirstOrDefault(d => d.DeviceId == preferredDeviceId);
                }

                // Fallback: se não encontrar, tenta selecionar um disco secundário com G: ou o primeiro
                if (target == null)
                {
                    target = _disks.FirstOrDefault(d => d.AssociatedPartitions.Contains("G:")) ?? _disks[0];
                }

                CmbDisks.SelectedItem = target;
                UpdateDiskTabs(target);
            }
            else
            {
                TxtStatus.Text = "Nenhum disco físico encontrado no sistema.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao listar discos: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_isScanning)
        {
            MessageBox.Show("Aguarde ou pare a verificação de superfície atual antes de recarregar os discos.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string? currentId = (CmbDisks.SelectedItem as DiskDriveInfo)?.DeviceId;
        RefreshDiskList(currentId);
        TxtStatus.Text = "Lista de discos atualizada com sucesso.";
    }

    private void CmbDisks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbDisks.SelectedItem is DiskDriveInfo selectedDisk)
        {
            UpdateDiskTabs(selectedDisk);
        }
    }

    private void UpdateDiskTabs(DiskDriveInfo disk)
    {
        UpdateHealthTab(disk);
        UpdateInfoTab(disk);
    }

    private void UpdateHealthTab(DiskDriveInfo disk)
    {
        var health = disk.HealthSummary;

        if (health == null || !health.IsSupported)
        {
            TxtHealthBadge.Text = "NÃO SUPORTADO";
            BdrHealthBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            TxtHealthMessage.Text = health?.HealthMessage ?? "Dados S.M.A.R.T. não disponíveis para este dispositivo.";

            TxtHealthTemp.Text = "--";
            TxtHealthPowerHours.Text = "--";
            TxtHealthPowerCycles.Text = "--";
            TxtHealthReallocated.Text = "--";
            TxtHealthCrc.Text = "--";

            GridSmartAttributes.ItemsSource = null;
            return;
        }

        // 1. Banner Geral de Saúde
        TxtHealthBadge.Text = health.OverallHealthBadge;
        BdrHealthBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(health.OverallHealthColor));
        TxtHealthMessage.Text = health.HealthMessage;

        // 2. Cards de Métricas Rápidas
        TxtHealthTemp.Text = health.Temperature.HasValue ? $"{health.Temperature.Value} °C" : "-- °C";
        TxtHealthPowerHours.Text = health.PowerOnTimeFormatted;
        TxtHealthPowerCycles.Text = health.PowerCycleCount.HasValue
            ? string.Format(CultureInfo.CurrentCulture, "{0:N0} vezes", health.PowerCycleCount.Value)
            : "--";

        // Setores Realocados
        if (health.ReallocatedSectors.HasValue)
        {
            ulong val = health.ReallocatedSectors.Value;
            TxtHealthReallocated.Text = string.Format(CultureInfo.CurrentCulture, "{0:N0} setores", val);
            TxtHealthReallocated.Foreground = val > 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
        }
        else
        {
            TxtHealthReallocated.Text = "0 setores";
            TxtHealthReallocated.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
        }

        // Erros de CRC de cabo
        if (health.CrcErrors.HasValue)
        {
            ulong crcVal = health.CrcErrors.Value;
            TxtHealthCrc.Text = string.Format(CultureInfo.CurrentCulture, "{0:N0} erros", crcVal);
            TxtHealthCrc.Foreground = crcVal > 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
        }
        else
        {
            TxtHealthCrc.Text = "0 erros";
            TxtHealthCrc.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
        }

        // 3. Tabela Completa de Atributos
        GridSmartAttributes.ItemsSource = health.Attributes;
    }

    private void UpdateInfoTab(DiskDriveInfo disk)
    {
        // Seção 1: Identificação
        TxtInfoModel.Text = string.IsNullOrWhiteSpace(disk.Model) ? "Desconhecido" : disk.Model;
        TxtInfoSerial.Text = string.IsNullOrWhiteSpace(disk.SerialNumber) ? "N/D" : disk.SerialNumber;
        TxtInfoFirmware.Text = string.IsNullOrWhiteSpace(disk.FirmwareRevision) ? "N/D" : disk.FirmwareRevision;
        TxtInfoDeviceId.Text = disk.DeviceId;
        TxtInfoStatus.Text = disk.Status;
        TxtInfoPnp.Text = string.IsNullOrWhiteSpace(disk.PnpDeviceId) ? "N/D" : disk.PnpDeviceId;

        // Seção 2: Interface e Armazenamento
        TxtInfoBusType.Text = disk.BusType;
        TxtInfoMediaType.Text = disk.MediaType;
        TxtInfoCapacity.Text = DiskDriveInfo.FormatBytes(disk.TotalSizeBytes);
        TxtInfoTotalBytes.Text = string.Format(CultureInfo.CurrentCulture, "{0:N0} Bytes", disk.TotalSizeBytes);
        TxtInfoSectors.Text = string.Format(CultureInfo.CurrentCulture, "{0:N0} setores", disk.TotalSectors);
        TxtInfoSectorSize.Text = $"{disk.BytesPerSector} Bytes / setor";

        // Seção 3: Geometria
        TxtInfoCylinders.Text = disk.TotalCylinders > 0 ? string.Format(CultureInfo.CurrentCulture, "{0:N0}", disk.TotalCylinders) : "N/D";
        TxtInfoHeads.Text = disk.TotalHeads > 0 ? string.Format(CultureInfo.CurrentCulture, "{0:N0}", disk.TotalHeads) : "N/D";
        TxtInfoTracksPerCyl.Text = disk.TracksPerCylinder > 0 ? string.Format(CultureInfo.CurrentCulture, "{0:N0}", disk.TracksPerCylinder) : "N/D";
        TxtInfoSectorsPerTrack.Text = disk.SectorsPerTrack > 0 ? string.Format(CultureInfo.CurrentCulture, "{0:N0}", disk.SectorsPerTrack) : "N/D";

        // Seção 4: Partições e Volumes
        LstPartitions.ItemsSource = disk.PartitionsDetails;
        TxtNoPartitions.Visibility = disk.PartitionsDetails.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (CmbDisks.SelectedItem is not DiskDriveInfo selectedDisk)
        {
            MessageBox.Show("Por favor, selecione um disco físico para verificar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Garante que a aba de verificação fique em primeiro plano ao iniciar
        MainTabControl.SelectedIndex = 0;

        _isScanning = true;
        BtnStart.IsEnabled = false;
        BtnStop.IsEnabled = true;
        BtnRefresh.IsEnabled = false;
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
            BtnRefresh.IsEnabled = true;
            CmbDisks.IsEnabled = true;
            ChkQuickScan.IsEnabled = true;
            TxtStatus.Text = message;
        });
    }
}
