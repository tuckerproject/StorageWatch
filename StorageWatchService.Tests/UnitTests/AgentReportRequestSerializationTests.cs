/// <summary>
/// Serialization Tests for AgentReportRequest
/// 
/// Validates JSON serialization and deserialization behavior for the Agent -> Server API contract.
/// </summary>

using FluentAssertions;
using StorageWatch.Models;
using System.Text.Json;

namespace StorageWatch.Tests.UnitTests
{
    public class AgentReportRequestSerializationTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        [Fact]
        public void Serialize_UsesCamelCasePropertyNames()
        {
            var request = CreateSampleRequest();
            var json = JsonSerializer.Serialize(request, JsonOptions);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            root.TryGetProperty("agentId", out _).Should().BeTrue();
            root.TryGetProperty("timestampUtc", out _).Should().BeTrue();
            root.TryGetProperty("drives", out var drives).Should().BeTrue();
            root.TryGetProperty("alerts", out var alerts).Should().BeTrue();

            drives.ValueKind.Should().Be(JsonValueKind.Array);
            alerts.ValueKind.Should().Be(JsonValueKind.Array);

            var drive = drives[0];
            drive.TryGetProperty("driveLetter", out _).Should().BeTrue();
            drive.TryGetProperty("totalSpaceGb", out _).Should().BeTrue();
            drive.TryGetProperty("freeSpaceGb", out _).Should().BeTrue();
            drive.TryGetProperty("usedPercent", out _).Should().BeTrue();

            var alert = alerts[0];
            alert.TryGetProperty("driveLetter", out _).Should().BeTrue();
            alert.TryGetProperty("level", out _).Should().BeTrue();
            alert.TryGetProperty("message", out _).Should().BeTrue();
        }

        [Fact]
        public void Serialize_IncludesRequiredFields()
        {
            var request = CreateSampleRequest();
            var json = JsonSerializer.Serialize(request, JsonOptions);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            root.GetProperty("agentId").GetString().Should().Be(request.AgentId);
            root.GetProperty("timestampUtc").GetDateTime().Should().Be(request.TimestampUtc);
            root.GetProperty("drives").GetArrayLength().Should().Be(1);
            root.GetProperty("alerts").GetArrayLength().Should().Be(1);
        }

        [Fact]
        public void RoundTrip_SerializesAndDeserializes()
        {
            var request = CreateSampleRequest();
            var json = JsonSerializer.Serialize(request, JsonOptions);

            var roundTrip = JsonSerializer.Deserialize<AgentReportRequest>(json, JsonOptions);

            roundTrip.Should().NotBeNull();
            roundTrip!.AgentId.Should().Be(request.AgentId);
            roundTrip.TimestampUtc.Should().Be(request.TimestampUtc);

            roundTrip.Drives.Should().HaveCount(1);
            roundTrip.Drives[0].DriveLetter.Should().Be(request.Drives[0].DriveLetter);
            roundTrip.Drives[0].TotalSpaceGb.Should().Be(request.Drives[0].TotalSpaceGb);
            roundTrip.Drives[0].FreeSpaceGb.Should().Be(request.Drives[0].FreeSpaceGb);
            roundTrip.Drives[0].UsedPercent.Should().Be(request.Drives[0].UsedPercent);

            roundTrip.Alerts.Should().HaveCount(1);
            roundTrip.Alerts[0].DriveLetter.Should().Be(request.Alerts[0].DriveLetter);
            roundTrip.Alerts[0].Level.Should().Be(request.Alerts[0].Level);
            roundTrip.Alerts[0].Message.Should().Be(request.Alerts[0].Message);
        }

        private static AgentReportRequest CreateSampleRequest()
        {
            return new AgentReportRequest
            {
                AgentId = "agent-123",
                TimestampUtc = DateTime.UtcNow,
                Drives =
                {
                    new DriveReportDto
                    {
                        DriveLetter = "C:",
                        TotalSpaceGb = 512,
                        FreeSpaceGb = 128,
                        UsedPercent = 75
                    }
                },
                Alerts =
                {
                    new AlertDto
                    {
                        DriveLetter = "C:",
                        Level = "Warning",
                        Message = "Low disk space"
                    }
                }
            };
        }
    }
}
