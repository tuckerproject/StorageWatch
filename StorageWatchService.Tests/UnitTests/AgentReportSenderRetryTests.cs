using FluentAssertions;
using StorageWatch.Config.Options;
using StorageWatch.Models;
using StorageWatch.Services.CentralServer;
using StorageWatch.Services.Logging;
using StorageWatch.Tests.Utilities;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StorageWatch.Tests.UnitTests
{
    public class AgentReportSenderRetryTests
    {
        [Fact]
        public async Task SendReportAsync_RetriesOnServerError()
        {
            var handler = new SequencedHandler(
                new HttpResponseMessage(HttpStatusCode.InternalServerError),
                new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(handler);
            var logger = new RollingFileLogger(TestHelpers.CreateTempLogFile());
            var sender = new AgentReportSender(httpClient, logger, new[] { TimeSpan.Zero });

            var options = new CentralServerOptions
            {
                ServerUrl = "http://localhost:5001",
                ApiKey = ""
            };

            var report = new AgentReportRequest
            {
                AgentId = "agent-1",
                TimestampUtc = DateTime.UtcNow,
                Drives = { new DriveReportDto { DriveLetter = "C:", TotalSpaceGb = 500, FreeSpaceGb = 200, UsedPercent = 60 } }
            };

            var result = await sender.SendReportAsync(report, options, CancellationToken.None);

            result.Should().BeTrue();
            handler.CallCount.Should().Be(2);
        }

        [Fact]
        public async Task SendReportAsync_DoesNotRetryOnClientError()
        {
            var handler = new SequencedHandler(new HttpResponseMessage(HttpStatusCode.BadRequest));
            var httpClient = new HttpClient(handler);
            var logger = new RollingFileLogger(TestHelpers.CreateTempLogFile());
            var sender = new AgentReportSender(httpClient, logger, new[] { TimeSpan.Zero });

            var options = new CentralServerOptions
            {
                ServerUrl = "http://localhost:5001",
                ApiKey = ""
            };

            var report = new AgentReportRequest
            {
                AgentId = "agent-1",
                TimestampUtc = DateTime.UtcNow,
                Drives = { new DriveReportDto { DriveLetter = "C:", TotalSpaceGb = 500, FreeSpaceGb = 200, UsedPercent = 60 } }
            };

            var result = await sender.SendReportAsync(report, options, CancellationToken.None);

            result.Should().BeFalse();
            handler.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task SendReportAsync_RetriesOnNetworkFailure()
        {
            var handler = new SequencedHandler(
                new HttpRequestException("Network down"),
                new HttpResponseMessage(HttpStatusCode.OK));

            var httpClient = new HttpClient(handler);
            var logger = new RollingFileLogger(TestHelpers.CreateTempLogFile());
            var sender = new AgentReportSender(httpClient, logger, new[] { TimeSpan.Zero });

            var options = new CentralServerOptions
            {
                ServerUrl = "http://localhost:5001",
                ApiKey = ""
            };

            var report = new AgentReportRequest
            {
                AgentId = "agent-1",
                TimestampUtc = DateTime.UtcNow,
                Drives = { new DriveReportDto { DriveLetter = "C:", TotalSpaceGb = 500, FreeSpaceGb = 200, UsedPercent = 60 } }
            };

            var result = await sender.SendReportAsync(report, options, CancellationToken.None);

            result.Should().BeTrue();
            handler.CallCount.Should().Be(2);
        }

        private sealed class SequencedHandler : HttpMessageHandler
        {
            private readonly Queue<object> _responses;

            public SequencedHandler(params object[] responses)
            {
                _responses = new Queue<object>(responses);
            }

            public int CallCount { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;

                if (_responses.Count == 0)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                }

                var response = _responses.Dequeue();
                if (response is Exception exception)
                {
                    throw exception;
                }

                return Task.FromResult((HttpResponseMessage)response);
            }
        }
    }
}
