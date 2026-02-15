using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using StorageWatchUI.Models;
using StorageWatchUI.Services;
using StorageWatchUI.ViewModels;
using Xunit;

namespace StorageWatchUI.Tests.ViewModels;

public class DashboardViewModelTests
{
    [Fact]
    public async Task RefreshCommand_WithValidData_PopulatesDisks()
    {
        // Arrange
        var mockDataProvider = new Mock<IDataProvider>();
        var mockConfigService = new Mock<ConfigurationService>(Mock.Of<IConfiguration>());

        var expectedDisks = new List<DiskInfo>
        {
            new() { DriveName = "C:", TotalSpaceGb = 500, FreeSpaceGb = 100, Status = DiskStatusLevel.OK },
            new() { DriveName = "D:", TotalSpaceGb = 1000, FreeSpaceGb = 50, Status = DiskStatusLevel.Warning }
        };

        mockDataProvider
            .Setup(p => p.GetCurrentDiskStatusAsync())
            .ReturnsAsync(expectedDisks);

        var viewModel = new DashboardViewModel(mockDataProvider.Object, mockConfigService.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(500); // Give time for async operation

        // Assert
        viewModel.Disks.Should().HaveCount(2);
        viewModel.Disks.First().DriveName.Should().Be("C:");
        viewModel.StatusMessage.Should().Contain("Last updated");
    }

    [Fact]
    public async Task RefreshCommand_WithNoData_ShowsAppropriateMessage()
    {
        // Arrange
        var mockDataProvider = new Mock<IDataProvider>();
        var mockConfigService = new Mock<ConfigurationService>(Mock.Of<IConfiguration>());

        mockDataProvider
            .Setup(p => p.GetCurrentDiskStatusAsync())
            .ReturnsAsync(new List<DiskInfo>());

        var viewModel = new DashboardViewModel(mockDataProvider.Object, mockConfigService.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(500);

        // Assert
        viewModel.Disks.Should().BeEmpty();
        viewModel.StatusMessage.Should().Contain("No disk data available");
    }

    [Fact]
    public async Task RefreshCommand_WithException_ShowsErrorMessage()
    {
        // Arrange
        var mockDataProvider = new Mock<IDataProvider>();
        var mockConfigService = new Mock<ConfigurationService>(Mock.Of<IConfiguration>());

        mockDataProvider
            .Setup(p => p.GetCurrentDiskStatusAsync())
            .ThrowsAsync(new Exception("Test exception"));

        var viewModel = new DashboardViewModel(mockDataProvider.Object, mockConfigService.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(500);

        // Assert
        viewModel.StatusMessage.Should().Contain("Error loading data");
    }

    [Fact]
    public async Task RefreshCommand_WithCriticalStatus_ShowsCriticalDisks()
    {
        // Arrange
        var mockDataProvider = new Mock<IDataProvider>();
        var mockConfigService = new Mock<ConfigurationService>(Mock.Of<IConfiguration>());

        var expectedDisks = new List<DiskInfo>
        {
            new() { DriveName = "C:", TotalSpaceGb = 500, FreeSpaceGb = 10, Status = DiskStatusLevel.Critical },
            new() { DriveName = "D:", TotalSpaceGb = 1000, FreeSpaceGb = 500, Status = DiskStatusLevel.OK }
        };

        mockDataProvider
            .Setup(p => p.GetCurrentDiskStatusAsync())
            .ReturnsAsync(expectedDisks);

        var viewModel = new DashboardViewModel(mockDataProvider.Object, mockConfigService.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(500);

        // Assert
        viewModel.Disks.Should().HaveCount(2);
        viewModel.Disks.Should().Contain(d => d.Status == DiskStatusLevel.Critical);
    }

    [Fact]
    public void Constructor_InitializesWithEmptyDisks()
    {
        // Arrange
        var mockDataProvider = new Mock<IDataProvider>();
        var mockConfigService = new Mock<ConfigurationService>(Mock.Of<IConfiguration>());

        // Act
        var viewModel = new DashboardViewModel(mockDataProvider.Object, mockConfigService.Object);

        // Assert
        viewModel.Disks.Should().NotBeNull();
        viewModel.Disks.Should().BeEmpty();
        viewModel.RefreshCommand.Should().NotBeNull();
    }
}
