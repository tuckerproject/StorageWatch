using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StorageWatch.Shared.Update.Models;
using StorageWatchUI.Config;
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
        private static UpdateViewModel CreateViewModel(
            Mock<IUiUpdateChecker>? checker = null,
            Mock<IUiUpdateDownloader>? downloader = null,
            Mock<IUiUpdateInstaller>? installer = null,
            Mock<IUiAutoUpdateWorker>? worker = null,
            Mock<IUiUpdateUserSettingsStore>? userSettingsStore = null,
            Mock<IOptionsMonitor<AutoUpdateOptions>>? autoUpdateOptions = null,
            Mock<ILogger<UpdateViewModel>>? logger = null)
        {
            if (worker == null)
            {
                worker = new Mock<IUiAutoUpdateWorker>();
                worker.SetupGet(w => w.IsCycleActive).Returns(false);
            }

            if (userSettingsStore == null)
            {
                userSettingsStore = new Mock<IUiUpdateUserSettingsStore>();
                userSettingsStore.Setup(s => s.GetSkippedVersion()).Returns((string?)null);
            }

            if (autoUpdateOptions == null)
            {
                autoUpdateOptions = new Mock<IOptionsMonitor<AutoUpdateOptions>>();
                autoUpdateOptions.SetupGet(o => o.CurrentValue).Returns(new AutoUpdateOptions { CheckIntervalMinutes = 60 });
            }

            return new UpdateViewModel(
                (checker ?? new Mock<IUiUpdateChecker>()).Object,
                (downloader ?? new Mock<IUiUpdateDownloader>()).Object,
                (installer ?? new Mock<IUiUpdateInstaller>()).Object,
                worker.Object,
                userSettingsStore.Object,
                autoUpdateOptions.Object,
                (logger ?? new Mock<ILogger<UpdateViewModel>>()).Object);
        }

        [Fact]
        public void UpdateViewModel_Initialization_SetsDefaultProperties()
        {
            // Arrange
            var viewModel = CreateViewModel();

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
            var viewModel = CreateViewModel();

            // Assert
            viewModel.CheckForUpdatesCommand.Should().NotBeNull();
            viewModel.CheckForUpdatesCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateViewModel_BeginUpdateCommand_IsNotNull()
        {
            // Arrange
            var viewModel = CreateViewModel();

            // Assert
            viewModel.BeginUpdateCommand.Should().NotBeNull();
            viewModel.BeginUpdateCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateViewModel_DismissBannerCommand_HidesBanner()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.IsBannerVisible = true;

            // Act
            viewModel.DismissBannerCommand.Execute(null);

            // Assert
            viewModel.IsBannerVisible.Should().BeFalse();
        }

        [Fact]
        public void UpdateViewModel_RemindMeLaterCommand_HidesBanner()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.IsBannerVisible = true;

            // Act
            viewModel.RemindMeLaterCommand.Execute(null);

            // Assert
            viewModel.IsBannerVisible.Should().BeFalse();
        }

        [Fact]
        public void UpdateViewModel_CancelUpdateCommand_CanExecuteWhenUpdateInProgress()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.IsUpdateInProgress = true;

            // Assert
            viewModel.CancelUpdateCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateViewModel_RestartNowCommand_CanExecuteWhenRestartRequired()
        {
            // Arrange
            var viewModel = CreateViewModel();
            viewModel.IsRestartRequired = true;

            // Assert
            viewModel.RestartNowCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateViewModel_PropertyNotifications_RaisePropertyChanged()
        {
            // Arrange
            var viewModel = CreateViewModel();

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

        [Fact]
        public async Task UpdateViewModel_CheckForUpdatesAsync_SkipsWhenCycleActive()
        {
            var workerMock = new Mock<IUiAutoUpdateWorker>();
            workerMock.SetupGet(w => w.IsCycleActive).Returns(true);

            var checkerMock = new Mock<IUiUpdateChecker>();
            var viewModel = CreateViewModel(checker: checkerMock, worker: workerMock);

            viewModel.CheckForUpdatesCommand.Execute(null);

            // Allow any async work to settle
            await Task.Delay(50);

            checkerMock.Verify(c => c.CheckForUpdateAsync(It.IsAny<CancellationToken>()), Times.Never);
            viewModel.UpdateStatus.Should().Be("Update check already in progress.");
        }

        [Fact]
        public async Task UpdateViewModel_SuppressesSkippedVersion_FromUpdateCheckResult()
        {
            var skippedVersion = "2.1.0";
            var workerMock = new Mock<IUiAutoUpdateWorker>();
            workerMock.SetupGet(w => w.IsCycleActive).Returns(false);
            workerMock.Setup(w => w.TryRunUpdateCycleAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));

            var settingsMock = new Mock<IUiUpdateUserSettingsStore>();
            settingsMock.Setup(s => s.GetSkippedVersion()).Returns(skippedVersion);

            var viewModel = CreateViewModel(worker: workerMock, userSettingsStore: settingsMock);

            viewModel.CheckForUpdatesCommand.Execute(null);

            workerMock.Raise(
                w => w.UpdateCheckCompleted += null,
                workerMock.Object,
                new ComponentUpdateCheckResult
                {
                    IsUpdateAvailable = true,
                    Component = new ComponentUpdateInfo { Version = skippedVersion }
                });

            await Task.Delay(50);

            viewModel.IsBannerVisible.Should().BeFalse();
            viewModel.IsUpdateAvailable.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateViewModel_RemindMeLater_SuppressesNotificationsUntilSnoozeExpires()
        {
            var workerMock = new Mock<IUiAutoUpdateWorker>();
            workerMock.SetupGet(w => w.IsCycleActive).Returns(false);

            var optionsMock = new Mock<IOptionsMonitor<AutoUpdateOptions>>();
            optionsMock.SetupGet(o => o.CurrentValue).Returns(new AutoUpdateOptions { CheckIntervalMinutes = 60 });

            var viewModel = CreateViewModel(worker: workerMock, autoUpdateOptions: optionsMock);
            viewModel.LatestVersion = "2.2.0";
            viewModel.IsUpdateAvailable = true;
            viewModel.IsBannerVisible = true;

            viewModel.RemindMeLaterCommand.Execute(null);

            workerMock.Raise(
                w => w.UpdateCheckCompleted += null,
                workerMock.Object,
                new ComponentUpdateCheckResult
                {
                    IsUpdateAvailable = true,
                    Component = new ComponentUpdateInfo { Version = "2.2.0" }
                });

            await Task.Delay(50);

            viewModel.IsBannerVisible.Should().BeFalse();
            viewModel.IsUpdateAvailable.Should().BeFalse();
            viewModel.UpdateStatus.Should().Contain("snoozed");
        }
    }
}
