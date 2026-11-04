using Logistics_Assistant.DTOs.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Logistics_Assistant.Controllers;

[ApiController]
[Route("api/auth")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult Csrf() => Ok(new
    {
        token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken
    });

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserResponse>> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(ToResponse(user));
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthUserResponse>> Register(RegisterRequest request)
    {
        var email = request.Email.Trim();
        var user = new IdentityUser { UserName = email, Email = email };
        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { message = "Пользователь с таким email уже зарегистрирован." });
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
                return Conflict(new { message = "Пользователь с таким email уже зарегистрирован." });

            return BadRequest(new { message = "Не удалось зарегистрироваться. Проверьте email и пароль (от 8 символов)." });
        }

        // Registration signs in immediately; no email or phone confirmation is required.
        await signIn.SignInAsync(user, isPersistent: false);
        return Ok(ToResponse(user));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthUserResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return Unauthorized(new { message = "Неверный email или пароль." });

        var result = await signIn.PasswordSignInAsync(
            user, request.Password, request.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "Слишком много попыток входа. Попробуйте через 5 минут." });

        if (!result.Succeeded)
            return Unauthorized(new { message = "Неверный email или пароль." });

        return Ok(ToResponse(user));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    private static AuthUserResponse ToResponse(IdentityUser user) => new(user.Id, user.Email ?? "");
}
