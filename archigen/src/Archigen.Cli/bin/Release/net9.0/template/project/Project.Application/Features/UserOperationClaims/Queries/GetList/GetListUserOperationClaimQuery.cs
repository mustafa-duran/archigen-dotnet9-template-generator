using AutoMapper;

using Core.Application.Pipelines.Authorization;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Paging;

using MediatR;

using Project.Application.Features.UserOperationClaims.Constants;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

namespace Project.Application.Features.UserOperationClaims.Queries.GetList;

public class GetListUserOperationClaimQuery : IRequest<GetListResponse<GetListUserOperationClaimListItemDto>>, ISecuredRequest
{
    public PageRequest PageRequest { get; set; }

    public string[] Roles => [UserOperationClaimsOperationClaims.Read];

    public GetListUserOperationClaimQuery()
    {
        PageRequest = new PageRequest { PageIndex = 0, PageSize = 10 };
    }

    public GetListUserOperationClaimQuery(PageRequest pageRequest)
    {
        PageRequest = pageRequest;
    }

    public class GetListUserOperationClaimQueryHandler
        : IRequestHandler<GetListUserOperationClaimQuery, GetListResponse<GetListUserOperationClaimListItemDto>>
    {
        private readonly IUserOperationClaimRepository _userOperationClaimRepository;
        private readonly IMapper _mapper;

        public GetListUserOperationClaimQueryHandler(IUserOperationClaimRepository userOperationClaimRepository, IMapper mapper)
        {
            _userOperationClaimRepository = userOperationClaimRepository;
            _mapper = mapper;
        }

        public async Task<GetListResponse<GetListUserOperationClaimListItemDto>> Handle(
            GetListUserOperationClaimQuery request,
            CancellationToken cancellationToken
        )
        {
            IPaginate<UserOperationClaim> userOperationClaims = await _userOperationClaimRepository.GetListAsync(
                index: request.PageRequest.PageIndex,
                size: request.PageRequest.PageSize,
                enableTracking: false
            );

            GetListResponse<GetListUserOperationClaimListItemDto> mappedUserOperationClaimListModel = _mapper.Map<
                GetListResponse<GetListUserOperationClaimListItemDto>
            >(userOperationClaims);
            return mappedUserOperationClaimListModel;
        }
    }
}
