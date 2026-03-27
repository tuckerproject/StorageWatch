using Microsoft.Extensions.Hosting;
using StorageWatch.Services.AutoUpdate;
using StorageWatch.Services.Logging;
using StorageWatch.Tests.Utilities;
using System;
using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace StorageWatch.Tests.UnitTests
{
    public class ServiceRestartHandlerTests
    {
        [Fact]
        public void ScmServiceRestartHandler_BuildRestartHelperScript_ContainsExpectedCommandsAndServiceName()
        {
            var method = typeof(ScmServiceRestartHandler).GetMethod("BuildRestartHelperScript", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var script = Assert.IsType<string>(method!.Invoke(null, new object[]
            {
                "StorageWatchAgent_TestSvc",
                TimeSpan.FromSeconds(45)
            }));

            Assert.Contains("Stop-Service -Name $serviceName", script, StringComparison.Ordinal);
            Assert.Contains("Start-Service -Name $serviceName", script, StringComparison.Ordinal);
            Assert.Contains("$serviceName='StorageWatchAgent_TestSvc'", script, StringComparison.Ordinal);
            Assert.Contains("FromSeconds(45)", script, StringComparison.Ordinal);
        }

        [Fact]
        public void ScmServiceRestartHandler_RequestRestart_PreparesPowerShellLaunchWithoutExecuting()
        {
            var previousServiceName = Environment.GetEnvironmentVariable("STORAGEWATCH_AGENT_SERVICE_NAME");
            Environment.SetEnvironmentVariable("STORAGEWATCH_AGENT_SERVICE_NAME", "StorageWatchAgent_TestSvc");

            try
            {
                var lifetime = new TestHostApplicationLifetime();
                var logger = new RollingFileLogger(TestHelpers.CreateTempLogFile());
                var handler = new TestScmServiceRestartHandler(logger, lifetime)
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
                Assert.Contains("Stop-Service -Name $serviceName", handler.CapturedStartInfo.Arguments, StringComparison.Ordinal);
                Assert.Contains("Start-Service -Name $serviceName", handler.CapturedStartInfo.Arguments, StringComparison.Ordinal);
                Assert.Contains("StorageWatchAgent_TestSvc", handler.CapturedStartInfo.Arguments, StringComparison.Ordinal);

                Assert.Equal(0, lifetime.StopApplicationCount);
            }
            finally
            {
                Environment.SetEnvironmentVariable("STORAGEWATCH_AGENT_SERVICE_NAME", previousServiceName);
            }
        }

        private sealed class TestScmServiceRestartHandler : ScmServiceRestartHandler
        {
            public TestScmServiceRestartHandler(RollingFileLogger logger, IHostApplicationLifetime lifetime)
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
