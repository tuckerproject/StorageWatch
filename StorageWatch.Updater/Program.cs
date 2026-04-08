using StorageWatch.Updater;

// Parse command-line arguments
var arguments = UpdaterArguments.Parse(args);

// Display parsed arguments
Console.WriteLine("StorageWatch Updater");
Console.WriteLine();
Console.WriteLine("Parsed Arguments:");
Console.WriteLine();

// Update flags
var hasUpdates = arguments.UpdateUI || arguments.UpdateAgent || arguments.UpdateServer;
if (hasUpdates)
{
    Console.WriteLine("Update Actions:");
    if (arguments.UpdateUI)
        Console.WriteLine("  ✓ Update UI");
    if (arguments.UpdateAgent)
        Console.WriteLine("  ✓ Update Agent");
    if (arguments.UpdateServer)
        Console.WriteLine("  ✓ Update Server");
    Console.WriteLine();
}

// Restart flags
var hasRestarts = arguments.RestartUI || arguments.RestartAgent || arguments.RestartServer;
if (hasRestarts)
{
    Console.WriteLine("Restart Actions:");
    if (arguments.RestartUI)
        Console.WriteLine("  ✓ Restart UI");
    if (arguments.RestartAgent)
        Console.WriteLine("  ✓ Restart Agent");
    if (arguments.RestartServer)
        Console.WriteLine("  ✓ Restart Server");
    Console.WriteLine();
}

// Paths
var hasPaths = arguments.ManifestPath != null || arguments.SourcePath != null || arguments.TargetPath != null;
if (hasPaths)
{
    Console.WriteLine("Paths:");
    if (arguments.ManifestPath != null)
        Console.WriteLine($"  Manifest: {arguments.ManifestPath}");
    if (arguments.SourcePath != null)
        Console.WriteLine($"  Source:   {arguments.SourcePath}");
    if (arguments.TargetPath != null)
        Console.WriteLine($"  Target:   {arguments.TargetPath}");
    Console.WriteLine();
}

Console.WriteLine("Ready for update operations.");
Environment.Exit(ExitCodes.Success);
