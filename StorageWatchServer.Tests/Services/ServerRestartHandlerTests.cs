using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StorageWatchServer.Services.AutoUpdate;
using StorageWatchServer.Services.Logging;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Xunit;

namespace StorageWatchServer.Tests.Services
{
    public class ServerRestartHandlerTests
    {
        [Fact]
        public void ServerRestartHandler_BuildRestartHelperScript_ContainsExpectedScmCommands_ServiceName_AndLogPath()
        {
            var helperLogPath = LogDirectoryInitializer.GetLogFilePath("server-restart.log");
            var method = typeof(ServerRestartHandler).GetMethod("BuildRestartHelperScript", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var script = Assert.IsType<string>(method!.Invoke(null, new object[]
            {
                "StorageWatchServer_TestSvc",
                TimeSpan.FromSeconds(50),
                helperLogPath
            }));

            Assert.Contains("sc.exe stop $serviceName", script, StringComparison.Ordinal);
            Assert.Contains("sc.exe start $serviceName", script, StringComparison.Ordinal);
            Assert.Contains("$serviceName='StorageWatchServer_TestSvc'", script, StringComparison.Ordinal);
            Assert.Contains("$helperLogPath='", script, StringComparison.Ordinal);
            Assert.Contains("server-restart.log", script, StringComparison.Ordinal);
        }

        [Fact]
        public void ServerRestartHandler_RequestRestart_PreparesPowerShellLaunchWithoutExecuting()
        {
            var previousServiceName = Environment.GetEnvironmentVariable("STORAGEWATCH_SERVER_SERVICE_NAME");
            Environment.SetEnvironmentVariable("STORAGEWATCH_SERVER_SERVICE_NAME", "StorageWatchServer_TestSvc");

            try
            {
                var lifetime = new TestHostApplicationLifetime();
                var handler = new TestServerRestartHandler(NullLogger<ServerRestartHandler>.Instance, lifetime)
                {
                    ReturnProcess = null
                };

                handler.RequestRestart();

                if (!OperatingSystem.IsWindows())
                {
                    Assert.Null(handler.CapturedStartInfo);
                    Assert.Equal(0, lifetime.StopApplicationCount);
                    return;
                }

                Assert.NotNull(handler.CapturedStartInfo);
                Assert.Equal("powershell.exe", handler.CapturedStartInfo!.FileName);
                Assert.Contains("-ExecutionPolicy Bypass", handler.CapturedStartInfo.Arguments, StringComparison.Ordinal);
                Assert.Contains("-EncodedCommand", handler.CapturedStartInfo.Arguments, StringComparison.Ordinal);

                var encodedScript = ExtractEncodedCommand(handler.CapturedStartInfo.Arguments);
                var decodedScript = Encoding.Unicode.GetString(Convert.FromBase64String(encodedScript));

                Assert.Contains("StorageWatchServer_TestSvc", decodedScript, StringComparison.Ordinal);
                Assert.Contains("sc.exe stop $serviceName", decodedScript, StringComparison.Ordinal);
                Assert.Contains("sc.exe start $serviceName", decodedScript, StringComparison.Ordinal);
                Assert.Contains("server-restart.log", decodedScript, StringComparison.Ordinal);

                Assert.Equal(0, lifetime.StopApplicationCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable("STORAGEWATCH_SERVER_SERVICE_NAME", previousServiceName);
            }
        }

        private static string ExtractEncodedCommand(string arguments)
        {
            var marker = "-EncodedCommand ";
            var index = arguments.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index >= 0);
            return arguments[(index + marker.Length)..].Trim();
        }

        private sealed class TestServerRestartHandler : ServerRestartHandler
        {
            public TestServerRestartHandler(Microsoft.Extensions.Logging.ILogger<ServerRestartHandler> logger, IHostApplicationLifetime lifetime)
                : base(logger, lifetime)
            {
            }

            public ProcessStartInfo? CapturedStartInfo { get; private set; }
            public Process? ReturnProcess { get; set; }

            protected override Process? StartHelperProcess(ProcessStartInfo processStartInfo)
            {
                CapturedStartInfo = processStartInfo;
                return ReturnProcess;
            }
        }

        private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
        {
            public CancellationToken ApplicationStarted => CancellationToken.None;
            public CancellationToken ApplicationStopping => CancellationToken.None;
            public CancellationToken ApplicationStopped => CancellationToken.None;

            public int StopApplicationCount { get; private set; }

            public void StopApplication()
            {
                StopApplicationCount++;
            }
        }
    }
}
