using Jellyfin.Plugin.BroadcastBox.Models;
using Jellyfin.Plugin.BroadcastBox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.BroadcastBox.Api;

/// <summary>Endpoints for observing and controlling Broadcast Box publishing.</summary>
[ApiController]
[Authorize]
[Route("BroadcastBox")]
public sealed class BroadcastController : ControllerBase
{
    private readonly BroadcastSessionManager _sessionManager;

    /// <summary>Initializes a new instance of the <see cref="BroadcastController"/> class.</summary>
    public BroadcastController(BroadcastSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    /// <summary>Gets safe state for the current publishing session.</summary>
    [HttpGet("session")]
    [ProducesResponseType<BroadcastSessionSnapshot>(StatusCodes.Status200OK)]
    public ActionResult<BroadcastSessionSnapshot> GetSession() => Ok(_sessionManager.GetSnapshot());
}
