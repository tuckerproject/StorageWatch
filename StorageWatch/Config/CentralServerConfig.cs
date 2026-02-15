/// <summary>
/// Central Server Configuration
/// 
/// This class defines configuration settings for the central server mode,
/// allowing StorageWatch to run as a central aggregation point that receives
/// disk space data from local and remote agents.
/// </summary>

namespace StorageWatch.Config
{
    /// <summary>
    /// Configuration for the central server component.
    /// When enabled, allows this StorageWatch instance to act as either:
    /// - An agent that forwards data to a central server, or
    /// - A central server that receives and aggregates data from agents.
    /// </summary>
    public class CentralServerConfig
    {
        /// <summary>
        /// Enables or disables central server functionality.
        /// When true, this instance will either forward data to a central server (agent mode)
        /// or host an HTTP API for receiving agent data (server mode).
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The role this instance plays: "Agent" (forwards to central server) or "Server" (receives from agents).
        /// Default is "Agent".
        /// </summary>
        public string Mode { get; set; } = "Agent";

        /// <summary>
        /// [Agent Mode] The URL of the central server to forward data to.
        /// Example: "http://central-server.example.com:5000"
        /// Leave empty or null if running in Server mode.
        /// </summary>
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// [Agent Mode] Optional API key or shared secret for authentication with the central server.
        /// Sent as an "X-API-Key" header in each request.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// [Server Mode] The local port on which the central server API will listen.
        /// Default is 5000. Example: "http://localhost:5000"
        /// Ignored if running in Agent mode.
        /// </summary>
        public int Port { get; set; } = 5000;

        /// <summary>
        /// [Server Mode] Connection string for the central database (separate from local agent database).
        /// Example: "Data Source=StorageWatch_Central.db;Version=3;"
        /// Ignored if running in Agent mode.
        /// </summary>
        public string CentralConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// [Server Mode] Optional identifier for this central server instance.
        /// Used for logging and identification purposes.
        /// Ignored if running in Agent mode.
        /// </summary>
        public string ServerId { get; set; } = "central-server";
    }
}
