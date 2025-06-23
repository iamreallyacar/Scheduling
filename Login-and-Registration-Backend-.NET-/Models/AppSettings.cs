namespace SchedulingApp.Models
{
    public class AppSettings
    {
        public string FrontendUrl { get; set; } = "http://localhost:5173";
        public JwtSettings Jwt { get; set; } = new();
        public GoogleAuthSettings GoogleAuth { get; set; } = new();
    }

    public class JwtSettings
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int ExpirationInMinutes { get; set; } = 60;
    }

    public class GoogleAuthSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}
