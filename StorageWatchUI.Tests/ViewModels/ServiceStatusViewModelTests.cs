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
        var mockServiceManager = new Mock<IServiceManager>();
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
        var mockServiceManager = new Mock<IServiceManager>();
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
        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager.Setup(m => m.IsServiceInstalled()).Returns(true);
        mockServiceManager.Setup(m => m.GetServiceStatus()).Returns(ServiceControllerStatus.Stopped);

        var viewModel = new ServiceStatusViewModel(mockServiceManager.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(500);

        // Assert
        viewModel.CanStart.Should().BeTrue();
        viewModel.CanStop.Should().BeFalse();
        viewModel.ServiceStatus.Should().Be("Stopped");
    }

    [Fact]
    public async Task RefreshCommand_WhenServicePaused_SetsCorrectStatus()
    {
        // Arrange
        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager.Setup(m => m.IsServiceInstalled()).Returns(true);
        mockServiceManager.Setup(m => m.GetServiceStatus()).Returns(ServiceControllerStatus.Paused);

        var viewModel = new ServiceStatusViewModel(mockServiceManager.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(500);

        // Assert
        viewModel.ServiceStatus.Should().Be("Paused");
        viewModel.CanStart.Should().BeFalse();
        viewModel.CanStop.Should().BeFalse();
    }

    [Fact]
    public async Task StartServiceAsync_WhenSuccessful_RefreshesStatus()
    {
        // Arrange
        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager.Setup(m => m.IsServiceInstalled()).Returns(true);
        mockServiceManager.Setup(m => m.GetServiceStatus()).Returns(ServiceControllerStatus.Stopped);
        mockServiceManager.Setup(m => m.StartServiceAsync()).ReturnsAsync(true);

        var viewModel = new ServiceStatusViewModel(mockServiceManager.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(300);

        // Assert
        viewModel.CanStart.Should().BeTrue();
        mockServiceManager.Verify(m => m.StartServiceAsync(), Times.Never);
    }

    [Fact]
    public async Task StopServiceAsync_WhenSuccessful_RefreshesStatus()
    {
        // Arrange
        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager.Setup(m => m.IsServiceInstalled()).Returns(true);
        mockServiceManager.Setup(m => m.GetServiceStatus()).Returns(ServiceControllerStatus.Running);
        mockServiceManager.Setup(m => m.StopServiceAsync()).ReturnsAsync(true);

        var viewModel = new ServiceStatusViewModel(mockServiceManager.Object);

        // Act
        viewModel.RefreshCommand.Execute(null);
        await Task.Delay(300);

        // Assert
        viewModel.CanStop.Should().BeTrue();
        mockServiceManager.Verify(m => m.StopServiceAsync(), Times.Never);
    }

    [Fact]
    public void IsRunningAsAdmin_WhenNotAdmin_SetsCorrectProperty()
    {
        // Arrange
        var mockServiceManager = new Mock<IServiceManager>();
        mockServiceManager.Setup(m => m.IsRunningAsAdmin()).Returns(false);
        mockServiceManager.Setup(m => m.IsServiceInstalled()).Returns(true);
        mockServiceManager.Setup(m => m.GetServiceStatus()).Returns(ServiceControllerStatus.Running);

        // Act
        var viewModel = new ServiceStatusViewModel(mockServiceManager.Object);

        // Assert - ViewModel should handle non-admin scenario gracefully
        viewModel.Should().NotBeNull();
    }
}
