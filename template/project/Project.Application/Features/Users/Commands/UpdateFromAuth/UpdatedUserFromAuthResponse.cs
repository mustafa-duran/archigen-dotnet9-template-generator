using Core.Application.Responses;
using Core.Security.JWT;

namespace Project.Application.Features.Users.Commands.UpdateFromAuth;

public class UpdatedUserFromAuthResponse : IResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public AccessToken AccessToken { get; set; }

    public UpdatedUserFromAuthResponse()
    {
        Email = string.Empty;
        AccessToken = null!;
    }

    public UpdatedUserFromAuthResponse(Guid id, string firstName, string lastName, string email, AccessToken accessToken)
    {
        Id = id;
        Email = email;
        AccessToken = accessToken;
    }
}
