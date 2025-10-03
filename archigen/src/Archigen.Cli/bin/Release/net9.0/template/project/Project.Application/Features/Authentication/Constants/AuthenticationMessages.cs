namespace Project.Application.Features.Authentication.Constants;

public static class AuthenticationMessages
{
    public const string SectionName = "Authentication";

    public const string EmailAuthenticatorDontExists = "EmailAuthenticatorDontExists";
    public const string OtpAuthenticatorDontExists = "OtpAuthenticatorDontExists";
    public const string AlreadyVerifiedOtpAuthenticatorIsExists = "AlreadyVerifiedOtpAuthenticatorIsExists";
    public const string EmailActivationKeyDontExists = "EmailActivationKeyDontExists";
    public const string UserDontExists = "UserDontExists";
    public const string UserHaveAlreadyAAuthenticator = "UserHaveAlreadyAAuthenticator";
    public const string RefreshDontExists = "RefreshDontExists";
    public const string InvalidRefreshToken = "InvalidRefreshToken";
    public const string UserMailAlreadyExists = "UserMailAlreadyExists";
    public const string PasswordDontMatch = "PasswordDontMatch";
}
