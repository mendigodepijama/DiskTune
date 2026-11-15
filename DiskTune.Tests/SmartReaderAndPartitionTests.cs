using DiskTune.Models;
using DiskTune.Services;
using FluentAssertions;
using Xunit;

namespace DiskTune.Tests;

public class SmartReaderAndPartitionTests
{
    [Theory]
    [InlineData(0, "0 horas")]
    [InlineData(12, "12 horas")]
    public void FormatPowerOnTime_LowHours_ReturnsHoursOnly(ulong hours, string expected)
    {
        string result = SmartReader.FormatPowerOnTime(hours);
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatPowerOnTime_Days_ReturnsDaysEstimate()
    {
        // 240 horas = 10 dias
        string result = SmartReader.FormatPowerOnTime(240);
        result.Should().Contain("dias");
        result.Should().Contain("240");
    }

    [Fact]
    public void FormatPowerOnTime_Years_ReturnsYearsEstimate()
    {
        // 44000 horas ~= 5 anos
        string result = SmartReader.FormatPowerOnTime(44000);
        result.Should().Contain("anos");
        result.Should().Contain("44");
    }

    [Fact]
    public void DiskPartitionDetail_CalculatesUsageCorrectly()
    {
        var part = new DiskPartitionDetail
        {
            DriveLetter = "D:",
            VolumeName = "BACKUP",
            FileSystem = "NTFS",
            TotalBytes = 1000L * 1024 * 1024 * 1024,
            FreeBytes = 250L * 1024 * 1024 * 1024
        };

        part.UsedBytes.Should().Be(750L * 1024 * 1024 * 1024);
        part.PercentUsed.Should().BeApproximately(75.0, 0.1);
        part.DisplayLabel.Should().Be("D: [BACKUP]");
    }

    [Fact]
    public void SmartAttribute_StatusTextAndColors_ShouldMatchEnum()
    {
        var good = new SmartAttribute { Id = 1, Status = SmartHealthStatus.Good };
        good.StatusText.Should().Be("OK");
        good.StatusColor.Should().Be("#22C55E");

        var warn = new SmartAttribute { Id = 5, Status = SmartHealthStatus.Warning };
        warn.StatusText.Should().Be("Atenção");
        warn.StatusColor.Should().Be("#F59E0B");

        var crit = new SmartAttribute { Id = 197, Status = SmartHealthStatus.Critical };
        crit.StatusText.Should().Be("Falha");
        crit.StatusColor.Should().Be("#EF4444");
    }
}
