using FluentAssertions;
using StorageWatchUI.ViewModels;
using Xunit;

namespace StorageWatchUI.Tests.ViewModels
{
    public class UpdateDialogViewModelTests
    {
        [Fact]
        public void UpdateDialogViewModel_Initialization_SetsDefaultProperties()
        {
            // Act
            var viewModel = new UpdateDialogViewModel();

            // Assert
            viewModel.CurrentVersion.Should().BeEmpty();
            viewModel.NewVersion.Should().BeEmpty();
            viewModel.ReleaseNotes.Should().BeEmpty();
        }

        [Fact]
        public void UpdateDialogViewModel_UpdateCommand_IsNotNull()
        {
            // Arrange
            var viewModel = new UpdateDialogViewModel();

            // Assert
            viewModel.UpdateCommand.Should().NotBeNull();
            viewModel.UpdateCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateDialogViewModel_CancelCommand_IsNotNull()
        {
            // Arrange
            var viewModel = new UpdateDialogViewModel();

            // Assert
            viewModel.CancelCommand.Should().NotBeNull();
            viewModel.CancelCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void UpdateDialogViewModel_UpdateCommand_RaisesUpdateRequestedEvent()
        {
            // Arrange
            var viewModel = new UpdateDialogViewModel();
            var eventRaised = false;

            viewModel.UpdateRequested += (s, e) => eventRaised = true;

            // Act
            viewModel.UpdateCommand.Execute(null);

            // Assert
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void UpdateDialogViewModel_CancelCommand_RaisesCancelRequestedEvent()
        {
            // Arrange
            var viewModel = new UpdateDialogViewModel();
            var eventRaised = false;

            viewModel.CancelRequested += (s, e) => eventRaised = true;

            // Act
            viewModel.CancelCommand.Execute(null);

            // Assert
            eventRaised.Should().BeTrue();
        }

        [Fact]
        public void UpdateDialogViewModel_PropertiesCanBeSet()
        {
            // Arrange
            var viewModel = new UpdateDialogViewModel();

            // Act
            viewModel.CurrentVersion = "1.0.0";
            viewModel.NewVersion = "2.0.0";
            viewModel.ReleaseNotes = "Bug fixes and improvements";

            // Assert
            viewModel.CurrentVersion.Should().Be("1.0.0");
            viewModel.NewVersion.Should().Be("2.0.0");
            viewModel.ReleaseNotes.Should().Be("Bug fixes and improvements");
        }
    }
}
