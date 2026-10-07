using Microsoft.AspNetCore.Identity;

namespace BmadPlatform.Infrastructure.Identity;

/// <summary>
/// Identity user persisted in MySQL. Kept in Infrastructure so the domain stays free of Identity.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
}
