using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ognia.Server.DTOs.Login;
using Ognia.Server.DTOs.Password;
using Ognia.Server.DTOs.Register;
using Ognia.Server.Models;
using Ognia.Server.Services.TokenService;
using System.Security.Claims;

namespace Ognia.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUserModel> _userManager;
    private readonly SignInManager<AppUserModel> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly JwtSettingsModel _jwtSettings;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        UserManager<AppUserModel> userManager,
        SignInManager<AppUserModel> signInManager,
        ITokenService tokenService,
        IOptions<JwtSettingsModel> jwtOptions,
        IWebHostEnvironment env)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _jwtSettings = jwtOptions.Value;
        _env = env;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            return Conflict(new { message = "An account with this email already exists." });

        var user = new AppUserModel { Email = request.Email, UserName = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Ok(new { message = "Account created." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = await _tokenService.IssueRefreshTokenAsync(user.Id);

        return Ok(new LoginResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            Email = user.Email!,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { message = "Refresh token is required." });

        var result = await _tokenService.ValidateAndRotateAsync(request.RefreshToken);
        if (result == null)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        var (user, newRefreshToken) = result.Value;
        var accessToken = _tokenService.GenerateAccessToken(user);

        return Ok(new LoginResponse
        {
            Token = accessToken,
            RefreshToken = newRefreshToken,
            Email = user.Email!,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            await _tokenService.RevokeAsync(request.RefreshToken);

        return Ok(new { message = "Logged out." });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            email = User.FindFirst(ClaimTypes.Email)?.Value,
            userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Ok(new { message = "Password changed." });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
            return Ok(new { message = "If that email exists, a reset link has been sent." });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        if (_env.IsDevelopment())
            Console.WriteLine($"\n=== PASSWORD RESET TOKEN for {request.Email} ===\n{token}\n=================================================\n");

        return Ok(new { message = "If that email exists, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return BadRequest(new { message = "Invalid request." });

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Ok(new { message = "Password reset." });
    }
}