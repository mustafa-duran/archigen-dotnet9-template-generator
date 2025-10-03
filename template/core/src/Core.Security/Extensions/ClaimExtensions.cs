using System.Security.Claims;

namespace Core.Security.Extensions;

public static class ClaimExtensions
{
    public static void AddEmail(this ICollection<Claim> claims, string email)
    {
        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new(ClaimTypes.Email, email));
    }

    public static void AddName(this ICollection<Claim> claims, string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            claims.Add(new(ClaimTypes.Name, name));
    }

    public static void AddNameIdentifier(this ICollection<Claim> claims, string nameIdentifier)
    {
        if (!string.IsNullOrWhiteSpace(nameIdentifier))
            claims.Add(new(ClaimTypes.NameIdentifier, nameIdentifier));
    }

    public static void AddRoles(this ICollection<Claim> claims, ICollection<string> roles)
    {
        foreach (var role in roles)
            claims.AddRole(role);
    }

    public static void AddRole(this ICollection<Claim> claims, string role)
    {
        if (!string.IsNullOrWhiteSpace(role))
            claims.Add(new(ClaimTypes.Role, role));
    }
}
