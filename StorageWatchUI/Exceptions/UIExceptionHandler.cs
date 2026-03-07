using StorageWatchUI.Services.Logging;
using System.Windows;

namespace StorageWatchUI.Exceptions;

/// <summary>
/// Global UI exception handler that logs unhandled exceptions and displays user-friendly messages.
/// </summary>
public static class UIExceptionHandler
{
    private static RollingFileLogger? _logger;

    /// <summary>
    /// Initializes the global exception handler with a logger.
    /// </summary>
    public static void Initialize(RollingFileLogger? logger)
    {
        _logger = logger;

        // Register for unhandled exceptions in the UI thread
        Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Register for unhandled exceptions on background threads
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

        // Register for task exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Log($"[ERROR] Unhandled UI exception: {e.Exception.Message}");
        
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application may become unstable.",
            "Unhandled Exception",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Mark as handled to keep the app running
        e.Handled = true;
    }

    private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            _logger?.Log($"[ERROR] Unhandled exception on background thread: {ex.Message}");
        }
    }

    private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Log($"[ERROR] Unobserved task exception: {e.Exception.Message}");

        // Don't mark as handled - let the system decide if the app should terminate
        // e.SetObserved(); // Uncomment if you want to suppress app termination
    }
}
