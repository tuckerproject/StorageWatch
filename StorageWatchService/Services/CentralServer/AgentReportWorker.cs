using StorageWatch.Config.Options;
using StorageWatch.Services.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace StorageWatch.Services.CentralServer
{
    public class AgentReportWorker : BackgroundService
    {
        private readonly IOptionsMonitor<CentralServerOptions> _centralServerOptions;
        private readonly IOptionsMonitor<StorageWatchOptions> _storageWatchOptions;
        private readonly AgentReportBuilder _reportBuilder;
        private readonly AgentReportSender _reportSender;
        private readonly RollingFileLogger _logger;

        public AgentReportWorker(
            IOptionsMonitor<CentralServerOptions> centralServerOptions,
            IOptionsMonitor<StorageWatchOptions> storageWatchOptions,
            AgentReportBuilder reportBuilder,
            AgentReportSender reportSender,
            RollingFileLogger logger)
        {
            _centralServerOptions = centralServerOptions;
            _storageWatchOptions = storageWatchOptions;
            _reportBuilder = reportBuilder;
            _reportSender = reportSender;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var options = _centralServerOptions.CurrentValue;

                if (!options.Enabled || !options.Mode.Equals("Agent", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Log("[AGENT REPORT] Reporting disabled.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                var interval = TimeSpan.FromSeconds(options.ReportIntervalSeconds);
                var agentId = string.IsNullOrWhiteSpace(options.AgentId)
                    ? Environment.MachineName
                    : options.AgentId;

                var report = _reportBuilder.BuildReport(agentId, DateTime.UtcNow);

                if (report.Drives.Count == 0)
                {
                    _logger.Log("[AGENT REPORT] No drive data collected; skipping report.");
                }
                else
                {
                    await _reportSender.SendReportAsync(report, options, stoppingToken);
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}
