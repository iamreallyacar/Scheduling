namespace Login_and_Registration_Backend_.NET_.Models
{
	public class UserDto
	{
		public string Id { get; set; } = string.Empty;
		public string Username { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
	}

	public class RegisterRequest
	{
		public required string Username { get; set; }
		public required string Email { get; set; }
		public required string Password { get; set; }
	}
	
	public class LoginRequest
	{
		public required string Username { get; set; }
		public required string Password { get; set; }
	}
}