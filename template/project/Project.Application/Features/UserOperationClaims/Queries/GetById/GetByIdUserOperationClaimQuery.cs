using AutoMapper;

using Core.Application.Pipelines.Authorization;

using MediatR;

using Project.Application.Features.UserOperationClaims.Constants;
using Project.Application.Features.UserOperationClaims.Rules;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

namespace Project.Application.Features.UserOperationClaims.Queries.GetById;

public class GetByIdUserOperationClaimQuery : IRequest<GetByIdUserOperationClaimResponse>, ISecuredRequest
{
    public Guid Id { get; set; }

    public string[] Roles => [UserOperationClaimsOperationClaims.Read];

    public class GetByIdUserOperationClaimQueryHandler
        : IRequestHandler<GetByIdUserOperationClaimQuery, GetByIdUserOperationClaimResponse>
    {
        private readonly IUserOperationClaimRepository _userOperationClaimRepository;
        private readonly IMapper _mapper;
        private readonly UserOperationClaimBusinessRules _userOperationClaimBusinessRules;

        public GetByIdUserOperationClaimQueryHandler(
            IUserOperationClaimRepository userOperationClaimRepository,
            IMapper mapper,
            UserOperationClaimBusinessRules userOperationClaimBusinessRules
        )
        {
            _userOperationClaimRepository = userOperationClaimRepository;
            _mapper = mapper;
            _userOperationClaimBusinessRules = userOperationClaimBusinessRules;
        }

        public async Task<GetByIdUserOperationClaimResponse> Handle(
            GetByIdUserOperationClaimQuery request,
            CancellationToken cancellationToken
        )
        {
            UserOperationClaim? userOperationClaim = await _userOperationClaimRepository.GetAsync(
                predicate: b => b.Id.Equals(request.Id),
                enableTracking: false,
                cancellationToken: cancellationToken
            );
            await _userOperationClaimBusinessRules.UserOperationClaimShouldExistWhenSelected(userOperationClaim);

            GetByIdUserOperationClaimResponse userOperationClaimDto = _mapper.Map<GetByIdUserOperationClaimResponse>(
                userOperationClaim
            );
            return userOperationClaimDto;
        }
    }
}