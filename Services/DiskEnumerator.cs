using System.Management;
using DiskTune.Models;

namespace DiskTune.Services;

public static class DiskEnumerator
{
    public static List<DiskDriveInfo> GetPhysicalDrives()
    {
        var list = new List<DiskDriveInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (ManagementObject drive in searcher.Get())
            {
                var index = Convert.ToInt32(drive["Index"] ?? -1);
                var deviceId = drive["DeviceID"]?.ToString() ?? $"\\\\.\\PhysicalDrive{index}";
                var model = drive["Model"]?.ToString()?.Trim() ?? "Desconhecido";
                var serial = drive["SerialNumber"]?.ToString()?.Trim() ?? "";
                var iface = drive["InterfaceType"]?.ToString() ?? "SATA";
                var size = Convert.ToInt64(drive["Size"] ?? 0);
                var media = drive["MediaType"]?.ToString() ?? "Fixed hard disk media";

                // Obter partições e letras de unidades associadas
                var partitionsList = new List<string>();
                try
                {
                    var partitionQuery = new ManagementObjectSearcher(
                        $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

                    foreach (ManagementObject partition in partitionQuery.Get())
                    {
                        var logicalQuery = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass = Win32_LogicalDiskToPartition");

                        foreach (ManagementObject logical in logicalQuery.Get())
                        {
                            var letter = logical["DeviceID"]?.ToString(); // e.g. "C:"
                            var volumeName = logical["VolumeName"]?.ToString();
                            if (!string.IsNullOrEmpty(letter))
                            {
                                partitionsList.Add(string.IsNullOrEmpty(volumeName) ? letter : $"{letter} ({volumeName})");
                            }
                        }
                    }
                }
                catch
                {
                    // Ignora falhas de associação em partições protegidas
                }

                list.Add(new DiskDriveInfo
                {
                    Index = index,
                    DeviceId = deviceId,
                    Model = model,
                    SerialNumber = serial,
                    InterfaceType = iface,
                    TotalSizeBytes = size,
                    MediaType = media,
                    AssociatedPartitions = partitionsList.Count > 0 ? string.Join(", ", partitionsList) : "Sem partição"
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao listar discos: {ex.Message}");
        }

        return list.OrderBy(d => d.Index).ToList();
    }
}
