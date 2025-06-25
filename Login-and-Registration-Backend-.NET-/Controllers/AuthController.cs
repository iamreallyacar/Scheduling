using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Login_and_Registration_Backend_.NET_.Services;
using Login_and_Registration_Backend_.NET_.Models;
using AuthService = Login_and_Registration_Backend_.NET_.Services.IAuthenticationService;

namespace Login_and_Registration_Backend_.NET_.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authenticationService;
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            AuthService authenticationService,
            IUserService userService, 
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _authenticationService = authenticationService;
            _userService = userService;
            _configuration = configuration;
            _logger = logger;
        }

        private string GetFrontendUrl()
        {
            return _configuration["AppSettings:FrontendUrl"] ?? "http://localhost:5173";
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            _logger.LogDebug("Registration attempt for username: {Username}", request.Username);
            
            var result = await _authenticationService.RegisterAsync(request);
            
            if (result.Success)
            {
                _logger.LogInformation("User registered successfully: {Username}", request.Username);
                return Ok(new { 
                    message = "User registered successfully",
                    user = result.User,
                    token = result.Token
                });
            }

            _logger.LogWarning("Registration failed for username: {Username}. Error: {Error}", 
                request.Username, result.ErrorMessage);
            
            return BadRequest(new { 
                message = result.ErrorMessage,
                errors = result.Errors
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            _logger.LogDebug("Login attempt for username: {Username}", request.Username);
            
            var result = await _authenticationService.AuthenticateAsync(request);
            
            if (result.Success)
            {
                _logger.LogInformation("User logged in successfully: {Username}", request.Username);
                return Ok(new
                {
                    user = result.User,
                    token = result.Token
                });
            }

            _logger.LogWarning("Login failed for username: {Username}. Error: {Error}", 
                request.Username, result.ErrorMessage);
            
            return Unauthorized(new { message = result.ErrorMessage });
        }

        [HttpGet("test")]
        public ActionResult<string> Test()
        {
            return Ok("Server is running successfully!");
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }

                var user = await _userService.GetUserByIdAsync(userIdClaim.Value);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                return Ok(new
                {
                    user = new UserDto
                    {
                        Id = user.Id,
                        Username = user.UserName ?? string.Empty,
                        Email = user.Email ?? string.Empty
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("google-login")]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var redirectUrl = $"{Request.Scheme}://{Request.Host}/api/auth/oauth-success";
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            
            _logger.LogDebug("Initiating Google OAuth login");
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("oauth-success")]
        [AllowAnonymous]
        public async Task<IActionResult> OAuthSuccess()
        {
            try
            {
                var authenticateResult = await HttpContext.AuthenticateAsync("OAuth");
                
                if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
                {
                    _logger.LogWarning("OAuth authentication failed");
                    return Redirect($"{GetFrontendUrl()}/oauth-success?error=OAuth%20authentication%20failed");
                }

                var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
                var name = authenticateResult.Principal.FindFirst(ClaimTypes.Name)?.Value;
                var picture = authenticateResult.Principal.FindFirst("picture")?.Value;
                var googleId = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(googleId))
                {
                    _logger.LogWarning("OAuth success but missing required claims");
                    return Redirect($"{GetFrontendUrl()}/oauth-success?error=No%20email%20or%20ID%20returned%20from%20Google");
                }

                var oauthInfo = new OAuthUserInfo
                {
                    Email = email,
                    Name = name,
                    Picture = picture,
                    Provider = "Google",
                    ProviderId = googleId
                };

                var result = await _authenticationService.HandleOAuthUserAsync(oauthInfo);
                
                if (result.Success)
                {
                    _logger.LogInformation("OAuth authentication successful for user: {Email}", email);
                    return Redirect($"{GetFrontendUrl()}/oauth-success?token={result.Token}");
                }

                _logger.LogError("OAuth user handling failed: {Error}", result.ErrorMessage);
                return Redirect($"{GetFrontendUrl()}/oauth-success?error={Uri.EscapeDataString(result.ErrorMessage ?? "Authentication failed")}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OAuth success handling");
                return Redirect($"{GetFrontendUrl()}/oauth-success?error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            var token = Request.Headers["Authorization"].ToString()?.Replace("Bearer ", "");
            if (!string.IsNullOrEmpty(token))
            {
                _authenticationService.RevokeToken(token);
                _logger.LogDebug("User logged out and token revoked");
            }
            
            return Ok(new { message = "Logged out successfully" });
        }
    }
}