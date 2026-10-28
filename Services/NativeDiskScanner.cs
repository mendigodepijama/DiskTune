using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DiskTune.Models;
using Microsoft.Win32.SafeHandles;

namespace DiskTune.Services;

public class NativeDiskScanner
{
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool ReadFile(
        SafeFileHandle hFile,
        IntPtr lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        NativeOverlapped* lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool GetOverlappedResult(
        SafeFileHandle hFile,
        NativeOverlapped* lpOverlapped,
        out uint lpNumberOfBytesTransferred,
        bool bWait);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern unsafe bool CancelIoEx(SafeFileHandle hFile, NativeOverlapped* lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint WAIT_OBJECT_0 = 0x00000000;
    private const uint WAIT_TIMEOUT = 0x00000102;
    private const int ERROR_IO_PENDING = 997;

    public event EventHandler<ScanProgressEventArgs>? ProgressChanged;
    public event EventHandler<string>? ScanCompleted;

    private volatile bool _isCancelled = false;
    private volatile bool _isPaused = false;

    public void Cancel() => _isCancelled = true;
    public void Pause() => _isPaused = true;
    public void Resume() => _isPaused = false;

    public async Task RunScanAsync(DiskDriveInfo disk, int totalBlocks = 2500, int timeoutMs = 1500, bool quickScan = false)
    {
        _isCancelled = false;
        _isPaused = false;

        await Task.Run(() => DoScan(disk, totalBlocks, timeoutMs, quickScan));
    }

    private unsafe void DoScan(DiskDriveInfo disk, int totalBlocks, int timeoutMs, bool quickScan)
    {
        using SafeFileHandle handle = CreateFileW(
            disk.DeviceId,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_NO_BUFFERING | FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var err = Marshal.GetLastWin32Error();
            ScanCompleted?.Invoke(this, $"Erro ao abrir o disco: {new Win32Exception(err).Message} (Execute como Administrador).");
            return;
        }

        // Buffer alinhado por setor (64 KB para Quick, ou 256 KB para Normal)
        uint chunkSize = quickScan ? 64u * 1024u : 256u * 1024u;
        IntPtr buffer = IntPtr.Zero;
        IntPtr eventHandle = IntPtr.Zero;

        try
        {
            buffer = Marshal.AllocHGlobal((int)chunkSize);
            eventHandle = CreateEvent(IntPtr.Zero, true, false, null);

            long totalSize = disk.TotalSizeBytes;
            if (totalSize <= 0) totalSize = 500L * 1024 * 1024 * 1024; // fallback 500GB

            long stepBytes = totalSize / totalBlocks;
            // Alinha stepBytes para múltiplo de setor (4096 bytes)
            stepBytes = (stepBytes / 4096) * 4096;
            if (stepBytes < chunkSize) stepBytes = chunkSize;

            var stopwatch = Stopwatch.StartNew();
            long bytesProcessedForSpeed = 0;
            var speedTimer = Stopwatch.StartNew();
            double currentSpeedMBps = 0;

            int goodCount = 0;
            int slowCount = 0;
            int damagedCount = 0;

            for (int i = 0; i < totalBlocks; i++)
            {
                if (_isCancelled)
                {
                    ScanCompleted?.Invoke(this, "Verificação cancelada pelo usuário.");
                    return;
                }

                while (_isPaused && !_isCancelled)
                {
                    Thread.Sleep(100);
                }

                long currentOffset = i * stepBytes;
                if (currentOffset + chunkSize > totalSize)
                {
                    currentOffset = totalSize - chunkSize;
                }
                currentOffset = (currentOffset / 4096) * 4096; // Alinhamento estrito a 4096 bytes

                var overlapped = new NativeOverlapped
                {
                    OffsetLow = (int)(currentOffset & 0xFFFFFFFF),
                    OffsetHigh = (int)((currentOffset >> 32) & 0xFFFFFFFF),
                    EventHandle = eventHandle
                };

                var blockStopwatch = Stopwatch.StartNew();
                bool isSuccess = ReadFile(handle, buffer, chunkSize, out uint bytesRead, &overlapped);
                int lastError = Marshal.GetLastWin32Error();

                bool readCompleted = false;
                bool isTimeout = false;

                if (isSuccess)
                {
                    readCompleted = true;
                }
                else if (lastError == ERROR_IO_PENDING)
                {
                    uint waitRes = WaitForSingleObject(eventHandle, (uint)timeoutMs);
                    if (waitRes == WAIT_OBJECT_0)
                    {
                        if (GetOverlappedResult(handle, &overlapped, out bytesRead, false))
                        {
                            readCompleted = true;
                        }
                    }
                    else if (waitRes == WAIT_TIMEOUT)
                    {
                        isTimeout = true;
                        CancelIoEx(handle, &overlapped);
                    }
                }

                blockStopwatch.Stop();
                double latency = blockStopwatch.Elapsed.TotalMilliseconds;

                BlockHealth health;
                string? errorMsg = null;

                if (isTimeout)
                {
                    health = BlockHealth.Damaged;
                    errorMsg = $"Timeout de resposta (> {timeoutMs}ms)";
                    damagedCount++;
                }
                else if (!readCompleted)
                {
                    health = BlockHealth.Damaged;
                    errorMsg = $"Erro de E/S ({lastError})";
                    damagedCount++;
                }
                else if (latency < 40)
                {
                    health = BlockHealth.Good;
                    goodCount++;
                }
                else if (latency < 150)
                {
                    health = BlockHealth.Normal;
                    goodCount++;
                }
                else if (latency < 500)
                {
                    health = BlockHealth.Slow;
                    slowCount++;
                }
                else
                {
                    health = BlockHealth.Critical;
                    slowCount++;
                }

                bytesProcessedForSpeed += chunkSize;
                if (speedTimer.ElapsedMilliseconds >= 400)
                {
                    currentSpeedMBps = (bytesProcessedForSpeed / (1024.0 * 1024.0)) / speedTimer.Elapsed.TotalSeconds;
                    bytesProcessedForSpeed = 0;
                    speedTimer.Restart();
                }

                var blockResult = new ScanBlockResult
                {
                    BlockIndex = i,
                    OffsetBytes = currentOffset,
                    LatencyMs = latency,
                    Health = health,
                    ErrorMessage = errorMsg
                };

                double progressPercent = ((i + 1) / (double)totalBlocks) * 100.0;
                TimeSpan elapsed = stopwatch.Elapsed;
                TimeSpan eta = progressPercent > 0
                    ? TimeSpan.FromSeconds((elapsed.TotalSeconds / progressPercent) * (100.0 - progressPercent))
                    : TimeSpan.Zero;

                ProgressChanged?.Invoke(this, new ScanProgressEventArgs
                {
                    BlockResult = blockResult,
                    PercentComplete = progressPercent,
                    CurrentSpeedMBps = currentSpeedMBps,
                    ScannedBytes = currentOffset + chunkSize,
                    TotalBytes = totalSize,
                    ElapsedTime = elapsed,
                    EstimatedTimeRemaining = eta,
                    GoodCount = goodCount,
                    SlowCount = slowCount,
                    DamagedCount = damagedCount
                });
            }

            ScanCompleted?.Invoke(this, "Verificação concluída com sucesso!");
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
            if (eventHandle != IntPtr.Zero)
            {
                CloseHandle(eventHandle);
            }
        }
    }
}
