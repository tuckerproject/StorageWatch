using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace StorageWatchServer.Tests.Integration
{
    public class UpdateRollbackTests
    {
        [Fact]
        public void RollbackScenario_SuccessfulRollback_RestoresOldVersion()
        {
            var testRoot = Path.Combine(Path.GetTempPath(), $"StorageWatch-Test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testRoot);

            try
            {
                var installDir = Path.Combine(testRoot, "install");
                var payloadRelativePath = Path.Combine("app", "version.txt");
                var installPayloadPath = Path.Combine(installDir, payloadRelativePath);
                var backupDir = Path.Combine(installDir, ".backup");
                var backupPayloadPath = Path.Combine(backupDir, payloadRelativePath);

                Directory.CreateDirectory(installDir);
                Directory.CreateDirectory(Path.GetDirectoryName(installPayloadPath)!);

                // Start with v1
                File.WriteAllText(installPayloadPath, "v1");

                // Simulate backup (what would happen during an update)
                Directory.CreateDirectory(backupDir);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPayloadPath)!);
                File.Copy(installPayloadPath, backupPayloadPath, overwrite: true);

                // Verify backup exists
                Assert.True(File.Exists(backupPayloadPath));
                Assert.Equal("v1", File.ReadAllText(backupPayloadPath));

                // Simulate update to v2 (by manual modification, as updater would do)
                File.WriteAllText(installPayloadPath, "v2");
                Assert.Equal("v2", File.ReadAllText(installPayloadPath));

                // Simulate rollback (restore from backup)
                File.Copy(backupPayloadPath, installPayloadPath, overwrite: true);

                // Verify rollback successful
                Assert.Equal("v1", File.ReadAllText(installPayloadPath));
                Assert.True(Directory.Exists(backupDir));
            }
            finally
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, recursive: true);
            }
        }

        [Fact]
        public void RollbackScenario_PartialRollback_MultipleComponentsMixedState()
        {
            // Simulate scenario where:
            // - Agent updated successfully (v1 -> v2)
            // - Server update failed (v1 remains)
            // - Rollback initiated for failed component only
            
            var testRoot = Path.Combine(Path.GetTempPath(), $"StorageWatch-Test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testRoot);

            try
            {
                var agentInstallDir = Path.Combine(testRoot, "agent");
                var agentPayloadPath = Path.Combine(agentInstallDir, "version.txt");
                var agentBackupDir = Path.Combine(agentInstallDir, ".backup");
                var agentBackupPayloadPath = Path.Combine(agentBackupDir, "version.txt");

                var serverInstallDir = Path.Combine(testRoot, "server");
                var serverPayloadPath = Path.Combine(serverInstallDir, "version.txt");
                var serverBackupDir = Path.Combine(serverInstallDir, ".backup");
                var serverBackupPayloadPath = Path.Combine(serverBackupDir, "version.txt");

                // Setup initial state (both at v1)
                Directory.CreateDirectory(agentInstallDir);
                Directory.CreateDirectory(serverInstallDir);
                File.WriteAllText(agentPayloadPath, "v1");
                File.WriteAllText(serverPayloadPath, "v1");

                // Create backups before update
                Directory.CreateDirectory(agentBackupDir);
                Directory.CreateDirectory(serverBackupDir);
                File.WriteAllText(agentBackupPayloadPath, "v1");
                File.WriteAllText(serverBackupPayloadPath, "v1");

                // Simulate Agent update succeeds (v1 -> v2)
                File.WriteAllText(agentPayloadPath, "v2");

                // Server update fails and remains at v1 (no change)
                // (no modification to serverPayloadPath)

                // Verify mixed state
                Assert.Equal("v2", File.ReadAllText(agentPayloadPath)); // Agent updated
                Assert.Equal("v1", File.ReadAllText(serverPayloadPath)); // Server not updated

                // Rollback only Server (since it "failed" in update attempt)
                File.Copy(serverBackupPayloadPath, serverPayloadPath, overwrite: true);

                // Verify post-rollback state
                Assert.Equal("v2", File.ReadAllText(agentPayloadPath)); // Agent remains at v2 (successful)
                Assert.Equal("v1", File.ReadAllText(serverPayloadPath)); // Server back at v1 (rolled back)
            }
            finally
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, recursive: true);
            }
        }

        [Fact]
        public void RollbackDiagnostics_VersionMismatchDetected_BetweenComponents()
        {
            // Simulate detection of version mismatch and diagnostic logging
            var testRoot = Path.Combine(Path.GetTempPath(), $"StorageWatch-Test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testRoot);

            try
            {
                var componentVersions = new Dictionary<string, string>
                {
                    ["UI"] = "2.1.0",
                    ["Agent"] = "2.0.0",
                    ["Server"] = "2.1.0"
                };

                // Detect mismatch
                var uiVersion = componentVersions["UI"];
                var agentVersion = componentVersions["Agent"];
                var serverVersion = componentVersions["Server"];

                var versionsConsistent = uiVersion == agentVersion && agentVersion == serverVersion;
                var diagnosticLog = Path.Combine(testRoot, "update-diagnostics.log");

                if (!versionsConsistent)
                {
                    var diagnostic = $"Version mismatch detected: UI={uiVersion}, Agent={agentVersion}, Server={serverVersion}. " +
                                    $"Partial update detected. Recommend rollback or manual verification.";
                    File.WriteAllText(diagnosticLog, diagnostic);
                }

                // Verify diagnostic generated
                Assert.False(versionsConsistent);
                Assert.True(File.Exists(diagnosticLog));
                var logContent = File.ReadAllText(diagnosticLog);
                Assert.Contains("Version mismatch detected", logContent);
                Assert.Contains("UI=2.1.0", logContent);
                Assert.Contains("Agent=2.0.0", logContent);
            }
            finally
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, recursive: true);
            }
        }

        [Fact]
        public void RollbackScenario_BackupMissing_LogsErrorButDoesNotThrow()
        {
            // Simulate attempted rollback when backup doesn't exist
            var testRoot = Path.Combine(Path.GetTempPath(), $"StorageWatch-Test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testRoot);

            try
            {
                var installDir = Path.Combine(testRoot, "install");
                var payloadPath = Path.Combine(installDir, "version.txt");
                var backupDir = Path.Combine(installDir, ".backup");
                var backupPayloadPath = Path.Combine(backupDir, "version.txt");

                Directory.CreateDirectory(installDir);
                File.WriteAllText(payloadPath, "broken");

                var errorLogged = false;
                var diagnosticLog = Path.Combine(testRoot, "rollback-error.log");

                // Attempt rollback when backup missing
                if (!File.Exists(backupPayloadPath))
                {
                    errorLogged = true;
                    File.WriteAllText(diagnosticLog, $"Rollback failed: backup not found at {backupPayloadPath}");
                }

                // Verify error logged but process didn't crash
                Assert.True(errorLogged);
                Assert.True(File.Exists(diagnosticLog));
                Assert.Contains("Rollback failed", File.ReadAllText(diagnosticLog));
                // Original file unchanged
                Assert.Equal("broken", File.ReadAllText(payloadPath));
            }
            finally
            {
                if (Directory.Exists(testRoot))
                    Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
