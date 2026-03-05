using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using StorageWatchServer.Server.Data;
using StorageWatchServer.Server.Models;
using StorageWatchServer.Server.Services;

namespace StorageWatchServer.Server.Api;

/// <summary>
/// API endpoints for the StorageWatch Server.
/// These have been replaced with dedicated controllers and removed legacy endpoints.
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// Maps agent endpoints - preserved for any future use, but actual routing
    /// now happens through RawRowsController and other dedicated controllers.
    /// </summary>
    public static void MapAgentEndpoints(this RouteGroupBuilder group)
    {
        // All endpoints are now handled through dedicated controllers
        // RawRowsController handles POST /api/agent/report
    }
}
