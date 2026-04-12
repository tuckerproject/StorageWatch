using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StorageWatchServer.Services.AutoUpdate;
using System;
using System.Threading;
using Xunit;

namespace StorageWatchServer.Tests.Services
{
    public class ServerRestartHandlerTests
    {
        [Fact]
        public void ServerRestartHandler_RequestRestart_DoesNotThrow()
        {
            var lifetime = new TestHostApplicationLifetime();
            var handler = new ServerRestartHandler(NullLogger<ServerRestartHandler>.Instance, lifetime);

            var exception = Record.Exception(() => handler.RequestRestart());

            Assert.Null(exception);
            Assert.Equal(0, lifetime.StopApplicationCount);
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
