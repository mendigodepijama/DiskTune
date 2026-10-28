using DiskTune.Models;
using FluentAssertions;
using Xunit;

namespace DiskTune.Tests;

public class DiskDriveInfoTests
{
    [Theory]
    [InlineData(0, "0 GB")]
    [InlineData(-100, "0 GB")]
    [InlineData(50L * 1024 * 1024 * 1024, "50.0 GB")]
    [InlineData(500L * 1024 * 1024 * 1024, "500.0 GB")]
    [InlineData(1024L * 1024 * 1024 * 1024, "1.00 TB")]
    [InlineData(2L * 1024 * 1024 * 1024 * 1024L, "2.00 TB")]
    public void FormatBytes_ShouldReturnExpectedString(long bytes, string expected)
    {
        // Act
        string result = DiskDriveInfo.FormatBytes(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void DisplayName_ShouldFormatCorrectly()
    {
        // Arrange
        var drive = new DiskDriveInfo
        {
            Index = 0,
            Model = "Samsung SSD 980",
            TotalSizeBytes = 500L * 1024 * 1024 * 1024,
            AssociatedPartitions = "C: (Windows)"
        };

        // Act
        string displayName = drive.DisplayName;

        // Assert
        displayName.Should().Be("Disco 0: Samsung SSD 980 (500.0 GB) [C: (Windows)]");
    }
}
