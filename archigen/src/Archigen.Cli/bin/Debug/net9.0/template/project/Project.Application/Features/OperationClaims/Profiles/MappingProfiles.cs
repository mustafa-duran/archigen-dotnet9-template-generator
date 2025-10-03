using AutoMapper;

using Core.Application.Responses;
using Core.Persistence.Paging;

using Project.Application.Features.OperationClaims.Commands.Create;
using Project.Application.Features.OperationClaims.Commands.Delete;
using Project.Application.Features.OperationClaims.Commands.Update;
using Project.Application.Features.OperationClaims.Queries.GetById;
using Project.Application.Features.OperationClaims.Queries.GetList;
using Project.Domain.Entities;

namespace Project.Application.Features.OperationClaims.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<OperationClaim, CreateOperationClaimCommand>().ReverseMap();
        CreateMap<OperationClaim, CreatedOperationClaimResponse>().ReverseMap();
        CreateMap<OperationClaim, UpdateOperationClaimCommand>().ReverseMap();
        CreateMap<OperationClaim, UpdatedOperationClaimResponse>().ReverseMap();
        CreateMap<OperationClaim, DeleteOperationClaimCommand>().ReverseMap();
        CreateMap<OperationClaim, DeletedOperationClaimResponse>().ReverseMap();
        CreateMap<OperationClaim, GetByIdOperationClaimResponse>().ReverseMap();
        CreateMap<OperationClaim, GetListOperationClaimListItemDto>().ReverseMap();
        CreateMap<IPaginate<OperationClaim>, GetListResponse<GetListOperationClaimListItemDto>>().ReverseMap();
    }
}
