using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Login_and_Registration_Backend_.NET_.Services;
using Login_and_Registration_Backend_.NET_.Models;

namespace Login_and_Registration_Backend_.NET_.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;

        public AuthController(IUserService userService, IConfiguration configuration)
        {
            _userService = userService;
            _configuration = configuration;
        }

        private string GetFrontendUrl()
        {
            return _configuration["AppSettings:FrontendUrl"] ?? "http://localhost:5173";
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Check if user already exists
                var existingUserByUsername = await _userService.GetUserByUsernameAsync(request.Username);
                var existingUserByEmail = await _userService.GetUserByEmailAsync(request.Email);

                if (existingUserByUsername != null || existingUserByEmail != null)
                {
                    return BadRequest(new { message = "Username or email already exists" });
                }

                // Register user using Identity
                var result = await _userService.RegisterUserAsync(request);
                
                if (result.Succeeded)
                {
                    return Ok(new { message = "User registered successfully" });
                }

                return BadRequest(new { 
                    message = "Registration failed", 
                    errors = result.Errors.Select(e => e.Description) 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Registration error: " + ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var (success, user) = await _userService.ValidateUserAsync(request);
                
                if (!success || user == null)
                {
                    return Unauthorized(new { message = "Invalid username or password" });
                }

                var token = _userService.GenerateJwtToken(user);

				return Ok(new
				{
					User = new UserDto
					{
						Id = user.Id,
						Username = user.UserName ?? string.Empty,
						Email = user.Email ?? string.Empty
					},
					Token = token
				});
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("oauth-success")]
        [AllowAnonymous]
        public async Task<IActionResult> OAuthSuccess()
        {
            try
            {                var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
                {
                    return Redirect($"{GetFrontendUrl()}/oauth-success?error=OAuth%20authentication%20failed");
                }

                var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
                var name = authenticateResult.Principal.FindFirst(ClaimTypes.Name)?.Value;
                
                if (string.IsNullOrEmpty(email))
                {
                    return Redirect($"{GetFrontendUrl()}/oauth-success?error=No%20email%20returned%20from%20Google");
                }// Find or create user
                var user = await _userService.GetUserByEmailAsync(email);
                if (user == null)
                {
                    // Generate a unique username by sanitizing the display name
                    var baseUsername = SanitizeUsername(name ?? email.Split('@')[0]);
                    var username = baseUsername;
                    var counter = 1;
                    
                    // Check if username exists and make it unique
                    while (await _userService.GetUserByUsernameAsync(username) != null)
                    {
                        username = $"{baseUsername}{counter}";
                        counter++;
                    }
                    
                    var registerRequest = new RegisterRequest
                    {
                        Username = username,
                        Email = email,
                        Password = GenerateSecurePassword() // Generate a password that meets requirements
                    };var result = await _userService.RegisterUserAsync(registerRequest);                    if (!result.Succeeded)
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        return Redirect($"{GetFrontendUrl()}/oauth-success?error=Failed%20to%20create%20user:%20{Uri.EscapeDataString(errors)}");
                    }

                    user = await _userService.GetUserByEmailAsync(email);
                }

                if (user == null)
                {
                    return Redirect($"{GetFrontendUrl()}/oauth-success?error=User%20creation%20failed");
                }

                var token = _userService.GenerateJwtToken(user);
                return Redirect($"{GetFrontendUrl()}/oauth-success?token={token}");
            }
            catch (Exception ex)
            {
                return Redirect($"{GetFrontendUrl()}/oauth-success?error={Uri.EscapeDataString(ex.Message)}");
            }
        }

        private static string GenerateSecurePassword()
        {
            // Generate a password that meets Identity requirements:
            // - At least 8 characters
            // - Contains digit, lowercase, uppercase
            var random = new Random();
            var chars = "abcdefghijklmnopqrstuvwxyz";
            var upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var digits = "0123456789";
            var special = "!@#$%^&*";
            
            var password = new char[12]; // 12 character password
            password[0] = chars[random.Next(chars.Length)];           // lowercase
            password[1] = upperChars[random.Next(upperChars.Length)]; // uppercase
            password[2] = digits[random.Next(digits.Length)];         // digit
            password[3] = special[random.Next(special.Length)];       // special
            
            // Fill the rest with random characters from all sets
            var allChars = chars + upperChars + digits + special;
            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }
            
            // Shuffle the password
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }
            
            return new string(password);
        }

        private static string SanitizeUsername(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "user";
            
            // Remove invalid characters and keep only letters and digits
            var sanitized = new string(input.Where(c => char.IsLetterOrDigit(c)).ToArray());
            
            // Ensure the username is not empty and has reasonable length
            if (string.IsNullOrEmpty(sanitized))
                return "user";
            
            // Limit length to prevent overly long usernames
            if (sanitized.Length > 20)
                sanitized = sanitized.Substring(0, 20);
            
            return sanitized;
        }
    }
}