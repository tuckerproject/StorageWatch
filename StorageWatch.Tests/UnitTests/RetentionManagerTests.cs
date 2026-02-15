/// <summary>
/// Unit Tests for RetentionManager
/// 
/// Tests the retention rule evaluation and retention logic without a real database.
/// </summary>

using FluentAssertions;
using Moq;
using StorageWatch.Config.Options;
using StorageWatch.Services.DataRetention;
using StorageWatch.Services.Logging;

namespace StorageWatch.Tests.UnitTests
{
    public class RetentionManagerTests
    {
        private readonly Mock<RollingFileLogger> _mockLogger;
        private const string TestConnectionString = "Data Source=:memory:";

        public RetentionManagerTests()
        {
            _mockLogger = new Mock<RollingFileLogger>(Path.GetTempFileName());
        }

        [Fact]
        public void Constructor_WithValidOptions_InitializesSuccessfully()
        {
            // Arrange
            var options = new RetentionOptions
            {
                Enabled = true,
                MaxDays = 365,
                MaxRows = 10000,
                CleanupIntervalMinutes = 60,
                ArchiveEnabled = false
            };

            // Act
            var manager = new RetentionManager(TestConnectionString, options, _mockLogger.Object);

            // Assert
            manager.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullConnectionString_ThrowsArgumentNullException()
        {
            // Arrange
            var options = new RetentionOptions { Enabled = true };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RetentionManager(null!, options, _mockLogger.Object)
            );
        }

        [Fact]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Arrange
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RetentionManager(TestConnectionString, null!, _mockLogger.Object)
            );
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange
            var options = new RetentionOptions { Enabled = true };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new RetentionManager(TestConnectionString, options, null!)
            );
        }

        [Fact]
        public async Task CheckAndCleanupAsync_WhenDisabled_ReturnsFalse()
        {
            // Arrange
            var options = new RetentionOptions { Enabled = false };
            var manager = new RetentionManager(TestConnectionString, options, _mockLogger.Object);

            // Act
            var result = await manager.CheckAndCleanupAsync();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task CheckAndCleanupAsync_WhenIntervalNotElapsed_ReturnsFalse()
        {
            // Arrange
            var options = new RetentionOptions
            {
                Enabled = true,
                CleanupIntervalMinutes = 60  // 60 minutes interval
            };
            var manager = new RetentionManager(TestConnectionString, options, _mockLogger.Object);

            // Act - Call twice in quick succession
            var result1 = await manager.CheckAndCleanupAsync();
            var result2 = await manager.CheckAndCleanupAsync();

            // Assert - First call should attempt cleanup (may fail due to in-memory DB), second should skip
            result2.Should().BeFalse("Second call within interval should be skipped");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(365)]
        [InlineData(3650)]
        public void RetentionOptions_WithValidMaxDays_PassesValidation(int maxDays)
        {
            // Arrange
            var options = new RetentionOptions { MaxDays = maxDays };

            // Act & Assert - Should not throw
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, true);

            isValid.Should().BeTrue("MaxDays should be valid");
        }

        [Theory]
        [InlineData(0)]      // Below minimum
        [InlineData(36501)]  // Above maximum
        public void RetentionOptions_WithInvalidMaxDays_FailsValidation(int maxDays)
        {
            // Arrange
            var options = new RetentionOptions { MaxDays = maxDays };

            // Act
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, true);

            // Assert
            isValid.Should().BeFalse("MaxDays should be invalid");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(60)]
        [InlineData(1440)]    // 1 day in minutes
        [InlineData(10080)]   // 7 days in minutes
        public void RetentionOptions_WithValidCleanupInterval_PassesValidation(int intervalMinutes)
        {
            // Arrange
            var options = new RetentionOptions { CleanupIntervalMinutes = intervalMinutes };

            // Act & Assert
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, true);

            isValid.Should().BeTrue("CleanupIntervalMinutes should be valid");
        }

        [Theory]
        [InlineData(0)]       // Below minimum
        [InlineData(10081)]   // Above maximum
        public void RetentionOptions_WithInvalidCleanupInterval_FailsValidation(int intervalMinutes)
        {
            // Arrange
            var options = new RetentionOptions { CleanupIntervalMinutes = intervalMinutes };

            // Act
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, true);

            // Assert
            isValid.Should().BeFalse("CleanupIntervalMinutes should be invalid");
        }

        [Fact]
        public void RetentionOptions_WithArchiveEnabledButNoDirectory_HasEmptyArchiveDirectory()
        {
            // Arrange
            var options = new RetentionOptions
            {
                ArchiveEnabled = true,
                ArchiveDirectory = ""
            };

            // Act & Assert
            options.ArchiveDirectory.Should().BeEmpty();
            options.ArchiveEnabled.Should().BeTrue();
        }

        [Fact]
        public void RetentionOptions_WithValidArchivePath_PassesValidation()
        {
            // Arrange
            var options = new RetentionOptions
            {
                ArchiveEnabled = true,
                ArchiveDirectory = "C:\\Archives\\StorageWatch"
            };

            // Act & Assert
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(options);
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(options, context, results, true);

            isValid.Should().BeTrue();
        }

        [Fact]
        public void RetentionOptions_DefaultValues_AreReasonable()
        {
            // Arrange & Act
            var options = new RetentionOptions();

            // Assert
            options.Enabled.Should().BeTrue("Retention should be enabled by default");
            options.MaxDays.Should().Be(365, "Default retention should be 1 year");
            options.MaxRows.Should().Be(0, "Default max rows should be unlimited (0)");
            options.CleanupIntervalMinutes.Should().Be(60, "Default cleanup interval should be hourly");
            options.ArchiveEnabled.Should().BeFalse("Archiving should be disabled by default");
            options.ExportCsvEnabled.Should().BeTrue("CSV export should be enabled by default");
        }
    }
}
