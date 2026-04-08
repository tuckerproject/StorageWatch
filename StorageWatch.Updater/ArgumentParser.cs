namespace StorageWatch.Updater;

/// <summary>
/// Represents the parsed command-line arguments for the updater.
/// </summary>
internal class UpdaterArguments
{
    public bool UpdateUI { get; set; }
    public bool UpdateAgent { get; set; }
    public bool UpdateServer { get; set; }
    public bool RestartUI { get; set; }
    public bool RestartAgent { get; set; }
    public bool RestartServer { get; set; }
    public string? ManifestPath { get; set; }
    public string? SourcePath { get; set; }
    public string? TargetPath { get; set; }
}

/// <summary>
/// Parses command-line arguments for the updater.
/// </summary>
public class ArgumentParser
{
    /// <summary>
    /// Parses command-line arguments into an UpdaterArguments object.
    /// Validates required arguments and exits with non-zero code on failure.
    /// </summary>
    /// <param name="args">The command-line arguments to parse.</param>
    /// <returns>An UpdaterArguments object containing the parsed values.</returns>
    public static UpdaterArguments Parse(string[] args)
    {
        var arguments = new UpdaterArguments();
        var errors = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--update-ui":
                    arguments.UpdateUI = true;
                    break;

                case "--update-agent":
                    arguments.UpdateAgent = true;
                    break;

                case "--update-server":
                    arguments.UpdateServer = true;
                    break;

                case "--restart-ui":
                    arguments.RestartUI = true;
                    break;

                case "--restart-agent":
                    arguments.RestartAgent = true;
                    break;

                case "--restart-server":
                    arguments.RestartServer = true;
                    break;

                case "--manifest":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        arguments.ManifestPath = args[++i];
                    }
                    else
                    {
                        errors.Add("Error: --manifest flag requires a path argument.");
                    }
                    break;

                case "--source":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        arguments.SourcePath = args[++i];
                    }
                    else
                    {
                        errors.Add("Error: --source flag requires a path argument.");
                    }
                    break;

                case "--target":
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        arguments.TargetPath = args[++i];
                    }
                    else
                    {
                        errors.Add("Error: --target flag requires a path argument.");
                    }
                    break;

                default:
                    errors.Add($"Error: Unknown argument '{arg}'.");
                    break;
            }
        }

        // Validate that at least one update or restart action is specified
        bool hasUpdateAction = arguments.UpdateUI || arguments.UpdateAgent || arguments.UpdateServer;
        bool hasRestartAction = arguments.RestartUI || arguments.RestartAgent || arguments.RestartServer;

        if (!hasUpdateAction && !hasRestartAction)
        {
            errors.Add("Error: At least one update or restart action must be specified.");
        }

        // If there are any validation errors, print them and exit
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.WriteLine(error);
            }
            Console.WriteLine();
            PrintUsage();
            Environment.Exit(ExitCodes.InvalidArguments);
        }

        return arguments;
    }

    /// <summary>
    /// Prints usage information to the console.
    /// </summary>
    public static void PrintUsage()
    {
        Console.WriteLine("StorageWatch Updater");
        Console.WriteLine("Usage: StorageWatch.Updater [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --update-ui              Update the UI component");
        Console.WriteLine("  --update-agent           Update the Agent component");
        Console.WriteLine("  --update-server          Update the Server component");
        Console.WriteLine("  --restart-ui             Restart the UI component after update");
        Console.WriteLine("  --restart-agent          Restart the Agent component after update");
        Console.WriteLine("  --restart-server         Restart the Server component after update");
        Console.WriteLine("  --manifest <path>        Path to the update manifest file");
        Console.WriteLine("  --source <path>          Path to the source directory containing update files");
        Console.WriteLine("  --target <path>          Path to the target installation directory");
    }
}
