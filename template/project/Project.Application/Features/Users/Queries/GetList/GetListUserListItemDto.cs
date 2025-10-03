using Core.Application.Dtos;

namespace Project.Application.Features.Users.Queries.GetList;

public class GetListUserListItemDto : IDto
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public bool Status { get; set; }

    public GetListUserListItemDto()
    {
        Email = string.Empty;
    }

    public GetListUserListItemDto(Guid id, string firstName, string lastName, string email, bool status)
    {
        Id = id;
        Email = email;
        Status = status;
    }
}
