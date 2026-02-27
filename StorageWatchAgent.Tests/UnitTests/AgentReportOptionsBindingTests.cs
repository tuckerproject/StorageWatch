using FluentAssertions;
using Microsoft.Extensions.Configuration;
using StorageWatch.Config.Options;

namespace StorageWatch.Tests.UnitTests
{
    public class AgentReportOptionsBindingTests
    {
        [Fact]
        public void CentralServerOptions_BindsReportingSettings()
        {
            var settings = new Dictionary<string, string?>
            {
                ["CentralServer:Enabled"] = "true",
                ["CentralServer:Mode"] = "Agent",
                ["CentralServer:BaseUrl"] = "http://localhost:5001",
                ["CentralServer:ApiKey"] = "api-key",
                ["CentralServer:AgentId"] = "agent-42",
                ["CentralServer:ReportingIntervalSeconds"] = "120"
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var options = new CentralServerOptions();
            config.GetSection("CentralServer").Bind(options);

            options.Enabled.Should().BeTrue();
            options.Mode.Should().Be("Agent");
            options.ServerUrl.Should().Be("http://localhost:5001");
            options.ApiKey.Should().Be("api-key");
            options.AgentId.Should().Be("agent-42");
            options.ReportIntervalSeconds.Should().Be(120);
        }
    }
}
