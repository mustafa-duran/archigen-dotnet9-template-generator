using AutoMapper;

using Core.Security.Entities;

using Project.Application.Features.Authentication.Commands.RevokeToken;
using Project.Domain.Entities;

namespace Project.Application.Features.Authentication.Profiles;

public class MappingProfiles : Profile
{
    public MappingProfiles()
    {
        CreateMap<RefreshToken<Guid, Guid>, RefreshToken>().ReverseMap();
        CreateMap<RefreshToken, RevokedTokenResponse>().ReverseMap();
    }
}
