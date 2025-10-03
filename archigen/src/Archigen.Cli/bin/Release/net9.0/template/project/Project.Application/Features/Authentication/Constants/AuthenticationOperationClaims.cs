namespace Project.Application.Features.Authentication.Constants;

public static class AuthenticationOperationClaims
{
    private const string _section = "Authentication";

    public const string Admin = $"{_section}.Admin";

    public const string Write = $"{_section}.Write";
    public const string Read = $"{_section}.Read";

    public const string RevokeToken = $"{_section}.RevokeToken";
}
