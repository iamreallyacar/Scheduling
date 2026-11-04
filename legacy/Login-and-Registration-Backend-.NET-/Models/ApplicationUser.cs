using Microsoft.AspNetCore.Identity;

namespace Login_and_Registration_Backend_.NET_.Models
{
	public class ApplicationUser : IdentityUser
	{
		// Add any additional properties you need
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? LastLoginAt { get; set; }
	}
}
