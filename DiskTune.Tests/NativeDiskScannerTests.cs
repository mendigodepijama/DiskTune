using DiskTune.Models;
using DiskTune.Services;
using FluentAssertions;
using Xunit;

namespace DiskTune.Tests;

public class NativeDiskScannerTests
{
    [Fact]
    public void Scanner_PauseAndResume_ShouldNotThrow()
    {
        // Arrange
        var scanner = new NativeDiskScanner();

        // Act & Assert
        scanner.Invoking(s => s.Pause()).Should().NotThrow();
        scanner.Invoking(s => s.Resume()).Should().NotThrow();
        scanner.Invoking(s => s.Cancel()).Should().NotThrow();
    }

    [Fact]
    public async Task RunScanAsync_WithInvalidDevice_ShouldCompleteOrNotifyError()
    {
        // Arrange
        var scanner = new NativeDiskScanner();
        var invalidDrive = new DiskDriveInfo
        {
            DeviceId = @"\\.\NonExistentPhysicalDrive999",
            TotalSizeBytes = 1024 * 1024 * 1024
        };

        bool completedFired = false;
        scanner.ScanCompleted += (sender, msg) =>
        {
            completedFired = true;
        };

        // Act
        await scanner.RunScanAsync(invalidDrive, totalBlocks: 10, timeoutMs: 100);

        // Assert
        completedFired.Should().BeTrue();
    }
}
