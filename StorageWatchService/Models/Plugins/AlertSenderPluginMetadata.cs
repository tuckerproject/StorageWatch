/// <summary>
/// Alert Sender Plugin Metadata
/// 
/// Defines metadata attributes for alert sender plugins. This allows plugins to declare
/// their capabilities, requirements, and configuration needs.
/// </summary>

using System;

namespace StorageWatch.Models.Plugins
{
    /// <summary>
    /// Attribute to mark a class as an alert sender plugin.
    /// Provides metadata about the plugin for discovery and registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class AlertSenderPluginAttribute : Attribute
    {
        /// <summary>
        /// Gets the unique identifier for this plugin.
        /// Should match the plugin's Name property implementation.
        /// </summary>
        public string PluginId { get; }

        /// <summary>
        /// Gets a human-readable description of this plugin.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets the version of this plugin.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Initializes a new instance of the AlertSenderPluginAttribute class.
        /// </summary>
        /// <param name="pluginId">The unique identifier for this plugin.</param>
        public AlertSenderPluginAttribute(string pluginId)
        {
            PluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
        }
    }

    /// <summary>
    /// Metadata about a discovered alert sender plugin.
    /// </summary>
    public class AlertSenderPluginMetadata
    {
        /// <summary>
        /// Gets or sets the unique plugin identifier.
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plugin description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the plugin version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the implementing type.
        /// </summary>
        public Type ImplementationType { get; set; } = null!;

        /// <summary>
        /// Gets or sets whether this plugin is a built-in plugin.
        /// </summary>
        public bool IsBuiltIn { get; set; }
    }
}
