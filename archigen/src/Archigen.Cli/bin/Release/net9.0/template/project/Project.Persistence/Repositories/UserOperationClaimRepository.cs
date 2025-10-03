using Core.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

using Project.Application.Services.Repositories;
using Project.Domain.Entities;
using Project.Persistence.Contexts;

namespace Project.Persistence.Repositories;

public class UserOperationClaimRepository
    : EfRepositoryBase<UserOperationClaim, Guid, BaseDbContext>,
        IUserOperationClaimRepository
{
    public UserOperationClaimRepository(BaseDbContext context)
        : base(context) { }

    public async Task<IList<OperationClaim>> GetOperationClaimsByUserIdAsync(Guid userId)
    {
        List<OperationClaim> operationClaims = await Query()
            .AsNoTracking()
            .Where(p => p.UserId.Equals(userId))
            .Select(p => new OperationClaim { Id = p.OperationClaimId, Name = p.OperationClaim.Name })
            .ToListAsync();
        return operationClaims;
    }
}