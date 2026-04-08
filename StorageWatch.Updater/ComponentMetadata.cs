namespace StorageWatch.Updater;

/// <summary>
/// Identifies the type of component being updated.
/// </summary>
internal enum ComponentType
{
    /// <summary>StorageWatch UI application.</summary>
    UI,

    /// <summary>StorageWatch Agent service.</summary>
    Agent,

    /// <summary>StorageWatch Server service.</summary>
    Server
}

/// <summary>
/// Metadata about a StorageWatch component for update operations.
/// Contains information needed for file replacement, backup, and rollback.
/// Placeholder values are used during Phase 2; real values populated during integration.
/// </summary>
internal class ComponentMetadata
{
    /// <summary>
    /// Gets the component type.
    /// </summary>
    public ComponentType ComponentType { get; }

    /// <summary>
    /// Gets the installation directory path for this component.
    /// </summary>
    public string InstallDirectory { get; }

    /// <summary>
    /// Gets the executable name for this component (e.g., "StorageWatchUI.exe").
    /// </summary>
    public string? ExecutableName { get; }

    /// <summary>
    /// Gets the Windows service name (for Agent and Server components).
    /// </summary>
    public string? ServiceName { get; }

    /// <summary>
    /// Gets a display name for this component.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the version identifier for this component.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Initializes a new ComponentMetadata instance.
    /// </summary>
    /// <param name="componentType">The type of component.</param>
    /// <param name="installDirectory">Installation directory path.</param>
    /// <param name="displayName">Display name for the component.</param>
    /// <param name="executableName">Executable name (optional).</param>
    /// <param name="serviceName">Windows service name (optional).</param>
    /// <param name="version">Version identifier (optional).</param>
    /// <exception cref="ArgumentException">Thrown when required parameters are null or empty.</exception>
    public ComponentMetadata(
        ComponentType componentType,
        string installDirectory,
        string displayName,
        string? executableName = null,
        string? serviceName = null,
        string? version = null)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
            throw new ArgumentException("Install directory cannot be null or empty.", nameof(installDirectory));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name cannot be null or empty.", nameof(displayName));

        ComponentType = componentType;
        InstallDirectory = installDirectory;
        ExecutableName = executableName;
        ServiceName = serviceName;
        DisplayName = displayName;
        Version = version;
    }

    /// <summary>
    /// Creates metadata for the UI component with placeholder values.
    /// </summary>
    /// <returns>ComponentMetadata for UI component.</returns>
    public static ComponentMetadata CreateUIPlaceholder()
    {
        return new ComponentMetadata(
            componentType: ComponentType.UI,
            installDirectory: "[UI_INSTALL_DIR]",
            displayName: "StorageWatch UI",
            executableName: "StorageWatchUI.exe",
            version: "[UI_VERSION]");
    }

    /// <summary>
    /// Creates metadata for the Agent component with placeholder values.
    /// </summary>
    /// <returns>ComponentMetadata for Agent component.</returns>
    public static ComponentMetadata CreateAgentPlaceholder()
    {
        return new ComponentMetadata(
            componentType: ComponentType.Agent,
            installDirectory: "[AGENT_INSTALL_DIR]",
            displayName: "StorageWatch Agent",
            executableName: "StorageWatchAgent.exe",
            serviceName: "StorageWatchAgent",
            version: "[AGENT_VERSION]");
    }

    /// <summary>
    /// Creates metadata for the Server component with placeholder values.
    /// </summary>
    /// <returns>ComponentMetadata for Server component.</returns>
    public static ComponentMetadata CreateServerPlaceholder()
    {
        return new ComponentMetadata(
            componentType: ComponentType.Server,
            installDirectory: "[SERVER_INSTALL_DIR]",
            displayName: "StorageWatch Server",
            executableName: "StorageWatchServer.exe",
            serviceName: "StorageWatchServer",
            version: "[SERVER_VERSION]");
    }

    /// <summary>
    /// Gets a string representation of the metadata.
    /// </summary>
    /// <returns>Formatted string with component information.</returns>
    public override string ToString()
    {
        return $"{DisplayName} ({ComponentType}): {InstallDirectory}";
    }
}
