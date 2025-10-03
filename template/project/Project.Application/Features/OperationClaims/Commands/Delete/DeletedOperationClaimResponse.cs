using Core.Application.Responses;

namespace Project.Application.Features.OperationClaims.Commands.Delete;

public class DeletedOperationClaimResponse : IResponse
{
    public int Id { get; set; }
}