// StudyApp.Api/Controllers/AuthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudyApp.Application.Auth;

namespace StudyApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private const string SubjectClaim = "sub";

    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        try
        {
            return Success(await _auth.RegisterAsync(request), 201);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(400, ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        return result is null ? Fail(401, "Invalid email or password.") : Success(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (CurrentUserId() is not { } userId)
            return Fail(401, "Invalid token subject.");

        var user = await _auth.GetUserAsync(userId);
        return user is null ? Fail(404, "User not found.") : Success(user);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (CurrentUserId() is not { } userId)
            return Fail(401, "Invalid token subject.");

        var changed = await _auth.ChangePasswordAsync(userId, request);
        return changed
            ? Success<object?>(null, 204)
            : Fail(400, "Current password is incorrect or the new password is not allowed.");
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(SubjectClaim)?.Value, out var id) ? id : null;
}