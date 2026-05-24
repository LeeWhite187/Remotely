using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Extensions;
using Remotely.Shared.Models;

namespace Remotely.Server.API;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(
        IDataService dataService,
        ILogger<DevicesController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    /// <summary>
    /// Per KD-06: target org is supplied per request as the explicit
    /// <c>organizationId</c> query parameter (FR-25). The previous header-based
    /// scheme (populated by ApiAuthorizationFilter from a per-token OrganizationID)
    /// is gone — see FR-28.
    /// </summary>
    [HttpGet]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IEnumerable<Device>> Get([FromQuery] string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Array.Empty<Device>();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (!userResult.IsSuccess)
            {
                return Array.Empty<Device>();
            }

            var isOrgAdmin = await IsOrgAdminAsync(userResult.Value, organizationId);
            return _dataService.GetDevicesForUser($"{User.Identity.Name}", organizationId, isOrgAdmin);
        }

        // Authorized with API key — return all devices for the org.
        return _dataService.GetAllDevices(organizationId);
    }

    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    [HttpGet("{id}")]
    public async Task<ActionResult<Device>> Get(string id, [FromQuery] string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            _logger.LogResult(userResult);

            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }

            var isOrgAdmin = await IsOrgAdminAsync(userResult.Value, organizationId);

            if (!_dataService.DoesUserHaveAccessToDevice(id, userResult.Value, organizationId, isOrgAdmin))
            {
                return Unauthorized();
            }
        }

        var deviceResult = await _dataService.GetDevice(organizationId, id);
        _logger.LogResult(deviceResult);

        if (!deviceResult.IsSuccess)
        {
            return NotFound();
        }

        return deviceResult.Value;
    }

    [HttpPut]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> Update([FromBody] DeviceSetupOptions deviceOptions, [FromQuery] string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(deviceOptions?.DeviceID))
        {
            return BadRequest("DeviceId is required.");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            _logger.LogResult(userResult);

            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }

            var isOrgAdmin = await IsOrgAdminAsync(userResult.Value, organizationId);

            if (!_dataService.DoesUserHaveAccessToDevice(deviceOptions.DeviceID, userResult.Value, organizationId, isOrgAdmin))
            {
                return Unauthorized();
            }
        }

        var deviceResult = await _dataService.UpdateDevice(deviceOptions, organizationId);
        _logger.LogResult(deviceResult);

        if (!deviceResult.IsSuccess)
        {
            return BadRequest();
        }
        return Created(Request.GetDisplayUrl(), deviceResult.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] DeviceSetupOptions deviceOptions)
    {
        var result = await _dataService.CreateDevice(deviceOptions);
        _logger.LogResult(result);

        if (!result.IsSuccess)
        {
            return BadRequest("Device already exists.  Use Put with authorization to update the device.");
        }
        return Created(Request.GetDisplayUrl(), result.Value);
    }

    /// <summary>
    /// FR-18 / KD-02: server admin implies org admin everywhere; otherwise check
    /// the membership record for the active org.
    /// </summary>
    private async Task<bool> IsOrgAdminAsync(RemotelyUser user, string organizationId)
    {
        if (user.IsServerAdmin)
        {
            return true;
        }
        var membership = await _dataService.GetMembership(organizationId, user.Id);
        return membership?.IsAdministrator == true;
    }
}
