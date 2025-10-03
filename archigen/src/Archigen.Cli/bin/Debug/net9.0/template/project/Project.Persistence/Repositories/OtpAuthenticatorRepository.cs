using Core.Persistence.Repositories;

using Project.Application.Services.Repositories;
using Project.Domain.Entities;
using Project.Persistence.Contexts;

namespace Project.Persistence.Repositories;

public class OtpAuthenticatorRepository : EfRepositoryBase<OtpAuthenticator, Guid, BaseDbContext>, IOtpAuthenticatorRepository
{
    public OtpAuthenticatorRepository(BaseDbContext context)
        : base(context) { }
}