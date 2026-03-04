using FluentAssertions;
using Microsoft.Extensions.Configuration;
using StorageWatch.Config.Options;

namespace StorageWatch.Tests.UnitTests
{
    public class AgentReportOptionsBindingTests
    {
        [Fact]
        public void CentralServerOptions_BindsPublishingSettings()
        {
            var settings = new Dictionary<string, string?>
            {
                ["CentralServer:ServerUrl"] = "http://localhost:5001",
                ["CentralServer:CheckIntervalSeconds"] = "120",
                ["CentralServer:ApiKey"] = "api-key"
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var options = new CentralServerOptions();
            config.GetSection("CentralServer").Bind(options);

            options.ServerUrl.Should().Be("http://localhost:5001");
            options.CheckIntervalSeconds.Should().Be(120);
            options.ApiKey.Should().Be("api-key");
        }
    }
}
