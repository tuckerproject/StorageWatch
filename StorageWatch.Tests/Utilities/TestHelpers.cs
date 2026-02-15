/// <summary>
/// Test Utilities and Helpers
/// 
/// Provides common utilities for test setup, teardown, and test data generation.
/// </summary>

using StorageWatch.Config;

namespace StorageWatch.Tests.Utilities
{
    public static class TestHelpers
    {
        /// <summary>
        /// Creates a valid test configuration with default values.
        /// </summary>
        public static StorageWatchConfig CreateDefaultTestConfig()
        {
            return new StorageWatchConfig
            {
                EnableSqlReporting = true,
                RunMissedCollection = false,
                RunOnlyOncePerDay = true,
                CollectionTime = TimeSpan.FromHours(2),
                EnableNotifications = true,
                ThresholdPercent = 20,
                Drives = new List<string> { "C:" },
                Database = new DatabaseConfig
                {
                    ConnectionString = "Data Source=:memory:"
                },
                GroupMe = new GroupMeConfig
                {
                    EnableGroupMe = false,
                    BotId = "test-bot-id"
                },
                Smtp = new SmtpConfig
                {
                    EnableSmtp = false,
                    Host = "smtp.example.com",
                    Port = 587,
                    UseSsl = true,
                    Username = "user@example.com",
                    Password = "password",
                    FromAddress = "from@example.com",
                    ToAddress = "to@example.com"
                },
                CentralServer = new CentralServerConfig
                {
                    Enabled = false,
                    Mode = "Agent",
                    ServerUrl = "http://localhost:5000",
                    ApiKey = "test-key",
                    Port = 5000,
                    CentralConnectionString = "Data Source=:memory:",
                    ServerId = "test-server"
                }
            };
        }

        /// <summary>
        /// Creates a temporary SQLite database for testing.
        /// </summary>
        public static string CreateTempDatabase()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid()}.db");
            return $"Data Source={dbPath}";
        }

        /// <summary>
        /// Creates a temporary log file for testing.
        /// </summary>
        public static string CreateTempLogFile()
        {
            return Path.Combine(Path.GetTempPath(), $"test_log_{Guid.NewGuid()}.log");
        }

        /// <summary>
        /// Creates a temporary directory for test files.
        /// </summary>
        public static string CreateTempDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "StorageWatchTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>
        /// Deletes a temporary database file.
        /// </summary>
        public static void DeleteTempDatabase(string connectionString)
        {
            try
            {
                var dbPath = connectionString.Replace("Data Source=", "").Trim();
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Generates a valid XML configuration string for testing.
        /// </summary>
        public static string GenerateTestConfigXml(
            bool enableSqlReporting = true,
            bool enableNotifications = true,
            int thresholdPercent = 20,
            string[]? drives = null)
        {
            drives ??= new[] { "C:" };

            var drivesXml = string.Join("\n    ", drives.Select(d => $"<Drive>{d}</Drive>"));

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<StorageWatchConfig>
  <EnableSqlReporting>{enableSqlReporting.ToString().ToLower()}</EnableSqlReporting>
  <RunMissedCollection>false</RunMissedCollection>
  <RunOnlyOncePerDay>true</RunOnlyOncePerDay>
  <CollectionTime>02:00:00</CollectionTime>
  <EnableNotifications>{enableNotifications.ToString().ToLower()}</EnableNotifications>
  <ThresholdPercent>{thresholdPercent}</ThresholdPercent>
  <Drives>
    {drivesXml}
  </Drives>
  <Database>
    <ConnectionString>Data Source=test.db</ConnectionString>
  </Database>
  <GroupMe>
    <EnableGroupMe>false</EnableGroupMe>
    <BotId>test-bot-id</BotId>
  </GroupMe>
  <Smtp>
    <EnableSmtp>false</EnableSmtp>
    <Host>smtp.example.com</Host>
    <Port>587</Port>
    <UseSsl>true</UseSsl>
    <Username>user@example.com</Username>
    <Password>password</Password>
    <FromAddress>from@example.com</FromAddress>
    <ToAddress>to@example.com</ToAddress>
  </Smtp>
  <CentralServer>
    <Enabled>false</Enabled>
    <Mode>Agent</Mode>
    <ServerUrl>http://localhost:5000</ServerUrl>
    <ApiKey>test-key</ApiKey>
    <Port>5000</Port>
    <CentralConnectionString>Data Source=central.db</CentralConnectionString>
    <ServerId>test-server</ServerId>
  </CentralServer>
</StorageWatchConfig>";
        }
    }
}
