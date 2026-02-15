using FluentAssertions;
using Moq;
using StorageWatchUI.Services;
using StorageWatchUI.ViewModels;
using System.ServiceProcess;
using Xunit;

namespace StorageWatchUI.Tests.ViewModels;

public class ServiceStatusViewModelTests
{
    [Fact]
    public async Task RefreshCommand_WhenServiceInstalled_SetsCorrectStatus()
    {
        // Arrange
        var mockServiceManager = new Mock<ServiceManager>();
        mockServiceManager.Setup(m => m.IsServiceInstalled()).Returns(true);
        mockServiceManager.Setup(m => m.GetServiceStatus()).Returns(ServiceControllerStatus.Running);

        var viewModel = new ServiceStatusViewModel(mockServiceManager.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(500);

        // Assert
        viewModel.IsServiceInstalled.Should().BeTrue();
        viewModel.ServiceStatus.Should().Be("Running");
        viewModel.CanStop.Should().BeTrue();
        viewModel.CanStart.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshCommand_WhenServiceNotInstalled_SetsCorrectStatus()
    {
        // Arrange
        var mockServiceManager = new Mock<ServiceManager>();
        mockServiceManager.Setup(m => m.IsServiceInstalled()).Returns(false);

        var viewModel = new ServiceStatusViewModel(mockServiceManager.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(500);

        // Assert
        viewModel.IsServiceInstalled.Should().BeFalse();
        viewModel.ServiceStatus.Should().Be("Not Installed");
        viewModel.CanStop.Should().BeFalse();
        viewModel.CanStart.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshCommand_WhenServiceStopped_EnablesStartButton()
    {
        // Arrange
        var mockServiceManager = new Mock<ServiceManager>();
        mockServiceManager.Setup(m => m.IsServiceInstalled()).Returns(true);
        mockServiceManager.Setup(m => m.GetServiceStatus()).Returns(ServiceControllerStatus.Stopped);

        var viewModel = new ServiceStatusViewModel(mockServiceManager.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(500);

        // Assert
        viewModel.CanStart.Should().BeTrue();
        viewModel.CanStop.Should().BeFalse();
    }
}
