namespace DiskTune.Models;

public class DiskPartitionDetail
{
    public string DriveLetter { get; set; } = string.Empty; // ex: "C:"
    public string VolumeName { get; set; } = string.Empty;  // ex: "Windows"
    public string FileSystem { get; set; } = string.Empty;  // ex: "NTFS"
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes => Math.Max(0, TotalBytes - FreeBytes);

    public double PercentUsed => TotalBytes > 0
        ? Math.Min(100.0, Math.Max(0.0, ((double)UsedBytes / TotalBytes) * 100.0))
        : 0;

    public string DisplayLabel => string.IsNullOrWhiteSpace(VolumeName)
        ? DriveLetter
        : $"{DriveLetter} [{VolumeName}]";

    public string FreeSizeFormatted => DiskDriveInfo.FormatBytes(FreeBytes);
    public string TotalSizeFormatted => DiskDriveInfo.FormatBytes(TotalBytes);
    public string UsedSizeFormatted => DiskDriveInfo.FormatBytes(UsedBytes);

    public string SummaryText => $"{FreeSizeFormatted} livres de {TotalSizeFormatted} ({PercentUsed:F1}% ocupado)";
}
