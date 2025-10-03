using System.Security.Cryptography;

namespace Core.Security.EmailAuthenticator;

public class EmailAuthenticatorHelper : IEmailAuthenticatorHelper
{
    public Task<string> CreateEmailActivationCode()
    {
        return Task.FromResult(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            );
    }

    public Task<string> CreateEmailActivationKey()
    {
        return Task.FromResult(
            RandomNumberGenerator.GetInt32(Convert.ToInt32(Math.Pow(x: 10, y: 6)))
            .ToString().PadLeft(totalWidth: 6, paddingChar: '0')
            );
    }
}