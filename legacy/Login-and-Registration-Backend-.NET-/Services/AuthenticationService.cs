using Login_and_Registration_Backend_.NET_.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace Login_and_Registration_Backend_.NET_.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IUserService _userService;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly HashSet<string> _revokedTokens = new(); // In production, use Redis or database

        public AuthenticationService(
            IUserService userService, 
            IJwtService jwtService,
            ILogger<AuthenticationService> logger)
        {
            _userService = userService;
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task<AuthenticationResult> AuthenticateAsync(LoginRequest request)
        {
            try
            {
                _logger.LogDebug("Attempting authentication for user: {Username}", request.Username);
                
                var userResult = await _userService.ValidateUserAsync(request);
                
                if (!userResult.IsSuccess || userResult.Data == null)
                {
                    _logger.LogWarning("Authentication failed for user: {Username}", request.Username);
                    return AuthenticationResult.FailureResult("Invalid username or password");
                }

                var token = _jwtService.GenerateJwtToken(userResult.Data);
                var userDto = new UserDto
                {
                    Id = userResult.Data.Id,
                    Username = userResult.Data.UserName ?? string.Empty,
                    Email = userResult.Data.Email ?? string.Empty
                };

                _logger.LogInformation("User authenticated successfully: {UserId}", userResult.Data.Id);
                return AuthenticationResult.SuccessResult(token, userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication for user: {Username}", request.Username);
                return AuthenticationResult.FailureResult("Authentication failed due to an internal error");
            }
        }

        public async Task<AuthenticationResult> RegisterAsync(RegisterRequest request)
        {
            try
            {
                _logger.LogDebug("Attempting registration for user: {Username}", request.Username);

                // Check for existing users
                var existingUserByUsername = await _userService.GetUserByUsernameAsync(request.Username);
                var existingUserByEmail = await _userService.GetUserByEmailAsync(request.Email);

                if (existingUserByUsername.IsSuccess || existingUserByEmail.IsSuccess)
                {
                    _logger.LogWarning("Registration failed - user already exists: {Username}, {Email}", 
                        request.Username, request.Email);
                    return AuthenticationResult.FailureResult("Username or email already exists");
                }

                var result = await _userService.RegisterUserAsync(request);
                
                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Registration failed for user: {Username}. Errors: {Errors}", 
                        request.Username, string.Join(", ", result.Errors));
                    return AuthenticationResult.FailureResult("Registration failed", result.Errors);
                }

                // Generate token for the newly created user
                var token = _jwtService.GenerateJwtToken(result.Data!);
                var userDto = new UserDto
                {
                    Id = result.Data!.Id,
                    Username = result.Data!.UserName ?? string.Empty,
                    Email = result.Data!.Email ?? string.Empty
                };

                _logger.LogInformation("User registered successfully: {UserId}", result.Data!.Id);
                return AuthenticationResult.SuccessResult(token, userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for user: {Username}", request.Username);
                return AuthenticationResult.FailureResult("Registration failed due to an internal error");
            }
        }

        public async Task<AuthenticationResult> HandleOAuthUserAsync(OAuthUserInfo oauthInfo)
        {
            try
            {
                _logger.LogDebug("Handling OAuth user: {Email} from {Provider}", oauthInfo.Email, oauthInfo.Provider);

                // Find existing user by email
                var userResult = await _userService.GetUserByEmailAsync(oauthInfo.Email);
                ApplicationUser? user = userResult.IsSuccess ? userResult.Data : null;
                
                if (user == null)
                {
                    // Create new user for OAuth
                    var username = await GenerateUniqueUsernameAsync(oauthInfo.Name ?? oauthInfo.Email.Split('@')[0]);
                    var registerRequest = new RegisterRequest
                    {
                        Username = username,
                        Email = oauthInfo.Email,
                        Password = GenerateSecurePassword()
                    };

                    var registrationResult = await _userService.RegisterUserAsync(registerRequest);
                    if (!registrationResult.IsSuccess)
                    {
                        _logger.LogError("Failed to create OAuth user: {Email}. Errors: {Errors}", 
                            oauthInfo.Email, string.Join(", ", registrationResult.Errors));
                        return AuthenticationResult.FailureResult("Failed to create user account", registrationResult.Errors);
                    }

                    user = registrationResult.Data;
                    if (user == null)
                    {
                        _logger.LogError("OAuth user creation succeeded but user not found: {Email}", oauthInfo.Email);
                        return AuthenticationResult.FailureResult("User creation failed");
                    }

                    _logger.LogInformation("Created new OAuth user: {UserId} for {Provider}", user.Id, oauthInfo.Provider);
                }

                var token = _jwtService.GenerateJwtToken(user);
                var userDto = new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty
                };

                _logger.LogInformation("OAuth authentication successful for user: {UserId}", user.Id);
                return AuthenticationResult.SuccessResult(token, userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OAuth authentication for: {Email}", oauthInfo.Email);
                return AuthenticationResult.FailureResult("OAuth authentication failed due to an internal error");
            }
        }

        public Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                if (_revokedTokens.Contains(token))
                {
                    return Task.FromResult(false);
                }

                var principal = _jwtService.ValidateToken(token);
                return Task.FromResult(principal != null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return Task.FromResult(false);
            }
        }

        public void RevokeToken(string token)
        {
            _revokedTokens.Add(token);
            _logger.LogDebug("Token revoked");
        }

        private async Task<string> GenerateUniqueUsernameAsync(string baseName)
        {
            var username = SanitizeUsername(baseName);
            var counter = 1;
            
            while (await _userService.GetUserByUsernameAsync(username) != null)
            {
                username = $"{SanitizeUsername(baseName)}{counter}";
                counter++;
            }
            
            return username;
        }

        private static string SanitizeUsername(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "user";
            
            var sanitized = new string(input.Where(c => char.IsLetterOrDigit(c)).ToArray());
            
            if (string.IsNullOrEmpty(sanitized))
                return "user";
            
            return sanitized.Length > 20 ? sanitized.Substring(0, 20) : sanitized;
        }

        private static string GenerateSecurePassword()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string special = "!@#$%^&*";
            
            using var rng = RandomNumberGenerator.Create();
            var password = new char[16];
            
            // Ensure at least one character from each required category
            password[0] = chars[GetRandomIndex(rng, chars.Length)];
            password[1] = upperChars[GetRandomIndex(rng, upperChars.Length)];
            password[2] = digits[GetRandomIndex(rng, digits.Length)];
            password[3] = special[GetRandomIndex(rng, special.Length)];
            
            // Fill the rest with random characters
            var allChars = chars + upperChars + digits + special;
            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allChars[GetRandomIndex(rng, allChars.Length)];
            }
            
            // Shuffle the password
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = GetRandomIndex(rng, i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }
            
            return new string(password);
        }

        private static int GetRandomIndex(RandomNumberGenerator rng, int max)
        {
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            return Math.Abs(BitConverter.ToInt32(bytes, 0)) % max;
        }
    }
}
