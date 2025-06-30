using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Login_and_Registration_Backend_.NET_.Models;
using Login_and_Registration_Backend_.NET_.Data;

namespace Login_and_Registration_Backend_.NET_.Services
{
	public class UserService : IUserService
	{
		private readonly UserManager<ApplicationUser> _userManager;
    	private readonly SignInManager<ApplicationUser> _signInManager;
		private readonly ApplicationDbContext _dbContext;
		private readonly ILogger<UserService> _logger;

		public UserService(
			UserManager<ApplicationUser> userManager,
			SignInManager<ApplicationUser> signInManager,
			ApplicationDbContext dbContext,
			ILogger<UserService> logger)
		{
			_userManager = userManager;
			_signInManager = signInManager;
			_dbContext = dbContext;
			_logger = logger;
		}

		public async Task<ServiceResult<ApplicationUser>> GetUserByIdAsync(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				_logger.LogWarning("GetUserByIdAsync called with null or empty id");
				return ServiceResult<ApplicationUser>.Failure("User ID cannot be null or empty");
			}

			try
			{
				var user = await _userManager.FindByIdAsync(id);
				return user != null 
					? ServiceResult<ApplicationUser>.Success(user)
					: ServiceResult<ApplicationUser>.Failure("User not found");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving user by ID: {UserId}", id);
				return ServiceResult<ApplicationUser>.Failure("Failed to retrieve user");
			}
		}

		public async Task<ServiceResult<ApplicationUser>> GetUserByEmailAsync(string email)
		{
			if (string.IsNullOrWhiteSpace(email))
			{
				_logger.LogWarning("GetUserByEmailAsync called with null or empty email");
				return ServiceResult<ApplicationUser>.Failure("Email cannot be null or empty");
			}

			try
			{
				var user = await _userManager.FindByEmailAsync(email);
				return user != null 
					? ServiceResult<ApplicationUser>.Success(user)
					: ServiceResult<ApplicationUser>.Failure("User not found");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving user by email: {Email}", email);
				return ServiceResult<ApplicationUser>.Failure("Failed to retrieve user");
			}
		}

		public async Task<ServiceResult<ApplicationUser>> GetUserByUsernameAsync(string username)
		{
			if (string.IsNullOrWhiteSpace(username))
			{
				_logger.LogWarning("GetUserByUsernameAsync called with null or empty username");
				return ServiceResult<ApplicationUser>.Failure("Username cannot be null or empty");
			}

			try
			{
				var user = await _userManager.FindByNameAsync(username);
				return user != null 
					? ServiceResult<ApplicationUser>.Success(user)
					: ServiceResult<ApplicationUser>.Failure("User not found");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving user by username: {Username}", username);
				return ServiceResult<ApplicationUser>.Failure("Failed to retrieve user");
			}
		}

		public async Task<ServiceResult<ApplicationUser>> RegisterUserAsync(RegisterRequest request)
		{
			if (request == null)
			{
				_logger.LogWarning("RegisterUserAsync called with null request");
				return ServiceResult<ApplicationUser>.Failure("Registration request cannot be null");
			}

			if (string.IsNullOrWhiteSpace(request.Username) || 
				string.IsNullOrWhiteSpace(request.Email) || 
				string.IsNullOrWhiteSpace(request.Password))
			{
				_logger.LogWarning("RegisterUserAsync called with incomplete data");
				return ServiceResult<ApplicationUser>.Failure("Username, email, and password are required");
			}

			try
			{
				var user = new ApplicationUser
				{
					UserName = request.Username,
					Email = request.Email,
					CreatedAt = DateTime.UtcNow
				};

				var result = await _userManager.CreateAsync(user, request.Password);
				
				if (result.Succeeded)
				{
					_logger.LogInformation("User registered successfully: {Username}", request.Username);
					return ServiceResult<ApplicationUser>.Success(user);
				}
				else
				{
					var errors = result.Errors.Select(e => e.Description);
					_logger.LogWarning("User registration failed: {Username}. Errors: {Errors}", 
						request.Username, string.Join(", ", errors));
					return ServiceResult<ApplicationUser>.Failure("Registration failed", errors);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during user registration: {Username}", request.Username);
				return ServiceResult<ApplicationUser>.Failure("Registration failed due to an internal error");
			}
		}

		public async Task<ServiceResult<ApplicationUser>> ValidateUserAsync(LoginRequest request)
		{
			if (request == null)
			{
				_logger.LogWarning("ValidateUserAsync called with null request");
				return ServiceResult<ApplicationUser>.Failure("Invalid credentials");
			}

			if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
			{
				_logger.LogWarning("ValidateUserAsync called with incomplete credentials");
				return ServiceResult<ApplicationUser>.Failure("Invalid credentials");
			}

			try
			{
				var user = await _userManager.FindByNameAsync(request.Username);
				if (user == null)
				{
					// Simulate password check to prevent timing attacks
					_userManager.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
					_logger.LogWarning("Login attempt with non-existent username: {Username}", request.Username);
					return ServiceResult<ApplicationUser>.Failure("Invalid credentials");
				}

				var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
				if (result.Succeeded)
				{
					user.LastLoginAt = DateTime.UtcNow;
					await _userManager.UpdateAsync(user);
					_logger.LogInformation("User validated successfully: {Username}", request.Username);
					return ServiceResult<ApplicationUser>.Success(user);
				}
				else
				{
					_logger.LogWarning("Login attempt with invalid password for user: {Username}", request.Username);
					return ServiceResult<ApplicationUser>.Failure("Invalid credentials");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during user validation: {Username}", request.Username);
				return ServiceResult<ApplicationUser>.Failure("Validation failed due to an internal error");
			}
		}

		public string HashPassword(ApplicationUser user, string password)
		{
			return _userManager.PasswordHasher.HashPassword(user, password);
		}

		public bool VerifyPassword(ApplicationUser user, string providedPassword, string hashedPassword)
		{
			var result = _userManager.PasswordHasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
			return result == PasswordVerificationResult.Success;
		}
    }
}