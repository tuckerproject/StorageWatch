/// <summary>
/// Alert Sender Plugin Registry
/// 
/// Central registry for discovering, registering, and managing alert sender plugins.
/// Supports both built-in plugins (SMTP, GroupMe) and external plugins loaded from assemblies.
/// </summary>

using Microsoft.Extensions.DependencyInjection;
using StorageWatch.Models;
using StorageWatch.Models.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StorageWatch.Services.Alerting.Plugins
{
    /// <summary>
    /// Manages plugin discovery and registration for alert senders.
    /// </summary>
    public class AlertSenderPluginRegistry
    {
        private readonly Dictionary<string, AlertSenderPluginMetadata> _plugins = new();

        /// <summary>
        /// Gets all registered plugins.
        /// </summary>
        public IReadOnlyDictionary<string, AlertSenderPluginMetadata> Plugins => _plugins;

        /// <summary>
        /// Discovers and registers all alert sender plugins in the specified assemblies.
        /// </summary>
        /// <param name="assemblies">Assemblies to scan for plugins. If null, scans the current assembly.</param>
        public void DiscoverPlugins(params Assembly[] assemblies)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                assemblies = new[] { Assembly.GetExecutingAssembly() };
            }

            foreach (var assembly in assemblies)
            {
                var pluginTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IAlertSender).IsAssignableFrom(t));

                foreach (var type in pluginTypes)
                {
                    var attribute = type.GetCustomAttribute<AlertSenderPluginAttribute>();

                    var metadata = new AlertSenderPluginMetadata
                    {
                        PluginId = attribute?.PluginId ?? type.Name,
                        Description = attribute?.Description ?? string.Empty,
                        Version = attribute?.Version ?? "1.0.0",
                        ImplementationType = type,
                        IsBuiltIn = assembly == Assembly.GetExecutingAssembly()
                    };

                    _plugins[metadata.PluginId] = metadata;
                }
            }
        }

        /// <summary>
        /// Registers a plugin manually.
        /// </summary>
        /// <typeparam name="TPlugin">The plugin type implementing IAlertSender.</typeparam>
        /// <param name="pluginId">The unique plugin identifier.</param>
        /// <param name="description">Optional description.</param>
        public void RegisterPlugin<TPlugin>(string pluginId, string description = "") where TPlugin : IAlertSender
        {
            var metadata = new AlertSenderPluginMetadata
            {
                PluginId = pluginId,
                Description = description,
                Version = "1.0.0",
                ImplementationType = typeof(TPlugin),
                IsBuiltIn = true
            };

            _plugins[pluginId] = metadata;
        }

        /// <summary>
        /// Gets metadata for a specific plugin by ID.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <returns>Plugin metadata if found, null otherwise.</returns>
        public AlertSenderPluginMetadata? GetPlugin(string pluginId)
        {
            _plugins.TryGetValue(pluginId, out var metadata);
            return metadata;
        }

        /// <summary>
        /// Checks if a plugin is registered.
        /// </summary>
        /// <param name="pluginId">The plugin identifier.</param>
        /// <returns>True if the plugin is registered, false otherwise.</returns>
        public bool IsPluginRegistered(string pluginId)
        {
            return _plugins.ContainsKey(pluginId);
        }

        /// <summary>
        /// Registers all discovered plugins with the DI container.
        /// </summary>
        /// <param name="services">The service collection.</param>
        public void RegisterWithDependencyInjection(IServiceCollection services)
        {
            foreach (var metadata in _plugins.Values)
            {
                // Register each plugin as a transient service
                // They will be resolved by the plugin manager based on configuration
                services.AddTransient(typeof(IAlertSender), metadata.ImplementationType);
            }
        }
    }
}
