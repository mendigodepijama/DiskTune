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
            // Mapeia detalhes físicos de MSFT_PhysicalDisk (BusType real como SATA, NVMe, USB, SSD)
            var storageMap = GetStoragePhysicalDiskMap();

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (ManagementObject drive in searcher.Get())
            {
                var index = Convert.ToInt32(drive["Index"] ?? -1);
                var deviceId = drive["DeviceID"]?.ToString() ?? $"\\\\.\\PhysicalDrive{index}";
                var model = drive["Model"]?.ToString()?.Trim() ?? "Desconhecido";
                var serial = drive["SerialNumber"]?.ToString()?.Trim() ?? "";
                var firmware = drive["FirmwareRevision"]?.ToString()?.Trim() ?? "";
                var iface = drive["InterfaceType"]?.ToString() ?? "SATA";
                var size = Convert.ToInt64(drive["Size"] ?? 0);
                var media = drive["MediaType"]?.ToString() ?? "Fixed hard disk media";
                var pnpId = drive["PNPDeviceID"]?.ToString() ?? "";
                var status = drive["Status"]?.ToString() ?? "OK";

                // Geometria
                uint bytesPerSector = Convert.ToUInt32(drive["BytesPerSector"] ?? 512);
                uint sectorsPerTrack = Convert.ToUInt32(drive["SectorsPerTrack"] ?? 0);
                uint tracksPerCyl = Convert.ToUInt32(drive["TracksPerCylinder"] ?? 0);
                ulong totalCyl = Convert.ToUInt64(drive["TotalCylinders"] ?? 0);
                uint totalHeads = Convert.ToUInt32(drive["TotalHeads"] ?? 0);
                ulong totalSectors = Convert.ToUInt64(drive["TotalSectors"] ?? 0);
                ulong totalTracks = Convert.ToUInt64(drive["TotalTracks"] ?? 0);
                uint partitionsCount = Convert.ToUInt32(drive["Partitions"] ?? 0);

                // Partições e volumes detalhados
                var partitionsList = new List<string>();
                var partitionDetails = new List<DiskPartitionDetail>();

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
                            var letter = logical["DeviceID"]?.ToString() ?? ""; // ex: "C:"
                            var volumeName = logical["VolumeName"]?.ToString() ?? "";
                            var fileSystem = logical["FileSystem"]?.ToString() ?? "";
                            var totalBytes = Convert.ToInt64(logical["Size"] ?? 0);
                            var freeBytes = Convert.ToInt64(logical["FreeSpace"] ?? 0);

                            if (!string.IsNullOrEmpty(letter))
                            {
                                string label = string.IsNullOrEmpty(volumeName) ? letter : $"{letter} ({volumeName})";
                                partitionsList.Add(label);

                                partitionDetails.Add(new DiskPartitionDetail
                                {
                                    DriveLetter = letter,
                                    VolumeName = volumeName,
                                    FileSystem = fileSystem,
                                    TotalBytes = totalBytes,
                                    FreeBytes = freeBytes
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Ignora falhas em partições protegidas
                }

                // Ajusta BusType e MediaType com base no MSFT_PhysicalDisk se disponível
                string busType = iface;
                string mediaType = media;
                uint spindleSpeed = 0;

                if (storageMap.TryGetValue(index.ToString(), out var storageInfo))
                {
                    if (!string.IsNullOrEmpty(storageInfo.BusType)) busType = storageInfo.BusType;
                    if (!string.IsNullOrEmpty(storageInfo.MediaType)) mediaType = storageInfo.MediaType;
                    spindleSpeed = storageInfo.SpindleSpeed;
                }

                var diskInfo = new DiskDriveInfo
                {
                    Index = index,
                    DeviceId = deviceId,
                    Model = model,
                    SerialNumber = serial,
                    FirmwareRevision = firmware,
                    InterfaceType = iface,
                    BusType = busType,
                    TotalSizeBytes = size,
                    MediaType = mediaType,
                    PnpDeviceId = pnpId,
                    Status = status,
                    BytesPerSector = bytesPerSector,
                    SectorsPerTrack = sectorsPerTrack,
                    TracksPerCylinder = tracksPerCyl,
                    TotalCylinders = totalCyl,
                    TotalHeads = totalHeads,
                    TotalSectors = totalSectors,
                    TotalTracks = totalTracks,
                    PartitionsCount = partitionsCount,
                    SpindleSpeedRpm = spindleSpeed,
                    AssociatedPartitions = partitionsList.Count > 0 ? string.Join(", ", partitionsList) : "Sem partição",
                    PartitionsDetails = partitionDetails
                };

                // Carrega dados de Saúde e S.M.A.R.T.
                diskInfo.HealthSummary = SmartReader.ReadDiskHealth(diskInfo);

                list.Add(diskInfo);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao listar discos: {ex.Message}");
        }

        return list.OrderBy(d => d.Index).ToList();
    }

    private class StorageDiskExtra
    {
        public string BusType { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public uint SpindleSpeed { get; set; }
    }

    private static Dictionary<string, StorageDiskExtra> GetStoragePhysicalDiskMap()
    {
        var map = new Dictionary<string, StorageDiskExtra>();
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT * FROM MSFT_PhysicalDisk");
            foreach (ManagementObject disk in searcher.Get())
            {
                var deviceId = disk["DeviceId"]?.ToString() ?? "";
                var busTypeRaw = Convert.ToUInt32(disk["BusType"] ?? 0);
                var mediaTypeRaw = Convert.ToUInt32(disk["MediaType"] ?? 0);
                var spindleSpeed = Convert.ToUInt32(disk["SpindleSpeed"] ?? 0);

                string busType = busTypeRaw switch
                {
                    11 => "SATA",
                    17 => "NVMe",
                    7 => "USB",
                    8 => "RAID",
                    3 => "ATAPI",
                    12 => "SAS",
                    _ => "SATA"
                };

                string mediaType = mediaTypeRaw switch
                {
                    3 => "HDD (Disco Mecânico)",
                    4 => "SSD (Estado Sólido)",
                    5 => "SCM",
                    _ => "Disco Rígido"
                };

                map[deviceId] = new StorageDiskExtra
                {
                    BusType = busType,
                    MediaType = mediaType,
                    SpindleSpeed = spindleSpeed
                };
            }
        }
        catch
        {
            // Ignora se o namespace storage não estiver acessível
        }
        return map;
    }
}
