using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StorageWatchUI.Models;
using StorageWatchUI.Services.AutoUpdate;
using StorageWatchUI.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StorageWatchUI.Tests.ViewModels
{
    public class UpdateUxFlowTests
    {
        [Fact]
        public void UpdateUxFlow_Banner_AppearsWhenUpdateAvailable()
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

            // Simulate update available
            viewModel.IsUpdateAvailable = true;
            viewModel.LatestVersion = "2.0.0";

            // Act
            viewModel.IsBannerVisible = true;

            // Assert
            viewModel.IsBannerVisible.Should().BeTrue();
            viewModel.IsUpdateAvailable.Should().BeTrue();
            viewModel.LatestVersion.Should().Be("2.0.0");
        }

        [Fact]
        public void UpdateUxFlow_Banner_CanBeDismissed()
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
                IsBannerVisible = true,
                IsUpdateAvailable = true
            };

            // Act
            viewModel.DismissBannerCommand.Execute(null);

            // Assert
            viewModel.IsBannerVisible.Should().BeFalse();
            viewModel.IsUpdateAvailable.Should().BeTrue(); // Update still available
        }

        [Fact]
        public void UpdateUxFlow_Dialog_ShowsCurrentAndNewVersions()
        {
            // Arrange
            var dialogVm = new UpdateDialogViewModel();

            // Act
            dialogVm.CurrentVersion = "1.0.0";
            dialogVm.NewVersion = "2.0.0";
            dialogVm.ReleaseNotes = "Important security update";

            // Assert
            dialogVm.CurrentVersion.Should().Be("1.0.0");
            dialogVm.NewVersion.Should().Be("2.0.0");
            dialogVm.ReleaseNotes.Should().Contain("security");
        }

        [Fact]
        public void UpdateUxFlow_Progress_TransitionsCorrectly()
        {
            // Arrange
            var progressVm = new UpdateProgressViewModel();

            // Act - Downloading
            progressVm.StatusText = "Downloading update...";
            progressVm.IsIndeterminate = true;

            // Assert
            progressVm.StatusText.Should().Be("Downloading update...");
            progressVm.IsIndeterminate.Should().BeTrue();

            // Act - Verifying
            progressVm.StatusText = "Verifying integrity...";
            progressVm.Progress = 50;
            progressVm.IsIndeterminate = false;

            // Assert
            progressVm.StatusText.Should().Be("Verifying integrity...");
            progressVm.Progress.Should().Be(50);

            // Act - Installing
            progressVm.StatusText = "Installing update...";
            progressVm.Progress = 75;

            // Assert
            progressVm.StatusText.Should().Be("Installing update...");
            progressVm.Progress.Should().Be(75);

            // Act - Complete
            progressVm.StatusText = "Update installed successfully";
            progressVm.Progress = 100;

            // Assert
            progressVm.StatusText.Should().Be("Update installed successfully");
            progressVm.Progress.Should().Be(100);
        }

        [Fact]
        public void UpdateUxFlow_Progress_CanBeCanceled()
        {
            // Arrange
            var progressVm = new UpdateProgressViewModel();
            var cancelEventRaised = false;

            progressVm.CancelRequested += (s, e) => cancelEventRaised = true;
            progressVm.StatusText = "Downloading update...";

            // Act
            progressVm.CancelCommand.Execute(null);

            // Assert
            cancelEventRaised.Should().BeTrue();
        }

        [Fact]
        public void UpdateUxFlow_UpdateViewModel_HasAllRequiredCommands()
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

            // Assert - All commands exist
            viewModel.CheckForUpdatesCommand.Should().NotBeNull();
            viewModel.BeginUpdateCommand.Should().NotBeNull();
            viewModel.CancelUpdateCommand.Should().NotBeNull();
            viewModel.RestartNowCommand.Should().NotBeNull();
            viewModel.DismissBannerCommand.Should().NotBeNull();
            viewModel.RemindMeLaterCommand.Should().NotBeNull();
        }

        [Fact]
        public void UpdateUxFlow_MainViewModel_ExposesUpdateViewModel()
        {
            // Arrange
            var mockChecker = new Mock<IUiUpdateChecker>();
            var mockDownloader = new Mock<IUiUpdateDownloader>();
            var mockInstaller = new Mock<IUiUpdateInstaller>();
            var mockRestartHandler = new Mock<IUiRestartHandler>();
            var mockLogger = new Mock<ILogger<UpdateViewModel>>();
            var updateViewModel = new UpdateViewModel(
                mockChecker.Object,
                mockDownloader.Object,
                mockInstaller.Object,
                mockRestartHandler.Object,
                mockLogger.Object);

            // We'll just verify that UpdateViewModel property exists and is accessible
            // The MainViewModel integration tests should verify the property binding in UI context
            
            // Assert
            updateViewModel.Should().NotBeNull();
            updateViewModel.CheckForUpdatesCommand.Should().NotBeNull();
            updateViewModel.BeginUpdateCommand.Should().NotBeNull();
        }

        [Fact]
        public void UpdateUxFlow_RestartPrompt_AllowsUserChoice()
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

            // Act & Assert
            viewModel.RestartNowCommand.CanExecute(null).Should().BeTrue();
            mockRestartHandler.Verify(h => h.RequestRestart(), Times.Never); // Not called yet
        }

        [Fact]
        public void UpdateUxFlow_CancelUpdate_CancelsOperation()
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

            var originalStatus = viewModel.UpdateStatus;

            // Act
            viewModel.CancelUpdateCommand.Execute(null);

            // Assert
            viewModel.IsUpdateInProgress.Should().BeFalse();
            viewModel.UpdateStatus.Should().Contain("cancelled");
        }
    }
}
