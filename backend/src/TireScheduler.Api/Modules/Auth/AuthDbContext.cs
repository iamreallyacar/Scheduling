using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TireScheduler.Api.Modules.Auth;

internal sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : IdentityUserContext<User, int>(options)
{
    public const string Schema = "auth";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);
        builder.Entity<User>(user =>
        {
            user.Property(u => u.Name).HasMaxLength(User.NameMaxLength);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });
    }
}
