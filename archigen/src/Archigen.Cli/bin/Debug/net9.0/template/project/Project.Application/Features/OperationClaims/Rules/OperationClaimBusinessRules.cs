using Core.Application.Rules;
using Core.CrossCuttingConcerns.Exception.Types;
using Core.Localization.Abstraction;

using Project.Application.Features.OperationClaims.Constants;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

namespace Project.Application.Features.OperationClaims.Rules;

public class OperationClaimBusinessRules : BaseBusinessRules
{
    private readonly IOperationClaimRepository _operationClaimRepository;
    private readonly ILocalizationService _localizationService;

    public OperationClaimBusinessRules(
        IOperationClaimRepository operationClaimRepository,
        ILocalizationService localizationService
    )
    {
        _operationClaimRepository = operationClaimRepository;
        _localizationService = localizationService;
    }

    private async Task throwBusinessException(string messageKey)
    {
        string message = await _localizationService.GetLocalizedAsync(messageKey, OperationClaimsMessages.SectionName);
        throw new BusinessException(message);
    }

    public async Task OperationClaimShouldExistWhenSelected(OperationClaim? operationClaim)
    {
        if (operationClaim == null)
            await throwBusinessException(OperationClaimsMessages.NotExists);
    }

    public async Task OperationClaimIdShouldExistWhenSelected(int id)
    {
        bool doesExist = await _operationClaimRepository.AnyAsync(predicate: b => b.Id == id);
        if (doesExist)
            await throwBusinessException(OperationClaimsMessages.NotExists);
    }

    public async Task OperationClaimNameShouldNotExistWhenCreating(string name)
    {
        bool doesExist = await _operationClaimRepository.AnyAsync(predicate: b => b.Name == name);
        if (doesExist)
            await throwBusinessException(OperationClaimsMessages.AlreadyExists);
    }

    public async Task OperationClaimNameShouldNotExistWhenUpdating(int id, string name)
    {
        bool doesExist = await _operationClaimRepository.AnyAsync(predicate: b => b.Id != id && b.Name == name);
        if (doesExist)
            await throwBusinessException(OperationClaimsMessages.AlreadyExists);
    }
}
