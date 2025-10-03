using Core.Application.Rules;
using Core.CrossCuttingConcerns.Exception.Types;
using Core.Localization.Abstraction;
using Core.Security.Enums;
using Core.Security.Hashing;

using Project.Application.Features.Authentication.Constants;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

namespace Project.Application.Features.Authentication.Rules;

public class AuthenticationBusinessRules : BaseBusinessRules
{
    private readonly IUserRepository _userRepository;
    private readonly ILocalizationService _localizationService;

    public AuthenticationBusinessRules(IUserRepository userRepository, ILocalizationService localizationService)
    {
        _userRepository = userRepository;
        _localizationService = localizationService;
    }

    private async Task throwBusinessException(string messageKey)
    {
        string message = await _localizationService.GetLocalizedAsync(messageKey, AuthenticationMessages.SectionName);
        throw new BusinessException(message);
    }

    public async Task EmailAuthenticatorShouldBeExists(EmailAuthenticator? emailAuthenticator)
    {
        if (emailAuthenticator is null)
            await throwBusinessException(AuthenticationMessages.EmailAuthenticatorDontExists);
    }

    public async Task OtpAuthenticatorShouldBeExists(OtpAuthenticator? otpAuthenticator)
    {
        if (otpAuthenticator is null)
            await throwBusinessException(AuthenticationMessages.OtpAuthenticatorDontExists);
    }

    public async Task OtpAuthenticatorThatVerifiedShouldNotBeExists(OtpAuthenticator? otpAuthenticator)
    {
        if (otpAuthenticator is not null && otpAuthenticator.IsVerified)
            await throwBusinessException(AuthenticationMessages.AlreadyVerifiedOtpAuthenticatorIsExists);
    }

    public async Task EmailAuthenticatorActivationKeyShouldBeExists(EmailAuthenticator emailAuthenticator)
    {
        if (emailAuthenticator.ActivationKey is null)
            await throwBusinessException(AuthenticationMessages.EmailActivationKeyDontExists);
    }

    public async Task UserShouldBeExistsWhenSelected(User? user)
    {
        if (user == null)
            await throwBusinessException(AuthenticationMessages.UserDontExists);
    }

    public async Task UserShouldNotBeHaveAuthenticator(User user)
    {
        if (user.AuthenticatorType != AuthenticatorType.None)
            await throwBusinessException(AuthenticationMessages.UserHaveAlreadyAAuthenticator);
    }

    public async Task RefreshTokenShouldBeExists(RefreshToken? refreshToken)
    {
        if (refreshToken == null)
            await throwBusinessException(AuthenticationMessages.RefreshDontExists);
    }

    public async Task RefreshTokenShouldBeActive(RefreshToken refreshToken)
    {
        if (refreshToken.RevokedDate != null && DateTime.UtcNow >= refreshToken.ExpirationDate)
            await throwBusinessException(AuthenticationMessages.InvalidRefreshToken);
    }

    public async Task UserEmailShouldBeNotExists(string email)
    {
        bool doesExists = await _userRepository.AnyAsync(predicate: u => u.Email == email);
        if (doesExists)
            await throwBusinessException(AuthenticationMessages.UserMailAlreadyExists);
    }

    public async Task UserPasswordShouldBeMatch(User user, string password)
    {
        if (!HashingHelper.VerifyPasswordHash(password, user!.PasswordHash, user.PasswordSalt))
            await throwBusinessException(AuthenticationMessages.PasswordDontMatch);
    }
}
