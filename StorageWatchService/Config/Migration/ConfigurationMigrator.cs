/// <summary>
/// XML to JSON Configuration Migration Utility
/// 
/// Converts existing StorageWatchConfig.xml files to the new JSON format.
/// Provides a command-line tool for legacy configuration migration.
/// </summary>

using System;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;

namespace StorageWatch.Config.Migration
{
    /// <summary>
    /// Utility for migrating configuration from XML to JSON format.
    /// </summary>
    public static class ConfigurationMigrator
    {
        /// <summary>
        /// Migrates an XML configuration file to JSON format.
        /// </summary>
        /// <param name="xmlPath">Path to the XML configuration file.</param>
        /// <param name="outputPath">Path where the JSON configuration will be written.</param>
        /// <returns>True if migration succeeded, false otherwise.</returns>
        public static bool MigrateXmlToJson(string xmlPath, string outputPath)
        {
            if (!File.Exists(xmlPath))
            {
                Console.WriteLine($"ERROR: XML configuration file not found: {xmlPath}");
                return false;
            }

            try
            {
                // Load and parse the legacy XML configuration
                var xml = XDocument.Load(xmlPath);
                var root = xml.Root ?? throw new InvalidOperationException("XML file has no root element");

                // Convert XML to JSON structure
                var jsonOptions = ConvertXmlToJson(root);

                // Write JSON file with nice formatting
                var options = new JsonSerializerOptions { WriteIndented = true };
                var jsonString = JsonSerializer.Serialize(jsonOptions, options);

                File.WriteAllText(outputPath, jsonString);

                Console.WriteLine($"✓ Successfully migrated XML to JSON");
                Console.WriteLine($"  Source: {xmlPath}");
                Console.WriteLine($"  Target: {outputPath}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR during migration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts XML configuration structure to JSON-compatible dictionary.
        /// </summary>
        private static object ConvertXmlToJson(XElement root)
        {
            var storageWatch = new Dictionary<string, object>
            {
                ["General"] = new Dictionary<string, object>
                {
                    ["EnableStartupLogging"] = ParseBool(root.Element("EnableStartupLogging")?.Value ?? "false")
                },
                ["Monitoring"] = new Dictionary<string, object>
                {
                    ["ThresholdPercent"] = int.Parse(root.Element("ThresholdPercent")?.Value ?? "10"),
                    ["Drives"] = ReadDrivesList(root.Element("Drives"))
                },
                ["Database"] = new Dictionary<string, object>
                {
                    ["ConnectionString"] = root.Element("Database")?.Element("ConnectionString")?.Value ??
                                         "Data Source=StorageWatch.db;Version=3;"
                },
                ["Alerting"] = new Dictionary<string, object>
                {
                    ["EnableNotifications"] = ParseBool(root.Element("EnableNotifications")?.Value ?? "true"),
                    ["Smtp"] = ConvertSmtpSettings(root.Element("Smtp")),
                    ["GroupMe"] = ConvertGroupMeSettings(root.Element("GroupMe"))
                },
                ["CentralServer"] = ConvertCentralServerSettings(root.Element("CentralServer")),
                ["SqlReporting"] = new Dictionary<string, object>
                {
                    ["Enabled"] = ParseBool(root.Element("EnableSqlReporting")?.Value ?? "true"),
                    ["RunMissedCollection"] = ParseBool(root.Element("RunMissedCollection")?.Value ?? "true"),
                    ["RunOnlyOncePerDay"] = ParseBool(root.Element("RunOnlyOncePerDay")?.Value ?? "true"),
                    ["CollectionTime"] = root.Element("CollectionTime")?.Value ?? "02:00"
                }
            };

            return new Dictionary<string, object> { ["StorageWatch"] = storageWatch };
        }

        /// <summary>
        /// Reads drive letters from XML Drives element.
        /// </summary>
        private static List<string> ReadDrivesList(XElement? drivesElement)
        {
            var drives = new List<string>();
            if (drivesElement != null)
            {
                foreach (var driveEl in drivesElement.Elements("Drive"))
                {
                    var drive = driveEl.Value?.Trim();
                    if (!string.IsNullOrEmpty(drive))
                        drives.Add(drive);
                }
            }
            return drives.Count > 0 ? drives : new List<string> { "C:" };
        }

        /// <summary>
        /// Converts SMTP settings from XML element.
        /// </summary>
        private static object ConvertSmtpSettings(XElement? smtpElement)
        {
            return new Dictionary<string, object>
            {
                ["Enabled"] = smtpElement != null ?
                    ParseBool(smtpElement.Element("EnableSmtp")?.Value ?? "false") : false,
                ["Host"] = smtpElement?.Element("Host")?.Value ?? "smtp.gmail.com",
                ["Port"] = smtpElement != null ?
                    int.Parse(smtpElement.Element("Port")?.Value ?? "587") : 587,
                ["UseSsl"] = smtpElement != null ?
                    ParseBool(smtpElement.Element("UseSsl")?.Value ?? "true") : true,
                ["Username"] = smtpElement?.Element("Username")?.Value ?? "",
                ["Password"] = smtpElement?.Element("Password")?.Value ?? "",
                ["FromAddress"] = smtpElement?.Element("FromAddress")?.Value ?? "",
                ["ToAddress"] = smtpElement?.Element("ToAddress")?.Value ?? ""
            };
        }

        /// <summary>
        /// Converts GroupMe settings from XML element.
        /// </summary>
        private static object ConvertGroupMeSettings(XElement? groupMeElement)
        {
            return new Dictionary<string, object>
            {
                ["Enabled"] = groupMeElement != null ?
                    ParseBool(groupMeElement.Element("EnableGroupMe")?.Value ?? "false") : false,
                ["BotId"] = groupMeElement?.Element("BotId")?.Value ?? ""
            };
        }

        /// <summary>
        /// Converts central server settings from XML element.
        /// </summary>
        private static object ConvertCentralServerSettings(XElement? serverElement)
        {
            return new Dictionary<string, object>
            {
                ["Enabled"] = serverElement != null ?
                    ParseBool(serverElement.Element("Enabled")?.Value ?? "false") : false,
                ["Mode"] = serverElement?.Element("Mode")?.Value ?? "Agent",
                ["ServerUrl"] = serverElement?.Element("ServerUrl")?.Value ?? "",
                ["ApiKey"] = serverElement?.Element("ApiKey")?.Value ?? "",
                ["Port"] = serverElement != null ?
                    int.Parse(serverElement.Element("Port")?.Value ?? "5000") : 5000,
                ["CentralConnectionString"] = serverElement?.Element("CentralConnectionString")?.Value ??
                                             "Data Source=StorageWatch_Central.db;Version=3;",
                ["ServerId"] = serverElement?.Element("ServerId")?.Value ?? "central-server"
            };
        }

        /// <summary>
        /// Parses boolean values from string representation.
        /// </summary>
        private static bool ParseBool(string value)
        {
            return bool.Parse(value ?? "false");
        }
    }
}
