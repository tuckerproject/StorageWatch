/// <summary>
/// Integration Tests for Network Readiness Checks
/// 
/// Tests the network readiness logic used by NotificationLoop to verify connectivity
/// before sending alerts. These tests verify DNS resolution and network availability checks.
/// </summary>

using FluentAssertions;
using System.Net;

namespace StorageWatch.Tests.IntegrationTests
{
    public class NetworkReadinessTests
    {
        [Fact]
        public void DnsGetHostEntry_WithValidHostname_Succeeds()
        {
            // Act
            Action act = () => Dns.GetHostEntry("google.com");

            // Assert
            act.Should().NotThrow("DNS resolution should succeed for valid hostnames when network is available");
        }

        [Fact]
        public void DnsGetHostEntry_WithInvalidHostname_ThrowsException()
        {
            // Act
            Action act = () => Dns.GetHostEntry("this-hostname-definitely-does-not-exist-12345.invalid");

            // Assert
            act.Should().Throw<Exception>("DNS resolution should fail for invalid hostnames");
        }

        [Fact]
        public void DnsGetHostEntry_WithGroupMeApiHostname_Succeeds()
        {
            // This is the actual hostname used in NotificationLoop for network readiness checks
            // Act
            Action act = () => Dns.GetHostEntry("api.groupme.com");

            // Assert
            act.Should().NotThrow("DNS resolution should succeed for GroupMe API hostname when network is available");
        }

        [Fact]
        public void DnsGetHostEntry_ReturnsHostInformation()
        {
            // Act
            var hostEntry = Dns.GetHostEntry("google.com");

            // Assert
            hostEntry.Should().NotBeNull();
            hostEntry.HostName.Should().NotBeNullOrEmpty();
            hostEntry.AddressList.Should().NotBeEmpty("At least one IP address should be resolved");
        }

        [Fact]
        public async Task DnsGetHostEntryAsync_WithValidHostname_Succeeds()
        {
            // Act
            Func<Task> act = async () => await Dns.GetHostEntryAsync("google.com");

            // Assert
            await act.Should().NotThrowAsync("Async DNS resolution should succeed for valid hostnames");
        }

        [Fact]
        public void NetworkReadinessCheck_SimulatesNotificationLoopLogic()
        {
            // This test simulates the exact logic used in NotificationLoop.NetworkReady()
            // Arrange
            bool networkReady = false;

            // Act
            try
            {
                Dns.GetHostEntry("api.groupme.com");
                networkReady = true;
            }
            catch
            {
                networkReady = false;
            }

            // Assert - On a machine with internet, this should be true
            // On offline machines, this will be false (which is correct behavior)
            networkReady.Should().BeTrue("Network should be ready when running tests with internet connectivity");
        }

        [Fact]
        public void NetworkReadinessCheck_WithLocalhost_AlwaysSucceeds()
        {
            // Act
            Action act = () => Dns.GetHostEntry("localhost");

            // Assert
            act.Should().NotThrow("localhost should always resolve");
        }

        [Fact]
        public void NetworkReadinessCheck_With127001_AlwaysSucceeds()
        {
            // Act
            Action act = () => Dns.GetHostEntry("127.0.0.1");

            // Assert
            act.Should().NotThrow("127.0.0.1 should always resolve");
        }
    }
}
