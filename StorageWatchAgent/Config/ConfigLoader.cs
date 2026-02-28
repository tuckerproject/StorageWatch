/// <summary>
/// Configuration Loader for StorageWatch
/// 
/// This utility class is responsible for loading and parsing the JSON configuration file
/// and deserializing it into strongly-typed configuration objects. It handles all configuration validation
/// and provides sensible defaults for missing values.
/// </summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace StorageWatch.Config
{
    /// <summary>
    /// Static utility class for loading and parsing configuration files.
    /// </summary>
    public static class ConfigLoader
    {
        /// <summary>
        /// Loads the configuration file and returns a fully-populated StorageWatchConfig object.
        /// </summary>
        /// <param name="path">The file path to the configuration file.</param>
        /// <returns>A populated StorageWatchConfig object with all configuration settings.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the configuration file does not exist.</exception>
        /// <exception cref="Exception">Thrown if the document is invalid or lacks a root element.</exception>
        public static StorageWatchConfig Load(string path)
        {
            // Validate that the configuration file exists before attempting to load it
            if (!File.Exists(path))
                throw new FileNotFoundException($"Configuration file not found: {path}");

            // Parse the XML document and retrieve the root element
            var xml = XDocument.Load(path);
            var root = xml.Root ?? throw new Exception("Invalid configuration file.");

            // Populate the StorageWatchConfig object by reading each XML element.
            // Helper methods (ReadBool, ReadInt, etc.) apply defaults if elements are missing.
            var config = new StorageWatchConfig
            {
                // SQL Reporting configuration
                EnableSqlReporting = ReadBool(root, "EnableSqlReporting"),
                RunMissedCollection = ReadBool(root, "RunMissedCollection"),
                RunOnlyOncePerDay = ReadBool(root, "RunOnlyOncePerDay"),
                CollectionTime = ReadTimeSpan(root, "CollectionTime"),

                // Notification configuration
                EnableNotifications = ReadBool(root, "EnableNotifications"),

                // Disk Monitoring configuration
                ThresholdPercent = ReadInt(root, "ThresholdPercent"),
                Drives = ReadList(root.Element("Drives"), "Drive"),

                // Database connection configuration
                Database = new DatabaseConfig
                {
                    ConnectionString = ReadString(root.Element("Database"), "ConnectionString")
                },

                // GroupMe alert delivery configuration
                GroupMe = new GroupMeConfig
                {
                    EnableGroupMe = ReadBool(root.Element("GroupMe"), "EnableGroupMe"),
                    BotId = ReadString(root.Element("GroupMe"), "BotId")
                },

                // SMTP alert delivery configuration
                Smtp = new SmtpConfig
                {
                    EnableSmtp = ReadBool(root.Element("Smtp"), "EnableSmtp"),
                    Host = ReadString(root.Element("Smtp"), "Host"),
                    Port = ReadInt(root.Element("Smtp"), "Port"),
                    UseSsl = ReadBool(root.Element("Smtp"), "UseSsl"),
                    Username = ReadString(root.Element("Smtp"), "Username"),
                    Password = ReadString(root.Element("Smtp"), "Password"),
                    FromAddress = ReadString(root.Element("Smtp"), "FromAddress"),
                    ToAddress = ReadString(root.Element("Smtp"), "ToAddress")
                },

                // Central Server configuration
                CentralServer = new CentralServerConfig
                {
                    Enabled = ReadBool(root.Element("CentralServer"), "Enabled"),
                    Mode = ReadString(root.Element("CentralServer"), "Mode") ?? "Agent",
                    ServerUrl = ReadString(root.Element("CentralServer"), "ServerUrl"),
                    ApiKey = ReadString(root.Element("CentralServer"), "ApiKey"),
                    Port = ReadInt(root.Element("CentralServer"), "Port"),
                    CentralConnectionString = ReadString(root.Element("CentralServer"), "CentralConnectionString"),
                    ServerId = ReadString(root.Element("CentralServer"), "ServerId")
                }
            };

            return config;
        }

        // ================================================================================
        // Helper XML parsing methods - These provide defaults for missing XML elements
        // ================================================================================

        /// <summary>
        /// Reads a boolean value from an XML element. Returns false if the element is missing.
        /// </summary>
        private static bool ReadBool(XElement? parent, string name)
            => bool.Parse(parent?.Element(name)?.Value ?? "false");

        /// <summary>
        /// Reads an integer value from an XML element. Returns 0 if the element is missing.
        /// </summary>
        private static int ReadInt(XElement? parent, string name)
            => int.Parse(parent?.Element(name)?.Value ?? "0");

        /// <summary>
        /// Reads a string value from an XML element. Returns empty string if the element is missing.
        /// </summary>
        private static string ReadString(XElement? parent, string name)
            => parent?.Element(name)?.Value ?? string.Empty;

        /// <summary>
        /// Reads a TimeSpan value from an XML element. Returns 00:00 if the element is missing or invalid.
        /// </summary>
        private static TimeSpan ReadTimeSpan(XElement parent, string name)
            => TimeSpan.Parse(parent.Element(name)?.Value ?? "00:00");

        /// <summary>
        /// Reads a list of string values from child XML elements with the specified name.
        /// </summary>
        /// <param name="parent">The parent XML element containing the list items.</param>
        /// <param name="elementName">The name of each child element to collect.</param>
        /// <returns>A List of strings. Returns empty list if parent is null.</returns>
        private static List<string> ReadList(XElement? parent, string elementName)
        {
            var list = new List<string>();
            if (parent == null) return list;

            // Extract the text value from each child element and add it to the list
            foreach (var el in parent.Elements(elementName))
                list.Add(el.Value);

            return list;
        }
    }
}