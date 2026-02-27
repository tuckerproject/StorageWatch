/// <summary>
/// Test Utilities and Helpers
/// 
/// Provides common utilities for test setup, teardown, and test data generation.
/// </summary>

using StorageWatch.Config;
using StorageWatch.Config.Options;

namespace StorageWatch.Tests.Utilities
{
    public static class TestHelpers
    {
        /// <summary>
        /// Creates a valid test configuration with default values.
        /// </summary>
        public static StorageWatchOptions CreateDefaultTestConfig()
        {
            return new StorageWatchOptions
            {
                Mode = StorageWatchMode.Standalone,
                General = new GeneralOptions
                {
                    EnableStartupLogging = false
                },
                Monitoring = new MonitoringOptions
                {
                    ThresholdPercent = 20,
                    Drives = new List<string> { "C:" }
                },
                Database = new DatabaseOptions
                {
                    ConnectionString = "Data Source=:memory:"
                },
                Alerting = new AlertingOptions
                {
                    EnableNotifications = true,
                    Smtp = new SmtpOptions
                    {
                        Enabled = false,
                        Host = "smtp.example.com",
                        Port = 587,
                        UseSsl = true,
                        Username = "user@example.com",
                        Password = "password",
                        FromAddress = "from@example.com",
                        ToAddress = "to@example.com"
                    },
                    GroupMe = new GroupMeOptions
                    {
                        Enabled = false,
                        BotId = "test-bot-id"
                    }
                }
            };
        }

        /// <summary>
        /// Creates a valid test configuration for Agent mode.
        /// </summary>
        public static StorageWatchOptions CreateAgentTestConfig()
        {
            var config = CreateDefaultTestConfig();
            config.Mode = StorageWatchMode.Agent;
            return config;
        }

        /// <summary>
        /// Creates a valid test configuration for Standalone mode.
        /// </summary>
        public static StorageWatchOptions CreateStandaloneTestConfig()
        {
            var config = CreateDefaultTestConfig();
            config.Mode = StorageWatchMode.Standalone;
            return config;
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
    <AgentId>test-agent</AgentId>
    <ReportIntervalSeconds>300</ReportIntervalSeconds>
    <Port>5000</Port>
    <CentralConnectionString>Data Source=central.db</CentralConnectionString>
    <ServerId>test-server</ServerId>
  </CentralServer>
</StorageWatchConfig>";
        }
    }
}
