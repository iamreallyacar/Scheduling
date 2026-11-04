using Login_and_Registration_Backend_.NET_.Models;

namespace Login_and_Registration_Backend_.NET_.Services
{
    public interface IAuthenticationService
    {
        Task<AuthenticationResult> AuthenticateAsync(LoginRequest request);
        Task<AuthenticationResult> RegisterAsync(RegisterRequest request);
        Task<AuthenticationResult> HandleOAuthUserAsync(OAuthUserInfo oauthInfo);
        Task<bool> ValidateTokenAsync(string token);
        void RevokeToken(string token);
    }

    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public UserDto? User { get; set; }
        public string? ErrorMessage { get; set; }
        public IEnumerable<string>? Errors { get; set; }

        public static AuthenticationResult SuccessResult(string token, UserDto user)
        {
            return new AuthenticationResult
            {
                Success = true,
                Token = token,
                User = user
            };
        }

        public static AuthenticationResult FailureResult(string errorMessage, IEnumerable<string>? errors = null)
        {
            return new AuthenticationResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Errors = errors
            };
        }
    }

    public class OAuthUserInfo
    {
        public required string Email { get; set; }
        public string? Name { get; set; }
        public string? Picture { get; set; }
        public string? Locale { get; set; }
        public required string Provider { get; set; }
        public required string ProviderId { get; set; }
    }
}
