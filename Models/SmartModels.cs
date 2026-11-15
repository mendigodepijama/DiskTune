namespace DiskTune.Models;

public enum SmartHealthStatus
{
    Good,
    Warning,
    Critical,
    Unknown
}

public class SmartAttribute
{
    public byte Id { get; set; }
    public string IdHex => $"0x{Id:X2}";
    public int IdDec => Id;
    public string Name { get; set; } = string.Empty;
    public int Current { get; set; }
    public int Worst { get; set; }
    public int Threshold { get; set; }
    public ulong RawValue { get; set; }
    public string RawFormatted { get; set; } = string.Empty;
    public SmartHealthStatus Status { get; set; } = SmartHealthStatus.Good;
    public string StatusText => Status switch
    {
        SmartHealthStatus.Good => "OK",
        SmartHealthStatus.Warning => "Atenção",
        SmartHealthStatus.Critical => "Falha",
        _ => "Desconhecido"
    };

    public string StatusColor => Status switch
    {
        SmartHealthStatus.Good => "#22C55E",      // Verde
        SmartHealthStatus.Warning => "#F59E0B",   // Amarelo / Laranja
        SmartHealthStatus.Critical => "#EF4444",  // Vermelho
        _ => "#94A3B8"
    };
}

public class DiskHealthSummary
{
    public bool IsSupported { get; set; } = true;
    public SmartHealthStatus OverallHealth { get; set; } = SmartHealthStatus.Good;
    public string HealthMessage { get; set; } = "Disco saudável e operando normalmente.";

    public int? Temperature { get; set; }
    public ulong? PowerOnHours { get; set; }
    public string PowerOnTimeFormatted { get; set; } = "N/D";
    public ulong? PowerCycleCount { get; set; }
    public ulong? ReallocatedSectors { get; set; }
    public ulong? PendingSectors { get; set; }
    public ulong? OfflineUncorrectable { get; set; }
    public ulong? CrcErrors { get; set; }

    public List<SmartAttribute> Attributes { get; set; } = new();

    public string OverallHealthColor => OverallHealth switch
    {
        SmartHealthStatus.Good => "#22C55E",
        SmartHealthStatus.Warning => "#F59E0B",
        SmartHealthStatus.Critical => "#EF4444",
        _ => "#94A3B8"
    };

    public string OverallHealthBadge => OverallHealth switch
    {
        SmartHealthStatus.Good => "SAUDÁVEL",
        SmartHealthStatus.Warning => "ATENÇÃO",
        SmartHealthStatus.Critical => "CRÍTICO",
        _ => "DESCONHECIDO"
    };
}
