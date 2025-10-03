using Core.Application.Responses;

namespace Project.Application.Features.Users.Commands.Update;

public class UpdatedUserResponse : IResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public bool Status { get; set; }

    public UpdatedUserResponse()
    {
        Email = string.Empty;
    }

    public UpdatedUserResponse(Guid id, string firstName, string lastName, string email, bool status)
    {
        Id = id;
        Email = email;
        Status = status;
    }
}
