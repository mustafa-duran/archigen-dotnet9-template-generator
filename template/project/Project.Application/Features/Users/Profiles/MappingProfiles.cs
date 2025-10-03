using AutoMapper;

using Core.Application.Responses;
using Core.Persistence.Paging;

using Project.Application.Features.Users.Commands.Create;
using Project.Application.Features.Users.Commands.Delete;
using Project.Application.Features.Users.Commands.Update;
using Project.Application.Features.Users.Commands.UpdateFromAuth;
using Project.Application.Features.Users.Queries.GetById;
using Project.Application.Features.Users.Queries.GetList;
using Project.Domain.Entities;

namespace Project.Application.Features.Users.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<User, CreateUserCommand>().ReverseMap();
        CreateMap<User, CreatedUserResponse>().ReverseMap();
        CreateMap<User, UpdateUserCommand>().ReverseMap();
        CreateMap<User, UpdatedUserResponse>().ReverseMap();
        CreateMap<User, UpdateUserFromAuthCommand>().ReverseMap();
        CreateMap<User, UpdatedUserFromAuthResponse>().ReverseMap();
        CreateMap<User, DeleteUserCommand>().ReverseMap();
        CreateMap<User, DeletedUserResponse>().ReverseMap();
        CreateMap<User, GetByIdUserResponse>().ReverseMap();
        CreateMap<User, GetListUserListItemDto>().ReverseMap();
        CreateMap<IPaginate<User>, GetListResponse<GetListUserListItemDto>>().ReverseMap();
    }
}
