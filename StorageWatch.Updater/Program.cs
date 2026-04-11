using StorageWatch.Updater;

var arguments = ArgumentParser.Parse(args);

if (arguments.UpdateUI)
{
    if (string.IsNullOrWhiteSpace(arguments.SourcePath) || string.IsNullOrWhiteSpace(arguments.TargetPath))
    {
        Console.WriteLine("Error: --update-ui requires both --source and --target paths.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.InvalidArguments);
    }

    var fileReplacementEngine = new FileReplacementEngine();

    Console.WriteLine("File replacement begins.");
    var replaced = fileReplacementEngine.TryCopyFilesFromStaging(arguments.SourcePath, arguments.TargetPath);

    if (!replaced)
    {
        Console.WriteLine("UI update failed during file replacement.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.UnexpectedError);
    }

    Console.WriteLine("File replacement succeeded.");

    var uiExecutablePath = Path.Combine(arguments.TargetPath, "StorageWatchUI.exe");
    var uiRestartHelper = new UIRestartHelper();
    uiRestartHelper.TryRestartUI(uiExecutablePath);

    Console.WriteLine("Updater exiting.");
    Environment.Exit(ExitCodes.Success);
}

if (arguments.UpdateAgent)
{
    if (string.IsNullOrWhiteSpace(arguments.SourcePath) || string.IsNullOrWhiteSpace(arguments.TargetPath))
    {
        Console.WriteLine("Error: --update-agent requires both --source and --target paths.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.InvalidArguments);
    }

    var fileReplacementEngine = new FileReplacementEngine();

    Console.WriteLine("File replacement begins.");
    var replaced = fileReplacementEngine.TryCopyFilesFromStaging(arguments.SourcePath, arguments.TargetPath);

    if (!replaced)
    {
        Console.WriteLine("Agent update failed during file replacement.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.UnexpectedError);
    }

    Console.WriteLine("File replacement succeeded.");

    var serviceName = Environment.GetEnvironmentVariable("STORAGEWATCH_AGENT_SERVICE_NAME");
    if (string.IsNullOrWhiteSpace(serviceName))
        serviceName = "StorageWatchAgent";

    Console.WriteLine("Agent restart begins.");
    var agentRestartHelper = new AgentRestartHelper();
    agentRestartHelper.TryRestartAgentService(serviceName);

    Console.WriteLine("Updater exiting.");
    Environment.Exit(ExitCodes.Success);
}

if (arguments.UpdateServer)
{
    if (string.IsNullOrWhiteSpace(arguments.SourcePath) || string.IsNullOrWhiteSpace(arguments.TargetPath))
    {
        Console.WriteLine("Error: --update-server requires both --source and --target paths.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.InvalidArguments);
    }

    var fileReplacementEngine = new FileReplacementEngine();

    Console.WriteLine("File replacement begins.");
    var replaced = fileReplacementEngine.TryCopyFilesFromStaging(arguments.SourcePath, arguments.TargetPath);

    if (!replaced)
    {
        Console.WriteLine("Server update failed during file replacement.");
        Console.WriteLine("Updater exiting.");
        Environment.Exit(ExitCodes.UnexpectedError);
    }

    Console.WriteLine("File replacement succeeded.");

    var serverExecutablePath = Path.Combine(arguments.TargetPath, "StorageWatchServer.exe");
    var serverRestartHelper = new ServerRestartHelper();
    serverRestartHelper.TryRestartServer(serverExecutablePath);

    Console.WriteLine("Updater exiting.");
    Environment.Exit(ExitCodes.Success);
}

if (arguments.RestartAgent)
{
    var serviceName = Environment.GetEnvironmentVariable("STORAGEWATCH_AGENT_SERVICE_NAME");
    if (string.IsNullOrWhiteSpace(serviceName))
        serviceName = "StorageWatchAgent";

    Console.WriteLine("Agent restart begins.");
    var agentRestartHelper = new AgentRestartHelper();
    agentRestartHelper.TryRestartAgentService(serviceName);

    Console.WriteLine("Updater exiting.");
    Environment.Exit(ExitCodes.Success);
}

Console.WriteLine("Updater exiting.");
Environment.Exit(ExitCodes.Success);
