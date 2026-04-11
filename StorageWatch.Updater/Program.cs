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

Console.WriteLine("Updater exiting.");
Environment.Exit(ExitCodes.Success);
