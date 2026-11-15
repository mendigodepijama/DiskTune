using System.Globalization;
using System.Management;
using DiskTune.Models;

namespace DiskTune.Services;

public static class SmartReader
{
    private static readonly Dictionary<byte, string> KnownAttributeNames = new()
    {
        { 0x01, "Raw Read Error Rate (Erros de Leitura)" },
        { 0x02, "Throughput Performance (Desempenho)" },
        { 0x03, "Spin Up Time (Tempo de Aceleração do Motor)" },
        { 0x04, "Start/Stop Count (Contagem de Início/Parada)" },
        { 0x05, "Reallocated Sector Count (Setores Realocados)" },
        { 0x07, "Seek Error Rate (Taxa de Erros de Busca)" },
        { 0x08, "Seek Time Performance (Desempenho de Busca)" },
        { 0x09, "Power-On Hours (Horas Ligado / Tempo de Uso)" },
        { 0x0A, "Spin Retry Count (Tentativas de Giro)" },
        { 0x0B, "Calibration Retry Count (Tentativas de Recalibração)" },
        { 0x0C, "Power Cycle Count (Ciclos de Inicialização)" },
        { 0x0D, "Soft Read Error Rate" },
        { 0xAA, "Available Reserved Space (Espaço Reservado SSD)" },
        { 0xAB, "Program Fail Count (Falhas de Gravação Flash)" },
        { 0xAC, "Erase Fail Count (Falhas de Eliminação Flash)" },
        { 0xAE, "Unexpected Power Loss (Quedas Inesperadas de Energia)" },
        { 0xB1, "Wear Leveling Count (Nivelamento de Desgaste)" },
        { 0xB7, "SATA Downshift Error Count" },
        { 0xB8, "End-to-End Error / IOEDC (Erros de Barramento)" },
        { 0xBB, "Reported Uncorrectable Errors (Erros Incorrigíveis)" },
        { 0xBC, "Command Timeout (Tempo Limite de Comandos)" },
        { 0xBD, "High Fly Writes" },
        { 0xBE, "Airflow Temperature (Temperatura do Fluxo de Ar)" },
        { 0xBF, "G-Sense Error Rate (Sensor de Choque/Impacto)" },
        { 0xC0, "Power-Off Retract Count (Retrações por Desligamento)" },
        { 0xC1, "Load/Unload Cycle Count (Ciclos de Cabeça)" },
        { 0xC2, "Temperature (Temperatura do Disco)" },
        { 0xC3, "Hardware ECC Recovered (Erros Corrigidos por Hardware)" },
        { 0xC4, "Reallocation Event Count (Eventos de Realocação)" },
        { 0xC5, "Current Pending Sector (Setores Pendentes)" },
        { 0xC6, "Offline Uncorrectable (Setores Incorrigíveis Offline)" },
        { 0xC7, "UltraDMA / Interface CRC Error (Erros de Cabo/Interface SATA)" },
        { 0xC8, "Write Error Rate (Taxa de Erro de Gravação)" },
        { 0xCA, "Data Address Marker Errors" },
        { 0xE7, "SSD Life Left (Vida Útil Restante do SSD)" },
        { 0xF0, "Head Flying Hours (Horas de Cabeça)" },
        { 0xF1, "Total LBAs Written (Total Gravado)" },
        { 0xF2, "Total LBAs Read (Total Lido)" }
    };

    public static DiskHealthSummary ReadDiskHealth(DiskDriveInfo drive)
    {
        var summary = new DiskHealthSummary();

        try
        {
            // Extrai o identificador limpo do PNPDeviceID (ex: SCSI\DISK&VEN_&PROD_ST3750640NS...)
            string cleanPnp = (drive.PnpDeviceId ?? string.Empty).Trim();

            byte[]? dataBytes = null;
            byte[]? thresholdBytes = null;

            // Busca os dados brutos de atributos SMART no WMI
            using var searcherData = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM MSStorageDriver_FailurePredictData");
            foreach (ManagementObject obj in searcherData.Get())
            {
                var instanceName = obj["InstanceName"]?.ToString() ?? string.Empty;
                if (IsMatchingDevice(cleanPnp, drive.Model, instanceName))
                {
                    dataBytes = (byte[])obj["VendorSpecific"];
                    break;
                }
            }

            // Busca os limiares (thresholds)
            using var searcherThresh = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM MSStorageDriver_FailurePredictThresholds");
            foreach (ManagementObject obj in searcherThresh.Get())
            {
                var instanceName = obj["InstanceName"]?.ToString() ?? string.Empty;
                if (IsMatchingDevice(cleanPnp, drive.Model, instanceName))
                {
                    thresholdBytes = (byte[])obj["VendorSpecific"];
                    break;
                }
            }

            if (dataBytes == null || dataBytes.Length < 512)
            {
                summary.IsSupported = false;
                summary.OverallHealth = SmartHealthStatus.Unknown;
                summary.HealthMessage = "S.M.A.R.T. não disponível ou não suportado por este dispositivo/controladora.";
                return summary;
            }

            // Mapeia thresholds por ID de atributo
            var threshMap = new Dictionary<byte, int>();
            if (thresholdBytes != null && thresholdBytes.Length >= 512)
            {
                for (int i = 2; i + 12 <= 512; i += 12)
                {
                    byte id = thresholdBytes[i];
                    if (id == 0) continue;
                    threshMap[id] = thresholdBytes[i + 1];
                }
            }

            // Processa atributos do disco
            var attributes = new List<SmartAttribute>();

            for (int i = 2; i + 12 <= 512; i += 12)
            {
                byte id = dataBytes[i];
                if (id == 0) continue;

                int current = dataBytes[i + 3];
                int worst = dataBytes[i + 4];

                // 6 bytes de Raw Little-Endian
                ulong raw = 0;
                for (int b = 5; b >= 0; b--)
                {
                    raw = (raw << 8) | dataBytes[i + 5 + b];
                }

                int threshold = threshMap.TryGetValue(id, out var thVal) ? thVal : 0;

                string name = KnownAttributeNames.TryGetValue(id, out var attrName)
                    ? attrName
                    : $"Atributo Desconhecido (ID {id})";

                string rawFormatted = FormatRawValue(id, raw);
                var status = EvaluateStatus(id, current, worst, threshold, raw);

                var attr = new SmartAttribute
                {
                    Id = id,
                    Name = name,
                    Current = current,
                    Worst = worst,
                    Threshold = threshold,
                    RawValue = raw,
                    RawFormatted = rawFormatted,
                    Status = status
                };

                attributes.Add(attr);

                // Armazena valores chave no sumário
                switch (id)
                {
                    case 0x05: // Reallocated Sectors
                        summary.ReallocatedSectors = raw;
                        break;
                    case 0x09: // Power-on Hours
                        summary.PowerOnHours = raw;
                        summary.PowerOnTimeFormatted = FormatPowerOnTime(raw);
                        break;
                    case 0x0C: // Power Cycle Count
                        summary.PowerCycleCount = raw;
                        break;
                    case 0xC2: // Temperature
                    case 0xBE: // Airflow Temperature
                        if (!summary.Temperature.HasValue)
                        {
                            int temp = (int)(raw & 0xFF);
                            if (temp > 0 && temp < 100) summary.Temperature = temp;
                        }
                        break;
                    case 0xC5: // Current Pending Sector
                        summary.PendingSectors = raw;
                        break;
                    case 0xC6: // Offline Uncorrectable
                        summary.OfflineUncorrectable = raw;
                        break;
                    case 0xC7: // UltraDMA / Interface CRC Error
                        summary.CrcErrors = raw;
                        break;
                }
            }

            summary.Attributes = attributes.OrderBy(a => a.Id).ToList();

            // Avalia saúde geral do disco
            EvaluateOverallHealth(summary);
        }
        catch (Exception ex)
        {
            summary.IsSupported = false;
            summary.OverallHealth = SmartHealthStatus.Unknown;
            summary.HealthMessage = $"Erro ao obter dados S.M.A.R.T.: {ex.Message}";
        }

        return summary;
    }

    private static bool IsMatchingDevice(string cleanPnp, string model, string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName)) return false;

        string inst = instanceName.Replace('\\', '_').Replace('&', '_').ToLowerInvariant();
        string pnp = cleanPnp.Replace('\\', '_').Replace('&', '_').ToLowerInvariant();
        string mdl = model.Replace(' ', '_').ToLowerInvariant();

        if (!string.IsNullOrEmpty(pnp) && inst.StartsWith(pnp)) return true;
        if (!string.IsNullOrEmpty(pnp) && pnp.StartsWith(inst)) return true;
        if (!string.IsNullOrEmpty(mdl) && inst.Contains(mdl)) return true;

        return false;
    }

    private static string FormatRawValue(byte id, ulong raw)
    {
        return id switch
        {
            0x09 => FormatPowerOnTime(raw), // Horas
            0xC2 or 0xBE => $"{(int)(raw & 0xFF)} °C", // Temperatura
            0x05 => $"{raw:N0} setores realocados",
            0xC5 => $"{raw:N0} setores pendentes",
            0xC6 => $"{raw:N0} setores incorrigíveis",
            0xC7 => $"{raw:N0} erros de interface (cabo)",
            0x04 or 0x0C => $"{raw:N0} ciclos",
            0xF1 or 0xF2 => FormatLbaSize(raw),
            _ => raw.ToString("N0", CultureInfo.CurrentCulture)
        };
    }

    public static string FormatPowerOnTime(ulong hours)
    {
        if (hours == 0) return "0 horas";

        double years = hours / 8760.0; // 365 * 24
        double days = hours / 24.0;

        if (years >= 1.0)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:N0} horas (~{1:F1} anos)", hours, years);
        }
        if (days >= 1.0)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:N0} horas (~{1:F0} dias)", hours, days);
        }
        return $"{hours:N0} horas";
    }

    private static string FormatLbaSize(ulong lbaCount)
    {
        // Cada LBA geralmente são 512 bytes
        double gb = (lbaCount * 512.0) / (1024.0 * 1024 * 1024);
        if (gb >= 1024.0)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0:F2} TB ({1:N0} LBAs)", gb / 1024.0, lbaCount);
        }
        return string.Format(CultureInfo.CurrentCulture, "{0:F1} GB ({1:N0} LBAs)", gb, lbaCount);
    }

    private static SmartHealthStatus EvaluateStatus(byte id, int current, int worst, int threshold, ulong raw)
    {
        // Se ultrapassou o limiar do fabricante
        if (threshold > 0 && current <= threshold)
        {
            return SmartHealthStatus.Critical;
        }

        // Setores pendentes ou incorrigíveis com falha ativa
        if ((id == 0xC5 || id == 0xC6) && raw > 0)
        {
            return SmartHealthStatus.Critical;
        }

        // Setores realocados já aposentados pelo disco
        if (id == 0x05 && raw > 0)
        {
            return SmartHealthStatus.Warning;
        }

        // Erros de CRC de cabo/interface
        if (id == 0xC7 && raw > 0)
        {
            return SmartHealthStatus.Warning;
        }

        // Erros incorrigíveis reportados
        if (id == 0xBB && raw > 0)
        {
            return SmartHealthStatus.Warning;
        }

        return SmartHealthStatus.Good;
    }

    private static void EvaluateOverallHealth(DiskHealthSummary summary)
    {
        if (summary.Attributes.Any(a => a.Status == SmartHealthStatus.Critical))
        {
            summary.OverallHealth = SmartHealthStatus.Critical;
            summary.HealthMessage = "Atenção Crítica: Foram detectados setores pendentes (bad blocks potenciais) ou atributos abaixo do limite do fabricante.";
            return;
        }

        var warnings = summary.Attributes.Where(a => a.Status == SmartHealthStatus.Warning).ToList();
        if (warnings.Any())
        {
            summary.OverallHealth = SmartHealthStatus.Warning;

            bool hasReallocated = summary.ReallocatedSectors.GetValueOrDefault() > 0;
            bool hasCrc = summary.CrcErrors.GetValueOrDefault() > 0;

            if (hasReallocated && hasCrc)
            {
                summary.HealthMessage = $"Aviso: O disco possui {summary.ReallocatedSectors:N0} setores realocados e histórico de {summary.CrcErrors:N0} erros de comunicação SATA (cabo). Monitore regularmente.";
            }
            else if (hasReallocated)
            {
                summary.HealthMessage = $"Aviso: O disco possui {summary.ReallocatedSectors:N0} setores realocados na área de reserva. A superfície atual está estável, mas requer atenção.";
            }
            else if (hasCrc)
            {
                summary.HealthMessage = $"Aviso: Histórico de {summary.CrcErrors:N0} erros de CRC na interface SATA (comunicação/cabo). Se o cabo já foi trocado, os erros não devem mais subir.";
            }
            else
            {
                summary.HealthMessage = "Aviso: Alguns parâmetros S.M.A.R.T. exigem acompanhamento preventivo.";
            }
            return;
        }

        summary.OverallHealth = SmartHealthStatus.Good;
        summary.HealthMessage = "O disco está perfeitamente saudável e todos os parâmetros S.M.A.R.T. estão normais.";
    }
}
