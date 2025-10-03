using Core.Application.Responses;

namespace Project.Application.Features.UserOperationClaims.Commands.Create;

public class CreatedUserOperationClaimResponse : IResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int OperationClaimId { get; set; }
}