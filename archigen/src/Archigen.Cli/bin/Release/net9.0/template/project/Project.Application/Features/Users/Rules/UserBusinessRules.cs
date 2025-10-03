using Core.Application.Rules;
using Core.CrossCuttingConcerns.Exception.Types;
using Core.Localization.Abstraction;
using Core.Security.Hashing;

using Project.Application.Features.Users.Constants;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

namespace Project.Application.Features.Users.Rules;

public class UserBusinessRules : BaseBusinessRules
{
    private readonly IUserRepository _userRepository;
    private readonly ILocalizationService _localizationService;

    public UserBusinessRules(IUserRepository userRepository, ILocalizationService localizationService)
    {
        _userRepository = userRepository;
        _localizationService = localizationService;
    }

    private async Task throwBusinessException(string messageKey)
    {
        string message = await _localizationService.GetLocalizedAsync(messageKey, CustomersMessages.SectionName);
        throw new BusinessException(message);
    }

    public async Task UserShouldBeExistsWhenSelected(User? user)
    {
        if (user == null)
            await throwBusinessException(CustomersMessages.UserDontExists);
    }

    public async Task UserIdShouldBeExistsWhenSelected(Guid id)
    {
        bool doesExist = await _userRepository.AnyAsync(predicate: u => u.Id == id);
        if (doesExist)
            await throwBusinessException(CustomersMessages.UserDontExists);
    }

    public async Task UserPasswordShouldBeMatched(User user, string password)
    {
        if (!HashingHelper.VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt))
            await throwBusinessException(CustomersMessages.PasswordDontMatch);
    }

    public async Task UserEmailShouldNotExistsWhenInsert(string email)
    {
        bool doesExists = await _userRepository.AnyAsync(predicate: u => u.Email == email);
        if (doesExists)
            await throwBusinessException(CustomersMessages.UserMailAlreadyExists);
    }

    public async Task UserEmailShouldNotExistsWhenUpdate(Guid id, string email)
    {
        bool doesExists = await _userRepository.AnyAsync(predicate: u => u.Id != id && u.Email == email);
        if (doesExists)
            await throwBusinessException(CustomersMessages.UserMailAlreadyExists);
    }
}
