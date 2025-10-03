using AutoMapper;

using Core.Application.Pipelines.Authorization;

using MediatR;

using Project.Application.Features.UserOperationClaims.Constants;
using Project.Application.Features.UserOperationClaims.Rules;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

using static Project.Application.Features.UserOperationClaims.Constants.UserOperationClaimsOperationClaims;

namespace Project.Application.Features.UserOperationClaims.Commands.Delete;

public class DeleteUserOperationClaimCommand : IRequest<DeletedUserOperationClaimResponse>, ISecuredRequest
{
    public Guid Id { get; set; }

    public string[] Roles => new[] { Admin, Write, UserOperationClaimsOperationClaims.Delete };

    public class DeleteUserOperationClaimCommandHandler
        : IRequestHandler<DeleteUserOperationClaimCommand, DeletedUserOperationClaimResponse>
    {
        private readonly IUserOperationClaimRepository _userOperationClaimRepository;
        private readonly IMapper _mapper;
        private readonly UserOperationClaimBusinessRules _userOperationClaimBusinessRules;

        public DeleteUserOperationClaimCommandHandler(
            IUserOperationClaimRepository userOperationClaimRepository,
            IMapper mapper,
            UserOperationClaimBusinessRules userOperationClaimBusinessRules
        )
        {
            _userOperationClaimRepository = userOperationClaimRepository;
            _mapper = mapper;
            _userOperationClaimBusinessRules = userOperationClaimBusinessRules;
        }

        public async Task<DeletedUserOperationClaimResponse> Handle(
            DeleteUserOperationClaimCommand request,
            CancellationToken cancellationToken
        )
        {
            UserOperationClaim? userOperationClaim = await _userOperationClaimRepository.GetAsync(
                predicate: uoc => uoc.Id.Equals(request.Id),
                cancellationToken: cancellationToken
            );
            await _userOperationClaimBusinessRules.UserOperationClaimShouldExistWhenSelected(userOperationClaim);

            await _userOperationClaimRepository.DeleteAsync(userOperationClaim!);

            DeletedUserOperationClaimResponse response = _mapper.Map<DeletedUserOperationClaimResponse>(userOperationClaim);
            return response;
        }
    }
}
