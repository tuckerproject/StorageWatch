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
/// Result of parsing command-line arguments.
/// </summary>
internal class ParseResult
{
    /// <summary>
    /// Gets whether parsing succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets the parsed arguments if parsing succeeded.
    /// </summary>
    public UpdaterArguments Arguments { get; set; }

    /// <summary>
    /// Gets validation errors if parsing failed.
    /// </summary>
    public List<string> Errors { get; set; }

    public ParseResult()
    {
        Arguments = new UpdaterArguments();
        Errors = new List<string>();
        Success = false;
    }
}

/// <summary>
/// Parses command-line arguments for the updater.
/// Designed for testability with instance methods and non-throwing variants.
/// </summary>
public class ArgumentParser
{
    /// <summary>
    /// Initializes a new ArgumentParser instance.
    /// </summary>
    public ArgumentParser()
    {
    }

    /// <summary>
    /// Parses command-line arguments into an UpdaterArguments object.
    /// Returns results without exiting or throwing.
    /// </summary>
    /// <param name="args">The command-line arguments to parse.</param>
    /// <returns>A ParseResult containing parsed values or errors.</returns>
    public ParseResult TryParse(string[] args)
    {
        var result = new ParseResult();
        var arguments = new UpdaterArguments();
        var errors = new List<string>();

        try
        {
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

            // Set result
            if (errors.Count > 0)
            {
                result.Success = false;
                result.Errors = errors;
            }
            else
            {
                result.Success = true;
                result.Arguments = arguments;
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Exception during parsing: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parses command-line arguments into an UpdaterArguments object.
    /// Validates required arguments and exits with non-zero code on failure.
    /// Static wrapper for backward compatibility.
    /// </summary>
    /// <param name="args">The command-line arguments to parse.</param>
    /// <returns>An UpdaterArguments object containing the parsed values.</returns>
    public static UpdaterArguments Parse(string[] args)
    {
        var parser = new ArgumentParser();
        var result = parser.TryParse(args);

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine(error);
            }
            Console.WriteLine();
            PrintUsage();
            Environment.Exit(ExitCodes.InvalidArguments);
        }

        return result.Arguments;
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
