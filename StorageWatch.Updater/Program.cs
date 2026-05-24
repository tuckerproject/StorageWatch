using StorageWatch.Updater;
using StorageWatch.Updater.Services.Logging;
using StorageWatch.Shared.Update.Models;
using System.Text.Json;

// Initialize logging at the very start
var logFilePath = LogDirectoryInitializer.GetLogFilePath("updater.log");
var logger = new RollingFileLogger(logFilePath);

logger.Log("[STARTUP] StorageWatch Updater starting...");
logger.Log($"[STARTUP] Raw arguments: {string.Join(" ", args)}");

try
{
    var arguments = ArgumentParser.Parse(args);

    logger.Log($"[PARSED] UpdateUI: {arguments.UpdateUI}");
    logger.Log($"[PARSED] UpdateAgent: {arguments.UpdateAgent}");
    logger.Log($"[PARSED] UpdateServer: {arguments.UpdateServer}");
    logger.Log($"[PARSED] SelfUpdateStage: {arguments.SelfUpdateStage}");
    logger.Log($"[PARSED] SelfUpdateApply: {arguments.SelfUpdateApply}");
    logger.Log($"[PARSED] RestartUI: {arguments.RestartUI}");
    logger.Log($"[PARSED] RestartAgent: {arguments.RestartAgent}");
    logger.Log($"[PARSED] RestartServer: {arguments.RestartServer}");
    logger.Log($"[PARSED] ManifestPath: {arguments.ManifestPath}");
    logger.Log($"[PARSED] SourcePath: {arguments.SourcePath}");
    logger.Log($"[PARSED] TargetPath: {arguments.TargetPath}");
    logger.Log($"[PARSED] SelfUpdateStagingPath: {arguments.SelfUpdateStagingPath}");

    var selfUpdateManager = new SelfUpdateManager();

    if (arguments.SelfUpdateStage)
    {
        if (string.IsNullOrWhiteSpace(arguments.ManifestPath) || !File.Exists(arguments.ManifestPath))
        {
            logger.Log("[ERROR] --self-update-stage requires --manifest path to an existing manifest file.");
            Console.WriteLine("Error: --self-update-stage requires --manifest path to an existing manifest file.");
            Console.WriteLine("Updater exiting.");
            Environment.Exit(ExitCodes.InvalidArguments);
        }

        logger.Log("[SELF-UPDATE] Running explicit self-update stage mode.");
        await selfUpdateManager.RunSelfUpdateStageAsync(arguments.ManifestPath, arguments);
        logger.Log("[SELF-UPDATE] Stage mode completed.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.Success);
    }

    if (arguments.SelfUpdateApply)
    {
        if (string.IsNullOrWhiteSpace(arguments.SelfUpdateStagingPath) || string.IsNullOrWhiteSpace(arguments.TargetPath))
        {
            logger.Log("[ERROR] --self-update-apply requires --self-update-staging and --target.");
            Console.WriteLine("Error: --self-update-apply requires --self-update-staging and --target.");
            Console.WriteLine("Updater exiting.");
            Environment.Exit(ExitCodes.InvalidArguments);
        }

        logger.Log("[SELF-UPDATE] Running explicit self-update apply mode.");
        await selfUpdateManager.RunSelfUpdateApplyAsync(arguments);
        logger.Log("[SELF-UPDATE] Apply mode completed.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.Success);
    }

    // Check for updater self-update (if manifest path provided)
    if (!string.IsNullOrWhiteSpace(arguments.ManifestPath) && File.Exists(arguments.ManifestPath))
    {
        try
        {
            logger.Log("[SELF-UPDATE] Checking for updater self-update...");
            var manifestJson = await File.ReadAllTextAsync(arguments.ManifestPath);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestJson);

            if (manifest?.Updater != null)
            {
                if (selfUpdateManager.IsUpdateAvailable(manifest.Updater))
                {
                    logger.Log("[SELF-UPDATE] Updater update available. Initiating self-update...");
                    await selfUpdateManager.RunLegacySelfUpdateStageAsync(manifest.Updater, arguments);
                    // Process exits in UpdateSelfAsync if successful
                }
                else
                {
                    logger.Log("[SELF-UPDATE] Updater is already up to date.");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Log($"[SELF-UPDATE] Failed to check/apply updater self-update: {ex.Message}");
            // Continue with component update even if self-update fails
        }
    }

    if (arguments.UpdateUI)
    {
        if (string.IsNullOrWhiteSpace(arguments.SourcePath) || string.IsNullOrWhiteSpace(arguments.TargetPath))
        {
            logger.Log("[ERROR] --update-ui requires both --source and --target paths.");
            Console.WriteLine("Error: --update-ui requires both --source and --target paths.");
            Console.WriteLine("Updater exiting.");
            Environment.Exit(ExitCodes.InvalidArguments);
        }

        var fileReplacementEngine = new FileReplacementEngine();

        logger.Log("[STEP] File replacement begins for UI.");
        Console.WriteLine("File replacement begins.");
        var replaced = fileReplacementEngine.TryCopyFilesFromStaging(arguments.SourcePath, arguments.TargetPath);

        if (!replaced)
        {
            logger.Log("[ERROR] UI update failed during file replacement.");
            Console.WriteLine("UI update failed during file replacement.");
            Console.WriteLine("Updater exiting.");
            Environment.Exit(ExitCodes.UnexpectedError);
        }

        logger.Log("[STEP] File replacement succeeded for UI.");
        Console.WriteLine("File replacement succeeded.");

        var uiExecutablePath = Path.Combine(arguments.TargetPath, "StorageWatchUI.exe");
        logger.Log($"[STEP] UI restart begins. UI executable path: {uiExecutablePath}");
        var uiRestartHelper = new UIRestartHelper();
        uiRestartHelper.TryRestartUI(uiExecutablePath);
        logger.Log("[STEP] UI restart completed.");

        logger.Log("[SUCCESS] UI update completed successfully.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.Success);
    }

    if (arguments.UpdateAgent)
    {
        if (string.IsNullOrWhiteSpace(arguments.SourcePath) || string.IsNullOrWhiteSpace(arguments.TargetPath))
        {
            logger.Log("[ERROR] --update-agent requires both --source and --target paths.");
            Console.WriteLine("Error: --update-agent requires both --source and --target paths.");
            Console.WriteLine("Updater exiting.");
            Environment.Exit(ExitCodes.InvalidArguments);
        }

        var fileReplacementEngine = new FileReplacementEngine();

        logger.Log("[STEP] File replacement begins for Agent.");
        Console.WriteLine("File replacement begins.");
        var replaced = fileReplacementEngine.TryCopyFilesFromStaging(arguments.SourcePath, arguments.TargetPath);

        if (!replaced)
        {
            logger.Log("[ERROR] Agent update failed during file replacement.");
            Console.WriteLine("Agent update failed during file replacement.");
            Console.WriteLine("Updater exiting.");
            Environment.Exit(ExitCodes.UnexpectedError);
        }

        logger.Log("[STEP] File replacement succeeded for Agent.");
        Console.WriteLine("File replacement succeeded.");

        var serviceName = Environment.GetEnvironmentVariable("STORAGEWATCH_AGENT_SERVICE_NAME");
        if (string.IsNullOrWhiteSpace(serviceName))
            serviceName = "StorageWatchAgent";

        logger.Log($"[STEP] Agent restart begins for service: {serviceName}");
        Console.WriteLine("Agent restart begins.");
        var agentRestartHelper = new AgentRestartHelper();
        agentRestartHelper.TryRestartAgentService(serviceName);
        logger.Log("[STEP] Agent restart completed.");

        logger.Log("[SUCCESS] Agent update completed successfully.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.Success);
    }

    if (arguments.UpdateServer)
    {
        if (string.IsNullOrWhiteSpace(arguments.SourcePath) || string.IsNullOrWhiteSpace(arguments.TargetPath))
        {
            logger.Log("[ERROR] --update-server requires both --source and --target paths.");
            Console.WriteLine("Error: --update-server requires both --source and --target paths.");
            Console.WriteLine("Updater exiting.");
            Environment.Exit(ExitCodes.InvalidArguments);
        }

        var fileReplacementEngine = new FileReplacementEngine();

        logger.Log("[STEP] File replacement begins for Server.");
        Console.WriteLine("File replacement begins.");
        var replaced = fileReplacementEngine.TryCopyFilesFromStaging(arguments.SourcePath, arguments.TargetPath);

        if (!replaced)
        {
            logger.Log("[ERROR] Server update failed during file replacement.");
            Console.WriteLine("Server update failed during file replacement.");
            Console.WriteLine("Updater exiting.");
            Environment.Exit(ExitCodes.UnexpectedError);
        }

        logger.Log("[STEP] File replacement succeeded for Server.");
        Console.WriteLine("File replacement succeeded.");

        var serverExecutablePath = Path.Combine(arguments.TargetPath, "StorageWatchServer.exe");
        logger.Log($"[STEP] Server restart begins. Server executable path: {serverExecutablePath}");
        var serverRestartHelper = new ServerRestartHelper();
        serverRestartHelper.TryRestartServer(serverExecutablePath);
        logger.Log("[STEP] Server restart completed.");

        logger.Log("[SUCCESS] Server update completed successfully.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.Success);
    }

    if (arguments.RestartAgent)
    {
        var serviceName = Environment.GetEnvironmentVariable("STORAGEWATCH_AGENT_SERVICE_NAME");
        if (string.IsNullOrWhiteSpace(serviceName))
            serviceName = "StorageWatchAgent";

        logger.Log($"[STEP] Agent restart begins for service: {serviceName}");
        Console.WriteLine("Agent restart begins.");
        var agentRestartHelper = new AgentRestartHelper();
        agentRestartHelper.TryRestartAgentService(serviceName);
        logger.Log("[STEP] Agent restart completed.");

        logger.Log("[SUCCESS] Agent restart completed successfully.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.Success);
    }

    logger.Log("[SUCCESS] Updater completed without any update or restart actions.");
    Console.WriteLine("Updater exiting.");
    Environment.Exit(ExitCodes.Success);
}
catch (Exception ex)
{
    logger.Log($"[ERROR] Unhandled exception: {ex.GetType().FullName}: {ex.Message}");
    logger.Log($"[ERROR] Stack trace: {ex.StackTrace}");
    if (ex.InnerException != null)
    {
        logger.Log($"[ERROR] Inner exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
    }
    Console.WriteLine("Updater exiting.");
    Environment.Exit(ExitCodes.UnexpectedError);
}
