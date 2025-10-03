using Core.Application.Responses;

namespace Project.Application.Features.Authentication.Commands.EnableOtpAuthenticator;

public class EnabledOtpAuthenticatorResponse : IResponse
{
    public string SecretKey { get; set; }

    public EnabledOtpAuthenticatorResponse()
    {
        SecretKey = string.Empty;
    }

    public EnabledOtpAuthenticatorResponse(string secretKey)
    {
        SecretKey = secretKey;
    }
}