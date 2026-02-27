# Service Communication Sequence Diagrams

## 1. Service Status Retrieval

```mermaid
sequenceDiagram
    participant UI as StorageWatchUI
    participant Client as ServiceCommunicationClient
    participant Pipe as Named Pipe
    participant Server as ServiceCommunicationServer
    participant Service as Worker Service

    UI->>Client: GetStatusAsync()
    Client->>Pipe: Connect()
    Pipe-->>Client: Connected
    Client->>Server: {"Command": "GetStatus"}
    Server->>Service: Query current status
    Service-->>Server: Status data
    Server->>Client: {"Success": true, "Data": {...}}
    Client->>Pipe: Disconnect()
    Client-->>UI: ServiceStatusInfo
```

## 2. Log Retrieval with Fallback

```mermaid
sequenceDiagram
    participant UI as StorageWatchUI
    participant Client as ServiceCommunicationClient
    participant Pipe as Named Pipe
    participant Server as ServiceCommunicationServer
    participant Logger as RollingFileLogger
    participant File as Log File

    UI->>Client: GetLogsAsync(100)
    
    alt IPC Available
        Client->>Pipe: Connect()
        Pipe-->>Client: Connected
        Client->>Server: {"Command": "GetLogs", "Parameters": {"count": 100}}
        Server->>File: ReadLastLinesAsync(100)
        File-->>Server: Log lines
        Server->>Client: {"Success": true, "Data": [...]}
        Client->>Pipe: Disconnect()
        Client-->>UI: List<string> logs
    else IPC Unavailable
        Client--xPipe: Connection timeout
        Client->>File: Direct file read
        File-->>Client: Log lines
        Client-->>UI: List<string> logs
    end
```

## 3. Configuration Validation

```mermaid
sequenceDiagram
    participant UI as SettingsViewModel
    participant Client as ServiceCommunicationClient
    participant Server as ServiceCommunicationServer
    participant Validator as JsonConfigLoader

    UI->>Client: ValidateConfigAsync()
    Client->>Server: {"Command": "ValidateConfig"}
    Server->>Validator: Validate(configPath)
    Validator->>Validator: Check schema
    Validator->>Validator: Check required fields
    Validator->>Validator: Check data types
    Validator-->>Server: ValidationResult
    Server->>Client: {"Success": true, "Data": {...}}
    Client-->>UI: ConfigValidationResult
    
    alt Has Errors
        UI->>UI: Display errors in red
        UI->>UI: Set IsConfigValid = false
    else Has Warnings
        UI->>UI: Display warnings in yellow
        UI->>UI: Set IsConfigValid = true
    else All Valid
        UI->>UI: Display success message
        UI->>UI: Set IsConfigValid = true
    end
```

## 4. Test Alert Senders

```mermaid
sequenceDiagram
    participant UI as SettingsViewModel
    participant Client as ServiceCommunicationClient
    participant Server as ServiceCommunicationServer
    participant PluginMgr as AlertSenderPluginManager
    participant SMTP as SmtpAlertSender
    participant GroupMe as GroupMeAlertSender

    UI->>Client: TestAlertSendersAsync()
    Client->>Server: {"Command": "TestAlertSenders"}
    Server->>PluginMgr: GetEnabledSenders()
    PluginMgr-->>Server: List<IAlertSender>
    
    par Send to all enabled senders
        Server->>SMTP: SendAlertAsync("Test Alert")
        SMTP-->>Server: Success/Failure
    and
        Server->>GroupMe: SendAlertAsync("Test Alert")
        GroupMe-->>Server: Success/Failure
    end
    
    Server->>Client: {"Success": true, "Data": {...}}
    Client-->>UI: ServiceResponse
    UI->>UI: Show success message
```

## 5. Local Data Query with IPC Optimization

```mermaid
sequenceDiagram
    participant UI as DashboardViewModel
    participant Provider as EnhancedLocalDataProvider
    participant Client as ServiceCommunicationClient
    participant Server as ServiceCommunicationServer
    participant DB as SQLite Database

    UI->>Provider: GetCurrentDiskStatusAsync()
    
    alt IPC Available (Preferred)
        Provider->>Client: GetLocalDataAsync(query)
        Client->>Server: {"Command": "GetLocalData", "Parameters": {...}}
        Server->>DB: SELECT ... FROM DiskSpaceLog
        DB-->>Server: Result rows
        Server->>Client: {"Success": true, "Data": [...]}
        Client-->>Provider: JsonElement
        Provider->>Provider: ParseDiskInfoFromJson()
        Provider-->>UI: List<DiskInfo>
    else IPC Unavailable (Fallback)
        Provider->>DB: Direct SQLite query (read-only)
        DB-->>Provider: Result rows
        Provider-->>UI: List<DiskInfo>
    end
```

## 6. Service Start with Elevation

```mermaid
sequenceDiagram
    participant UI as ServiceStatusViewModel
    participant Manager as ServiceManager
    participant SCM as Service Control Manager
    participant UAC as Windows UAC

    UI->>Manager: StartServiceAsync()
    Manager->>Manager: IsRunningAsAdmin()?
    
    alt Not Admin
        Manager->>UAC: Request elevation (sc.exe start)
        UAC->>UAC: Show UAC prompt
        
        alt User approves
            UAC-->>Manager: Elevated
            Manager->>SCM: Start service
            SCM-->>Manager: Success
            Manager-->>UI: true
        else User cancels
            UAC--xManager: Cancelled
            Manager-->>UI: false
            UI->>UI: Show error message
        end
    else Already Admin
        Manager->>SCM: Start service directly
        SCM-->>Manager: Success
        Manager-->>UI: true
    end
    
    UI->>UI: Refresh status
```

## 7. Error Handling and Retry

```mermaid
sequenceDiagram
    participant UI as ViewModel
    participant Client as ServiceCommunicationClient
    participant Pipe as Named Pipe

    UI->>Client: SendRequestAsync(request)
    
    loop MaxRetries = 3
        Client->>Pipe: Connect()
        
        alt Connection Successful
            Pipe-->>Client: Connected
            Client->>Client: Send request
            Client->>Client: Receive response
            Client->>Pipe: Disconnect()
            Client-->>UI: ServiceResponse
        else Timeout
            Pipe--xClient: Timeout
            Client->>Client: Wait 500ms * attempt
            Note over Client: Retry with exponential backoff
        end
    end
    
    alt All Retries Failed
        Client-->>UI: {"Success": false, "ErrorMessage": "Failed after 3 attempts"}
        UI->>UI: Fall back to direct file/DB access
    end
```

## Key Design Decisions

### 1. Request-Response Pattern
Each UI operation creates a new connection, sends a request, receives a response, and disconnects. This keeps the protocol simple and stateless.

### 2. JSON Serialization
All data is serialized as JSON for:
- Human readability
- Easy debugging
- Cross-language compatibility (future)

### 3. Graceful Degradation
If IPC fails, the UI falls back to direct file system and database access. This ensures the UI remains functional even if the service is not responding.

### 4. Timeout and Retry
All operations have a 5-second timeout with 3 retry attempts. This balances responsiveness with reliability.

### 5. Asynchronous Processing
Both server and client use async/await throughout for non-blocking I/O and better scalability.
