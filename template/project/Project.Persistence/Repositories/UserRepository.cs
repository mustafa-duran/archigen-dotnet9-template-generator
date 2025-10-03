using Core.Persistence.Repositories;

using Project.Application.Services.Repositories;
using Project.Domain.Entities;
using Project.Persistence.Contexts;

namespace Project.Persistence.Repositories;

public class UserRepository : EfRepositoryBase<User, Guid, BaseDbContext>, IUserRepository
{
    public UserRepository(BaseDbContext context)
        : base(context) { }
}