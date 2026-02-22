using FluentAssertions;
using StorageWatchUI.ViewModels;
using Xunit;

namespace StorageWatchUI.Tests.ViewModels
{
    public class UpdateProgressViewModelTests
    {
        [Fact]
        public void UpdateProgressViewModel_Initialization_SetsDefaultProperties()
        {
            // Act
            var viewModel = new UpdateProgressViewModel();

            // Assert
            viewModel.StatusText.Should().BeEmpty();
            viewModel.Progress.Should().Be(0);
            viewModel.IsIndeterminate.Should().BeTrue();
        }

        [Fact]
        public void UpdateProgressViewModel_CancelCommand_IsNotNull()
        {
            // Arrange
            var viewModel = new UpdateProgressViewModel();

            // Assert
            viewModel.CancelCommand.Should().NotBeNull();
            viewModel.CancelCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateProgressViewModel_CancelCommand_RaisesCancelRequestedEvent()
        {
            // Arrange
            var viewModel = new UpdateProgressViewModel();
            var eventRaised = false;

            viewModel.CancelRequested += (s, e) => eventRaised = true;

            // Act
            viewModel.CancelCommand.Execute(null);

            // Assert
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void UpdateProgressViewModel_StatusTextCanBeSet()
        {
            // Arrange
            var viewModel = new UpdateProgressViewModel();
            var statusText = "Downloading update...";

            // Act
            viewModel.StatusText = statusText;

            // Assert
            viewModel.StatusText.Should().Be(statusText);
        }

        [Fact]
        public void UpdateProgressViewModel_ProgressCanBeSet()
        {
            // Arrange
            var viewModel = new UpdateProgressViewModel();

            // Act
            viewModel.Progress = 50;

            // Assert
            viewModel.Progress.Should().Be(50);
        }

        [Fact]
        public void UpdateProgressViewModel_IsIndeterminateCanBeSet()
        {
            // Arrange
            var viewModel = new UpdateProgressViewModel();

            // Act
            viewModel.IsIndeterminate = false;

            // Assert
            viewModel.IsIndeterminate.Should().BeFalse();
        }

        [Fact]
        public void UpdateProgressViewModel_ProgressTransition_FromIndeterminateToPercentage()
        {
            // Arrange
            var viewModel = new UpdateProgressViewModel();

            // Act
            viewModel.IsIndeterminate = true;
            viewModel.StatusText = "Downloading update...";

            // Transition to percentage
            viewModel.IsIndeterminate = false;
            viewModel.Progress = 50;

            // Assert
            viewModel.IsIndeterminate.Should().BeFalse();
            viewModel.Progress.Should().Be(50);
            viewModel.StatusText.Should().Be("Downloading update...");
        }

        [Fact]
        public void UpdateProgressViewModel_PropertyNotifications_RaisePropertyChanged()
        {
            // Arrange
            var viewModel = new UpdateProgressViewModel();
            var propertyChangedRaised = false;

            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(UpdateProgressViewModel.StatusText))
                    propertyChangedRaised = true;
            };

            // Act
            viewModel.StatusText = "Installing update...";

            // Assert
            propertyChangedRaised.Should().BeTrue();
        }
    }
}
