using Core.Application.Responses;

namespace Project.Application.Features.Users.Commands.Delete;

public class DeletedUserResponse : IResponse
{
    public Guid Id { get; set; }
}
