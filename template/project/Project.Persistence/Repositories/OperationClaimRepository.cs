using Core.Persistence.Repositories;

using Project.Application.Services.Repositories;
using Project.Domain.Entities;
using Project.Persistence.Contexts;

namespace Project.Persistence.Repositories;

public class OperationClaimRepository : EfRepositoryBase<OperationClaim, int, BaseDbContext>, IOperationClaimRepository
{
    public OperationClaimRepository(BaseDbContext context)
        : base(context) { }
}