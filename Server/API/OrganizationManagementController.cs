using Remotely.Shared.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.ViewModels;
using System.Text;
using System.Text.Encodings.Web;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class OrganizationManagementController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly IEmailSenderEx _emailSender;
    private readonly ILogger<OrganizationManagementController> _logger;
    private readonly UserManager<RemotelyUser> _userManager;

    public OrganizationManagementController(
        UserManager<RemotelyUser> userManager,
        IDataService dataService,
        IEmailSenderEx emailSender,
        ILogger<OrganizationManagementController> logger)
    {
        _dataService = dataService;
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpPost("ChangeIsAdmin/{userID}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> ChangeIsAdmin(string userId, [FromBody] bool isAdmin, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (userResult.IsSuccess && userResult.Value.Id == userId)
            {
                return BadRequest("You can't remove administrator rights from yourself.");
            }
        }

        // §8.1 + KD-04: superseded by SetMemberIsAdmin (enforces FR-15 lockout).
        var setResult = await _dataService.SetMemberIsAdmin(orgId, userId, isAdmin);
        if (!setResult.IsSuccess)
        {
            return BadRequest(setResult.Reason);
        }
        return NoContent();
    }

    [HttpDelete("DeleteInvite/{inviteID}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> DeleteInvite(string inviteID, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        var result = await _dataService.DeleteInvite(orgId, inviteID);
        _logger.LogResult(result);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Reason);
        }

        return NoContent();
    }

    [HttpDelete("DeleteUser/{userID}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> DeleteUser(string userId, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");
            if (userResult.IsSuccess && userResult.Value.Id == userId)
            {
                return BadRequest("You can't delete yourself here.  You must go to the Personal Data page to delete your own account.");
            }
        }


        var result = await _dataService.DeleteUser(orgId, userId);
        _logger.LogResult(result);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Reason);
        }

        return NoContent();
    }

    [HttpGet("DeviceGroup")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public IActionResult DeviceGroup([FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        return Ok(_dataService.GetDeviceGroupsForOrganization(orgId));
    }

    [HttpDelete("DeviceGroup")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> DeviceGroup([FromBody] string deviceGroupId, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        var result = await _dataService.DeleteDeviceGroup(orgId, deviceGroupId.Trim());
        _logger.LogResult(result);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Reason);
        }
        return NoContent();
    }

    [HttpPost("DeviceGroup")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> DeviceGroup([FromBody] DeviceGroup deviceGroup, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var result = await _dataService.AddDeviceGroup(orgId, deviceGroup);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Reason);
        }
        return Ok(result.Value.ID);
    }

    [HttpDelete("DeviceGroup/{groupID}/Users/")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> DeviceGroupRemoveUser([FromBody] string userID, string groupID, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        if (!await _dataService.RemoveUserFromDeviceGroup(orgId, groupID, userID))
        {
            return BadRequest("Failed to remove user from group.");
        }
        return Ok();
    }

    [HttpPost("DeviceGroup/{groupID}/Users/")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public IActionResult DeviceGroupAddUser([FromBody] string userID, string groupID, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var result = _dataService.AddUserToDeviceGroup(orgId, groupID, userID, out var resultMessage);
        if (!result)
        {
            return BadRequest(resultMessage);
        }

        return Ok(resultMessage);
    }

    [HttpGet("GenerateResetUrl/{userID}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> GenerateResetUrl(string userId, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return NotFound();
        }

        // Multi-tenant refactor: confirm the user is a member of the target org.
        var membership = await _dataService.GetMembership(orgId, user.Id);
        if (membership is null && !user.IsServerAdmin)
        {
            return Unauthorized();
        }

        var code = await _userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page(
            "/Account/ResetPassword",
            pageHandler: null,
            values: new { area = "Identity", code },
            protocol: Request.Scheme);

        return Ok(callbackUrl);

    }

    [HttpPut("Name")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> Name([FromBody] string organizationName, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        if (organizationName.Length > 25)
        {
            return BadRequest();
        }

        var result = await _dataService.UpdateOrganizationName(orgId, organizationName.Trim());
        _logger.LogResult(result);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Reason);
        }
        return NoContent();
    }

    [HttpPut("SetDefault")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> SetDefault([FromBody] bool isDefault, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        await _dataService.SetIsDefaultOrganization(orgId, isDefault);
        return NoContent();
    }

    [HttpPost("SendInvite")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> SendInvite([FromBody] InviteViewModel invite, [FromQuery] string organizationId)
    {
        // KD-06: org id is supplied per request as the explicit organizationId query param.
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return Unauthorized();
        }
        var orgId = organizationId;

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(invite.InvitedUser))
        {
            return BadRequest();
        }


        // KD-03: invitations are reserved for new (not-yet-registered) users.
        // Existing users are directly added to the org via FR-08 — no email.
        if (_dataService.DoesUserExist(invite.InvitedUser))
        {
            var existing = await _dataService.GetUserByName(invite.InvitedUser);
            if (!existing.IsSuccess)
            {
                return BadRequest("Existing user lookup failed.");
            }
            var addResult = await _dataService.AddUserToOrganization(orgId, existing.Value.Id);
            if (!addResult.IsSuccess)
            {
                return BadRequest(addResult.Reason);
            }
            return Ok();
        }

        // KD-04: invitees always receive base privileges (IsAdmin removed from InviteViewModel).
        var newInvite = await _dataService.AddInvite(orgId, invite);
        if (!newInvite.IsSuccess)
        {
            return BadRequest(newInvite.Reason);
        }

        var inviteURL = $"{Request.Scheme}://{Request.Host}/Invite/{newInvite.Value.ID}";
        var emailResult = await _emailSender.SendEmailAsync(
            invite.InvitedUser,
            "Invitation to Organization in Remotely",
            $@"<img src='{Request.Scheme}://{Request.Host}/images/Remotely_Logo.png'/>
                <br><br>
                Hello!
                <br><br>
                You've been invited to join an organization in Remotely.
                <br><br>
                You can join the organization by <a href='{HtmlEncoder.Default.Encode(inviteURL)}'>clicking here</a>.",
            null);

        if (!emailResult)
        {
            return Problem("There was an error sending the invitation email.");
        }

        return Ok();
    }
}
