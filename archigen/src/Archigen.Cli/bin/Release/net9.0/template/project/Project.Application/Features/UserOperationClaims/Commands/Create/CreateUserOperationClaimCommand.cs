using AutoMapper;

using Core.Application.Pipelines.Authorization;

using MediatR;

using Project.Application.Features.UserOperationClaims.Constants;
using Project.Application.Features.UserOperationClaims.Rules;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

using static Project.Application.Features.UserOperationClaims.Constants.UserOperationClaimsOperationClaims;

namespace Project.Application.Features.UserOperationClaims.Commands.Create;

public class CreateUserOperationClaimCommand : IRequest<CreatedUserOperationClaimResponse>, ISecuredRequest
{
    public Guid UserId { get; set; }
    public int OperationClaimId { get; set; }

    public string[] Roles => new[] { Admin, Write, UserOperationClaimsOperationClaims.Create };

    public class CreateUserOperationClaimCommandHandler
        : IRequestHandler<CreateUserOperationClaimCommand, CreatedUserOperationClaimResponse>
    {
        private readonly IUserOperationClaimRepository _userOperationClaimRepository;
        private readonly IMapper _mapper;
        private readonly UserOperationClaimBusinessRules _userOperationClaimBusinessRules;

        public CreateUserOperationClaimCommandHandler(
            IUserOperationClaimRepository userOperationClaimRepository,
            IMapper mapper,
            UserOperationClaimBusinessRules userOperationClaimBusinessRules
        )
        {
            _userOperationClaimRepository = userOperationClaimRepository;
            _mapper = mapper;
            _userOperationClaimBusinessRules = userOperationClaimBusinessRules;
        }

        public async Task<CreatedUserOperationClaimResponse> Handle(
            CreateUserOperationClaimCommand request,
            CancellationToken cancellationToken
        )
        {
            await _userOperationClaimBusinessRules.UserShouldNotHasOperationClaimAlreadyWhenInsert(
                request.UserId,
                request.OperationClaimId
            );
            UserOperationClaim mappedUserOperationClaim = _mapper.Map<UserOperationClaim>(request);

            UserOperationClaim createdUserOperationClaim = await _userOperationClaimRepository.AddAsync(mappedUserOperationClaim);

            CreatedUserOperationClaimResponse createdUserOperationClaimDto = _mapper.Map<CreatedUserOperationClaimResponse>(
                createdUserOperationClaim
            );
            return createdUserOperationClaimDto;
        }
    }
}