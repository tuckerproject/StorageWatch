namespace StorageWatchServer.Server.Services;

public class ServerOptions
{
    public string ListenUrl { get; set; } = "http://localhost:5001";

    public string DatabasePath { get; set; } = "Data/StorageWatchServer.db";

    public int OnlineTimeoutMinutes { get; set; } = 10;
}
