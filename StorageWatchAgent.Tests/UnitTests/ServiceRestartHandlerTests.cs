using StorageWatch.Services.AutoUpdate;
using StorageWatch.Services.Logging;
using StorageWatch.Tests.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace StorageWatch.Tests.UnitTests
{
    public class ServiceRestartHandlerTests
    {
        [Fact]
        public void UpdaterServiceRestartHandler_RequestRestart_LaunchesUpdaterAndExits()
        {
            var updaterPath = Path.Combine(AppContext.BaseDirectory, "StorageWatch.Updater.exe");
            File.WriteAllText(updaterPath, string.Empty);

            try
            {
                var logger = new RollingFileLogger(TestHelpers.CreateTempLogFile());
                ProcessStartInfo? capturedStartInfo = null;
                var exitCount = 0;

                var handler = new UpdaterServiceRestartHandler(
                    logger,
                    "StorageWatchAgent_TestSvc",
                    processStartInfo =>
                    {
                        capturedStartInfo = processStartInfo;
                        return Process.GetCurrentProcess();
                    },
                    () => exitCount++);

                handler.RequestRestart();

                if (!OperatingSystem.IsWindows())
                {
                    Assert.Null(capturedStartInfo);
                    Assert.Equal(0, exitCount);
                    return;
                }

                Assert.NotNull(capturedStartInfo);
                Assert.Contains("StorageWatch.Updater", capturedStartInfo!.FileName, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("--restart-agent", capturedStartInfo.Arguments);
                Assert.Equal("StorageWatchAgent_TestSvc", capturedStartInfo.EnvironmentVariables["STORAGEWATCH_AGENT_SERVICE_NAME"]);
                Assert.Equal(1, exitCount);
            }
            finally
            {
                if (File.Exists(updaterPath))
                    File.Delete(updaterPath);
            }
        }

        [Fact]
        public void UpdaterServiceRestartHandler_RequestRestart_DoesNotExitWhenUpdaterLaunchFails()
        {
            var updaterPath = Path.Combine(AppContext.BaseDirectory, "StorageWatch.Updater.exe");
            File.WriteAllText(updaterPath, string.Empty);

            try
            {
                var logger = new RollingFileLogger(TestHelpers.CreateTempLogFile());
                var exitCount = 0;

                var handler = new UpdaterServiceRestartHandler(
                    logger,
                    "StorageWatchAgent_TestSvc",
                    _ => null,
                    () => exitCount++);

                handler.RequestRestart();

                Assert.Equal(0, exitCount);
            }
            finally
            {
                if (File.Exists(updaterPath))
                    File.Delete(updaterPath);
            }
        }
    }
}
