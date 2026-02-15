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
    /// When enabled, allows this StorageWatch instance to receive and aggregate data from agents.
    /// </summary>
    public class CentralServerConfig
    {
        /// <summary>
        /// Enables or disables central server mode.
        /// When true, this instance will host an HTTP API for receiving agent data.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// The local port on which the central server API will listen.
        /// Default is 5000. Example: "http://localhost:5000"
        /// </summary>
        public int Port { get; set; } = 5000;

        /// <summary>
        /// Connection string for the central database (separate from local agent database).
        /// Example: "Data Source=StorageWatch_Central.db;Version=3;"
        /// </summary>
        public string CentralConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Optional identifier for this central server instance.
        /// Used for logging and identification purposes.
        /// </summary>
        public string ServerId { get; set; } = "central-server";
    }
}
