using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StorageWatchUI.Models;
using StorageWatchUI.Services.AutoUpdate;
using StorageWatchUI.ViewModels;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StorageWatchUI.Tests.ViewModels
{
    public class UpdateViewModelTests
    {
        [Fact]
        public void UpdateViewModel_Initialization_SetsDefaultProperties()
        {
            // Arrange
            var mockChecker = new Mock<IUiUpdateChecker>();
            var mockDownloader = new Mock<IUiUpdateDownloader>();
            var mockInstaller = new Mock<IUiUpdateInstaller>();
            var mockRestartHandler = new Mock<IUiRestartHandler>();
            var mockLogger = new Mock<ILogger<UpdateViewModel>>();

            // Act
            var viewModel = new UpdateViewModel(
                mockChecker.Object,
                mockDownloader.Object,
                mockInstaller.Object,
                mockRestartHandler.Object,
                mockLogger.Object);

            // Assert
            viewModel.IsUpdateAvailable.Should().BeFalse();
            viewModel.IsUpdateInProgress.Should().BeFalse();
            viewModel.IsRestartRequired.Should().BeFalse();
            viewModel.IsBannerVisible.Should().BeFalse();
            viewModel.UpdateProgress.Should().Be(0);
        }

        [Fact]
        public void UpdateViewModel_CheckForUpdatesCommand_IsNotNull()
        {
            // Arrange
            var mockChecker = new Mock<IUiUpdateChecker>();
            var mockDownloader = new Mock<IUiUpdateDownloader>();
            var mockInstaller = new Mock<IUiUpdateInstaller>();
            var mockRestartHandler = new Mock<IUiRestartHandler>();
            var mockLogger = new Mock<ILogger<UpdateViewModel>>();

            var viewModel = new UpdateViewModel(
                mockChecker.Object,
                mockDownloader.Object,
                mockInstaller.Object,
                mockRestartHandler.Object,
                mockLogger.Object);

            // Assert
            viewModel.CheckForUpdatesCommand.Should().NotBeNull();
            viewModel.CheckForUpdatesCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateViewModel_BeginUpdateCommand_IsNotNull()
        {
            // Arrange
            var mockChecker = new Mock<IUiUpdateChecker>();
            var mockDownloader = new Mock<IUiUpdateDownloader>();
            var mockInstaller = new Mock<IUiUpdateInstaller>();
            var mockRestartHandler = new Mock<IUiRestartHandler>();
            var mockLogger = new Mock<ILogger<UpdateViewModel>>();

            var viewModel = new UpdateViewModel(
                mockChecker.Object,
                mockDownloader.Object,
                mockInstaller.Object,
                mockRestartHandler.Object,
                mockLogger.Object);

            // Assert
            viewModel.BeginUpdateCommand.Should().NotBeNull();
            viewModel.BeginUpdateCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateViewModel_DismissBannerCommand_HidesBanner()
        {
            // Arrange
            var mockChecker = new Mock<IUiUpdateChecker>();
            var mockDownloader = new Mock<IUiUpdateDownloader>();
            var mockInstaller = new Mock<IUiUpdateInstaller>();
            var mockRestartHandler = new Mock<IUiRestartHandler>();
            var mockLogger = new Mock<ILogger<UpdateViewModel>>();

            var viewModel = new UpdateViewModel(
                mockChecker.Object,
                mockDownloader.Object,
                mockInstaller.Object,
                mockRestartHandler.Object,
                mockLogger.Object)
            {
                IsBannerVisible = true
            };

            // Act
            viewModel.DismissBannerCommand.Execute(null);

            // Assert
            viewModel.IsBannerVisible.Should().BeFalse();
        }

        [Fact]
        public void UpdateViewModel_RemindMeLaterCommand_HidesBanner()
        {
            // Arrange
            var mockChecker = new Mock<IUiUpdateChecker>();
            var mockDownloader = new Mock<IUiUpdateDownloader>();
            var mockInstaller = new Mock<IUiUpdateInstaller>();
            var mockRestartHandler = new Mock<IUiRestartHandler>();
            var mockLogger = new Mock<ILogger<UpdateViewModel>>();

            var viewModel = new UpdateViewModel(
                mockChecker.Object,
                mockDownloader.Object,
                mockInstaller.Object,
                mockRestartHandler.Object,
                mockLogger.Object)
            {
                IsBannerVisible = true
            };

            // Act
            viewModel.RemindMeLaterCommand.Execute(null);

            // Assert
            viewModel.IsBannerVisible.Should().BeFalse();
        }

        [Fact]
        public void UpdateViewModel_CancelUpdateCommand_CanExecuteWhenUpdateInProgress()
        {
            // Arrange
            var mockChecker = new Mock<IUiUpdateChecker>();
            var mockDownloader = new Mock<IUiUpdateDownloader>();
            var mockInstaller = new Mock<IUiUpdateInstaller>();
            var mockRestartHandler = new Mock<IUiRestartHandler>();
            var mockLogger = new Mock<ILogger<UpdateViewModel>>();

            var viewModel = new UpdateViewModel(
                mockChecker.Object,
                mockDownloader.Object,
                mockInstaller.Object,
                mockRestartHandler.Object,
                mockLogger.Object)
            {
                IsUpdateInProgress = true
            };

            // Assert
            viewModel.CancelUpdateCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateViewModel_RestartNowCommand_CanExecuteWhenRestartRequired()
        {
            // Arrange
            var mockChecker = new Mock<IUiUpdateChecker>();
            var mockDownloader = new Mock<IUiUpdateDownloader>();
            var mockInstaller = new Mock<IUiUpdateInstaller>();
            var mockRestartHandler = new Mock<IUiRestartHandler>();
            var mockLogger = new Mock<ILogger<UpdateViewModel>>();

            var viewModel = new UpdateViewModel(
                mockChecker.Object,
                mockDownloader.Object,
                mockInstaller.Object,
                mockRestartHandler.Object,
                mockLogger.Object)
            {
                IsRestartRequired = true
            };

            // Assert
            viewModel.RestartNowCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateViewModel_PropertyNotifications_RaisePropertyChanged()
        {
            // Arrange
            var mockChecker = new Mock<IUiUpdateChecker>();
            var mockDownloader = new Mock<IUiUpdateDownloader>();
            var mockInstaller = new Mock<IUiUpdateInstaller>();
            var mockRestartHandler = new Mock<IUiRestartHandler>();
            var mockLogger = new Mock<ILogger<UpdateViewModel>>();

            var viewModel = new UpdateViewModel(
                mockChecker.Object,
                mockDownloader.Object,
                mockInstaller.Object,
                mockRestartHandler.Object,
                mockLogger.Object);

            var propertyChangedRaised = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(UpdateViewModel.IsUpdateAvailable))
                    propertyChangedRaised = true;
            };

            // Act
            viewModel.IsUpdateAvailable = true;

            // Assert
            propertyChangedRaised.Should().BeTrue();
        }
    }
}
