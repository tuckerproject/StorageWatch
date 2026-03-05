namespace StorageWatchServer.Server.Services;

public class ServerOptions
{
    public string ListenUrl { get; set; } = "http://localhost:5001";

    public string DatabasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "StorageWatch", "Server", "StorageWatchServer.db");

    public int OnlineTimeoutMinutes { get; set; } = 10;
}
