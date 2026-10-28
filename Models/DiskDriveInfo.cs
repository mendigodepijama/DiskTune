using System.Globalization;

namespace DiskTune.Models;

public enum BlockHealth
{
    Pending,
    Good,       // < 50ms
    Normal,     // 50ms - 150ms
    Slow,       // 150ms - 500ms
    Critical,   // 500ms - 2000ms
    Damaged     // Error or Timeout > 2000ms
}

public class DiskDriveInfo
{
    public int Index { get; set; }
    public string DeviceId { get; set; } = string.Empty; // e.g. \\.\PhysicalDrive1
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string InterfaceType { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string AssociatedPartitions { get; set; } = string.Empty;

    public string DisplayName =>
        $"Disco {Index}: {Model} ({FormatBytes(TotalSizeBytes)}) [{AssociatedPartitions}]";

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 GB";
        double gb = bytes / (1024.0 * 1024 * 1024);
        if (gb >= 1000)
            return string.Format(CultureInfo.InvariantCulture, "{0:F2} TB", gb / 1024.0);
        return string.Format(CultureInfo.InvariantCulture, "{0:F1} GB", gb);
    }
}

public class ScanBlockResult
{
    public int BlockIndex { get; set; }
    public long OffsetBytes { get; set; }
    public double LatencyMs { get; set; }
    public BlockHealth Health { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ScanProgressEventArgs : EventArgs
{
    public ScanBlockResult BlockResult { get; set; } = null!;
    public double PercentComplete { get; set; }
    public double CurrentSpeedMBps { get; set; }
    public long ScannedBytes { get; set; }
    public long TotalBytes { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
    public int GoodCount { get; set; }
    public int SlowCount { get; set; }
    public int DamagedCount { get; set; }
}
