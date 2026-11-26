using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using QuizFlow.Api.Services;
using QuizFlow.Infrastructure.Identity;
using QuizFlow.Infrastructure.Persistence;

namespace QuizFlow.Api.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/v1/auth")]
public sealed class AuthController(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    QuizFlowDbContext db,
    JwtTokenService tokens,
    IAccountEmailSender emailSender,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    public sealed record RegisterRequest([Required, EmailAddress] string Email, [Required, MinLength(12)] string Password,
        [Required] string ConfirmPassword, [Required, MaxLength(100)] string FirstName,
        [Required, MaxLength(100)] string LastName);
    public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
    public sealed record UpdateProfileRequest([Required, MaxLength(100)] string FirstName, [Required, MaxLength(100)] string LastName);
    public sealed record ChangePasswordRequest([Required] string CurrentPassword, [Required, MinLength(12)] string NewPassword);
    public sealed record SetPasswordRequest([Required, MinLength(12)] string NewPassword);
    public sealed record ChangeEmailRequest([Required, EmailAddress] string NewEmail, [Required] string CurrentPassword);
    public sealed record ConfirmEmailChangeRequest([Required, EmailAddress] string Email, [Required, EmailAddress] string NewEmail, [Required] string Token);
    public sealed record EmailRequest([Required, EmailAddress] string Email);
    public sealed record OneTimeTokenRequest([Required, EmailAddress] string Email, [Required] string Token);
    public sealed record ResetPasswordRequest([Required, EmailAddress] string Email, [Required] string Token,
        [Required, MinLength(12)] string NewPassword);
    public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return BadRequest(new { errors = new[] { "Password and confirmation must match." } });
        var user = new AppUser { Id = Guid.NewGuid(), UserName = request.Email, Email = request.Email,
            FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim() };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildAccountLink("verify-email", user.Email!, token);
        await emailSender.SendAsync(user.Email!, "Verify your QuizFlow email", $"<p>Confirm your email by <a href=\"{link}\">verifying your account</a>.</p>", HttpContext.RequestAborted);
        logger.LogInformation("Created account {UserId}; verification email sent", user.Id);
        return Accepted(new { message = "Check your email for a verification link." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !await users.IsEmailConfirmedAsync(user) ||
            !(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
            return Unauthorized(new { message = "Invalid credentials or unverified account." });
        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("QuizFlow.Refresh", out var rawToken) || string.IsNullOrWhiteSpace(rawToken))
            return Unauthorized();
        var hash = JwtTokenService.Hash(rawToken);
        var oldToken = await db.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash);
        if (oldToken is null || oldToken.RevokedAt is not null || oldToken.ExpiresAt <= DateTimeOffset.UtcNow)
            return Unauthorized();

        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await users.FindByIdAsync(oldToken.UserId.ToString());
        if (user is null) return Unauthorized();
        var replacement = JwtTokenService.NewRefreshToken();
        var replacementHash = JwtTokenService.Hash(replacement);
        var refreshedUntil = DateTimeOffset.UtcNow.AddDays(30);
        oldToken.RevokedAt = DateTimeOffset.UtcNow;
        oldToken.ReplacedByTokenHash = replacementHash;
        oldToken.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = replacementHash,
            ExpiresAt = refreshedUntil });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Unauthorized();
        }
        await transaction.CommitAsync();
        var (access, expires) = await tokens.CreateAccessTokenAsync(user);
        SetRefreshCookie(replacement, refreshedUntil);
        return Ok(new TokenResponse(access, expires));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue("QuizFlow.Refresh", out var rawToken) && !string.IsNullOrWhiteSpace(rawToken))
        {
            var token = await db.RefreshTokens.SingleOrDefaultAsync(item => item.TokenHash == JwtTokenService.Hash(rawToken));
        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        }
        Response.Cookies.Delete("QuizFlow.Refresh", new CookieOptions { Path = "/api/v1/auth", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var activeTokens = await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null).ToListAsync();
        foreach (var token in activeTokens) token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Response.Cookies.Delete("QuizFlow.Refresh", new CookieOptions { Path = "/api/v1/auth", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(new { user.Id, user.Email, user.FirstName, user.LastName });
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        var result = await users.UpdateAsync(user);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors.Select(error => error.Description) });
    }

    [HttpPost("change-email")]
    [Authorize]
    public async Task<IActionResult> ChangeEmail(ChangeEmailRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var passwordCheck = await signIn.CheckPasswordSignInAsync(user, request.CurrentPassword, lockoutOnFailure: true);
        if (!passwordCheck.Succeeded) return Unauthorized(new { message = "Current password is incorrect." });
        if (await users.FindByEmailAsync(request.NewEmail) is not null || await users.FindByNameAsync(request.NewEmail) is not null)
            return Conflict(new { message = "That email address cannot be used." });
        var token = await users.GenerateChangeEmailTokenAsync(user, request.NewEmail);
        var link = BuildAccountLink("confirm-email-change", user.Email!, token, request.NewEmail);
        await emailSender.SendAsync(request.NewEmail, "Confirm your new QuizFlow email", $"<p>Confirm this email address by <a href=\"{link}\">opening this link</a>.</p>", HttpContext.RequestAborted);
        return Accepted(new { message = "Check your new email address to confirm the change." });
    }

    [HttpPost("confirm-email-change")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmailChange(ConfirmEmailChangeRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || await users.FindByEmailAsync(request.NewEmail) is not null || await users.FindByNameAsync(request.NewEmail) is not null)
            return BadRequest(new { message = "This email change link is invalid or has expired." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var emailResult = await users.ChangeEmailAsync(user, request.NewEmail, request.Token);
        if (!emailResult.Succeeded) return BadRequest(new { message = "This email change link is invalid or has expired." });
        var usernameResult = await users.SetUserNameAsync(user, request.NewEmail);
        if (!usernameResult.Succeeded)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = "This email change link is invalid or has expired." });
        }
        var activeTokens = await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null).ToListAsync();
        foreach (var token in activeTokens) token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        var activeTokens = await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null).ToListAsync();
        foreach (var token in activeTokens) token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Response.Cookies.Delete("QuizFlow.Refresh", new CookieOptions { Path = "/api/v1/auth", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });
        return NoContent();
    }

    [HttpPost("set-password")]
    [Authorize]
    public async Task<IActionResult> SetPassword(SetPasswordRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        if (await users.HasPasswordAsync(user)) return Conflict(new { message = "A password is already set. Use change-password instead." });
        var result = await users.AddPasswordAsync(user, request.NewPassword);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors.Select(error => error.Description) });
    }

    [HttpDelete("google")]
    [Authorize]
    public async Task<IActionResult> UnlinkGoogle()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var logins = await users.GetLoginsAsync(user);
        var googleLogins = logins.Where(login => login.LoginProvider == "Google").ToList();
        if (googleLogins.Count == 0) return NotFound();
        if (!await users.HasPasswordAsync(user) && logins.Count == googleLogins.Count)
            return Conflict(new { message = "Add a password or another sign-in method before unlinking Google." });
        foreach (var login in googleLogins)
        {
            var result = await users.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
            if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        }
        return NoContent();
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(OneTimeTokenRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return BadRequest(new { message = "Invalid or expired verification token." });
        var result = await users.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded ? NoContent() : BadRequest(new { message = "Invalid or expired verification token." });
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification(EmailRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null && !await users.IsEmailConfirmedAsync(user))
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            var link = BuildAccountLink("verify-email", user.Email!, token);
            await emailSender.SendAsync(user.Email!, "Verify your QuizFlow email", $"<p>Confirm your email by <a href=\"{link}\">verifying your account</a>.</p>", HttpContext.RequestAborted);
        }
        return Accepted(new { message = "If the account needs verification, an email will be sent." });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(EmailRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is not null && await users.IsEmailConfirmedAsync(user))
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = BuildAccountLink("reset-password", user.Email!, token);
            await emailSender.SendAsync(user.Email!, "Reset your QuizFlow password", $"<p>Reset your password by <a href=\"{link}\">opening this secure link</a>.</p>", HttpContext.RequestAborted);
        }
        return Accepted(new { message = "If the account exists, password reset instructions will be sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return BadRequest(new { message = "Invalid or expired reset token." });
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors.Select(error => error.Description) });
    }

    [HttpGet("google")]
    [AllowAnonymous]
    public IActionResult Google()
    {
        var clientId = configuration["Google:ClientId"];
        var clientSecret = configuration["Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return Problem("Google sign-in is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        return Challenge(new AuthenticationProperties { RedirectUri = "/api/v1/auth/google/complete" }, "Google");
    }

    [HttpGet("google/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleComplete()
    {
        var external = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!external.Succeeded || external.Principal is null) return Unauthorized();
        var email = external.Principal.FindFirst("email")?.Value;
        var subject = external.Principal.FindFirst("sub")?.Value;
        var verified = external.Principal.FindFirst("email_verified")?.Value;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(subject) || !bool.TryParse(verified, out var isVerified) || !isVerified)
            return Unauthorized(new { message = "Google did not provide a verified email identity." });
        var user = await users.FindByLoginAsync("Google", subject);
        if (user is null)
        {
            user = await users.FindByEmailAsync(email);
            if (user is not null && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) return Unauthorized();
            if (user is null)
            {
                user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
                    FirstName = external.Principal.FindFirst("given_name")?.Value ?? "", LastName = external.Principal.FindFirst("family_name")?.Value ?? "" };
                var created = await users.CreateAsync(user);
                if (!created.Succeeded) return BadRequest(new { errors = created.Errors.Select(error => error.Description) });
            }
            else if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                var confirmed = await users.UpdateAsync(user);
                if (!confirmed.Succeeded) return BadRequest(new { errors = confirmed.Errors.Select(error => error.Description) });
            }
            var linked = await users.AddLoginAsync(user, new UserLoginInfo("Google", subject, "Google"));
            if (!linked.Succeeded) return BadRequest(new { errors = linked.Errors.Select(error => error.Description) });
        }
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        var result = await IssueTokensAsync(user);
        var publicUrl = configuration["App:PublicUrl"] ?? throw new InvalidOperationException("App:PublicUrl must be configured.");
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var webUri)) return Problem("App:PublicUrl must be an absolute URL.");
        var payload = System.Text.Json.JsonSerializer.Serialize(new { type = "quizflow:auth", accessToken = result.AccessToken, expiresAt = result.ExpiresAt });
        var targetOrigin = System.Text.Json.JsonSerializer.Serialize(webUri.GetLeftPart(UriPartial.Authority));
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'unsafe-inline'; base-uri 'none'; frame-ancestors 'none'";
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return Content($"<!doctype html><html><head><meta charset=\"utf-8\"><title>QuizFlow sign-in</title></head><body><p>Signed in. This window will close.</p><script>window.opener?.postMessage({payload}, {targetOrigin});window.close();</script></body></html>", "text/html");
    }

    private async Task<TokenResponse> IssueTokensAsync(AppUser user)
    {
        var (access, expires) = await tokens.CreateAccessTokenAsync(user);
        var raw = JwtTokenService.NewRefreshToken();
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = JwtTokenService.Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
        await db.SaveChangesAsync();
        SetRefreshCookie(raw, DateTimeOffset.UtcNow.AddDays(30));
        return new TokenResponse(access, expires);
    }

    private void SetRefreshCookie(string raw, DateTimeOffset expiresAt) => Response.Cookies.Append("QuizFlow.Refresh", raw,
        new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/v1/auth", Expires = expiresAt });

    private string BuildAccountLink(string page, string email, string token, string? newEmail = null)
    {
        var webUrl = configuration["App:PublicUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("App:PublicUrl must be configured to send account emails.");
        var extra = newEmail is null ? string.Empty : $"&newEmail={Uri.EscapeDataString(newEmail)}";
        return $"{webUrl}/auth/{page}?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}{extra}";
    }
}
