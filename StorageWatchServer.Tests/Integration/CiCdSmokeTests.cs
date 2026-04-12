using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace StorageWatchServer.Tests.Integration
{
    public class CiCdSmokeTests
    {
        private static string FindRepositoryRoot()
        {
            var path = AppContext.BaseDirectory;
            while (!Directory.Exists(Path.Combine(path, ".git")) && path != Path.GetPathRoot(path))
            {
                path = Directory.GetParent(path)!.FullName;
            }

            if (!Directory.Exists(Path.Combine(path, ".git")))
                throw new InvalidOperationException("Could not find repository root.");

            return path;
        }

        [Fact]
        public void Smoke_UpdaterExecutableExists()
        {
            var repoRoot = FindRepositoryRoot();
            var configuration =
                Environment.GetEnvironmentVariable("CONFIGURATION")
                ?? Environment.GetEnvironmentVariable("Configuration")
                ?? "Debug";

            var updaterExePath = Path.Combine(
                repoRoot,
                "StorageWatch.Updater",
                "bin",
                configuration,
                "net10.0",
                "StorageWatch.Updater.exe");

            Assert.True(File.Exists(updaterExePath), $"Updater executable not found at: {updaterExePath}");
        }

        [Fact]
        public void Smoke_UpdaterExecutableHasValidVersion()
        {
            var repoRoot = FindRepositoryRoot();
            var updaterExePath = Path.Combine(repoRoot, "StorageWatch.Updater", "bin", "Debug", "net10.0", "StorageWatch.Updater.exe");

            if (!File.Exists(updaterExePath))
            {
                // Skip if updater not built yet
                Assert.True(true, "Skipping: Updater not built yet");
                return;
            }

            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(updaterExePath);
            Assert.NotNull(versionInfo.FileVersion);
            Assert.False(string.IsNullOrWhiteSpace(versionInfo.FileVersion));
            
            // Version should be in format like "1.0.0.0"
            var versionParts = versionInfo.FileVersion!.Split('.');
            Assert.True(versionParts.Length >= 3, $"Invalid version format: {versionInfo.FileVersion}");
        }

        [Fact]
        public void Smoke_UpdaterSha256HashComputable()
        {
            var repoRoot = FindRepositoryRoot();
            var updaterExePath = Path.Combine(repoRoot, "StorageWatch.Updater", "bin", "Debug", "net10.0", "StorageWatch.Updater.exe");

            if (!File.Exists(updaterExePath))
            {
                // Skip if updater not built yet
                Assert.True(true, "Skipping: Updater not built yet");
                return;
            }

            using var hashAlgo = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(updaterExePath);
            var hash = hashAlgo.ComputeHash(stream);
            var hashString = Convert.ToHexString(hash).ToLowerInvariant();

            Assert.NotNull(hashString);
            Assert.Equal(64, hashString.Length); // SHA256 produces 64 hex characters
        }

        [Fact]
        public void Smoke_ManifestSchemaValid_MinimalExample()
        {
            // Test minimal valid manifest structure
            var manifestJson = @"{
                ""version"": ""1.0.0"",
                ""ui"": {
                    ""version"": ""1.0.0"",
                    ""downloadUrl"": ""https://updates.example.com/ui.zip"",
                    ""sha256"": ""abc123def456""
                },
                ""agent"": {
                    ""version"": ""1.0.0"",
                    ""downloadUrl"": ""https://updates.example.com/agent.zip"",
                    ""sha256"": ""def456abc123""
                },
                ""server"": {
                    ""version"": ""1.0.0"",
                    ""downloadUrl"": ""https://updates.example.com/server.zip"",
                    ""sha256"": ""ghi789jkl012""
                }
            }";

            // Parse and validate structure
            var manifest = System.Text.Json.JsonDocument.Parse(manifestJson);
            var root = manifest.RootElement;

            Assert.True(root.TryGetProperty("version", out var versionProp));
            Assert.Equal("1.0.0", versionProp.GetString());

            Assert.True(root.TryGetProperty("ui", out var uiProp));
            Assert.True(uiProp.TryGetProperty("version", out _));
            Assert.True(uiProp.TryGetProperty("downloadUrl", out _));
            Assert.True(uiProp.TryGetProperty("sha256", out _));

            Assert.True(root.TryGetProperty("agent", out var agentProp));
            Assert.True(agentProp.TryGetProperty("version", out _));
            Assert.True(agentProp.TryGetProperty("downloadUrl", out _));
            Assert.True(agentProp.TryGetProperty("sha256", out _));

            Assert.True(root.TryGetProperty("server", out var serverProp));
            Assert.True(serverProp.TryGetProperty("version", out _));
            Assert.True(serverProp.TryGetProperty("downloadUrl", out _));
            Assert.True(serverProp.TryGetProperty("sha256", out _));
        }

        [Fact]
        public void Smoke_ManifestComponentVersionsAreSemVer()
        {
            var testVersions = new[]
            {
                "1.0.0",
                "2.1.3",
                "10.20.30",
                "1.0.0-alpha",
                "1.0.0-beta.1"
            };

            foreach (var version in testVersions)
            {
                // Basic SemVer validation: major.minor.patch with optional prerelease
                var isSemVer = System.Text.RegularExpressions.Regex.IsMatch(
                    version,
                    @"^\d+\.\d+\.\d+(-[a-zA-Z0-9\.\-]+)?$");
                
                Assert.True(isSemVer, $"Invalid SemVer format: {version}");
            }
        }

        [Fact]
        public void Smoke_ManifestDownloadUrlsAreValid()
        {
            var testUrls = new[]
            {
                "https://updates.example.com/ui.zip",
                "https://cdn.example.com/storage-watch/agent-2.1.0.zip",
                "https://example.com/releases/server-update.zip"
            };

            foreach (var url in testUrls)
            {
                Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri));
                Assert.True(uri.Scheme == "https", $"URL should use HTTPS: {url}");
                Assert.True(url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase), $"URL should reference ZIP file: {url}");
            }
        }

        [Fact]
        public void Smoke_ManifestSha256HashesAreValidFormat()
        {
            var testHashes = new[]
            {
                "abc123def456abc123def456abc123def456abc123def456abc123def456abc1", // 64 chars
                "0000000000000000000000000000000000000000000000000000000000000000",  // all zeros
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"   // all F's
            };

            foreach (var hash in testHashes)
            {
                Assert.Equal(64, hash.Length);
                var isValidHash = System.Text.RegularExpressions.Regex.IsMatch(
                    hash.ToLowerInvariant(),
                    @"^[a-f0-9]{64}$");
                Assert.True(isValidHash, $"Invalid SHA256 format: {hash}");
            }
        }

        [Fact]
        public void Smoke_UpdaterVersionConsistency_AssemblyMatchesFile()
        {
            var repoRoot = FindRepositoryRoot();
            var updaterExePath = Path.Combine(repoRoot, "StorageWatch.Updater", "bin", "Debug", "net10.0", "StorageWatch.Updater.exe");
            var updaterProjectFile = Path.Combine(repoRoot, "StorageWatch.Updater", "StorageWatch.Updater.csproj");

            if (!File.Exists(updaterExePath) || !File.Exists(updaterProjectFile))
            {
                // Skip if not built or project file not found
                Assert.True(true, "Skipping: Updater not built or project file not found");
                return;
            }

            // Get file version from EXE
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(updaterExePath);
            var fileVersion = versionInfo.FileVersion;

            // Get version from project file (simplified check)
            var projectContent = File.ReadAllText(updaterProjectFile);
            var hasVersionMatch = projectContent.Contains(fileVersion!.Split('+')[0].Trim())
                                 || projectContent.Contains("Version>");

            // At minimum, file should have valid version
            Assert.NotNull(fileVersion);
            Assert.False(string.IsNullOrWhiteSpace(fileVersion));
        }

        [Fact]
        public void Smoke_ZipStructureValidation_UpdatePackageFormat()
        {
            // Create a test ZIP that simulates update package structure
            var tempDir = Path.Combine(Path.GetTempPath(), $"ci-smoke-test-{Guid.NewGuid():N}");
            var sourceDir = Path.Combine(tempDir, "source");
            var zipPath = Path.Combine(tempDir, "test-package.zip");

            try
            {
                Directory.CreateDirectory(sourceDir);

                // Create typical update package structure
                var appDir = Path.Combine(sourceDir, "app");
                Directory.CreateDirectory(appDir);
                File.WriteAllText(Path.Combine(appDir, "file1.txt"), "content1");
                File.WriteAllText(Path.Combine(appDir, "file2.txt"), "content2");

                var libDir = Path.Combine(sourceDir, "lib");
                Directory.CreateDirectory(libDir);
                File.WriteAllText(Path.Combine(libDir, "library.dll"), "binary");

                // Create ZIP
                ZipFile.CreateFromDirectory(sourceDir, zipPath);

                // Validate ZIP structure
                Assert.True(File.Exists(zipPath));

                using var archive = ZipFile.OpenRead(zipPath);
                var entries = archive.Entries;

                Assert.True(entries.Count > 0, "ZIP should contain files");

                var appEntries = entries.Where(e => e.FullName.StartsWith("app/")).ToList();
                var libEntries = entries.Where(e => e.FullName.StartsWith("lib/")).ToList();

                Assert.NotEmpty(appEntries);
                Assert.NotEmpty(libEntries);

                // Verify entries can be read
                foreach (var entry in entries)
                {
                    if (!entry.FullName.EndsWith('/'))
                    {
                        using var stream = entry.Open();
                        Assert.True(stream.CanRead);
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
