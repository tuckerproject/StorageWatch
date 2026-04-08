namespace StorageWatch.Updater;

/// <summary>
/// Standard exit codes for the StorageWatch Updater application.
/// </summary>
internal static class ExitCodes
{
    /// <summary>Operation completed successfully.</summary>
    public const int Success = 0;

    /// <summary>Invalid or missing command-line arguments.</summary>
    public const int InvalidArguments = 1;

    /// <summary>An unexpected error occurred during operation.</summary>
    public const int UnexpectedError = 2;
}
