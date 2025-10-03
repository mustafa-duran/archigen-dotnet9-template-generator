using Core.Persistence.Repositories;

using Project.Application.Services.Repositories;
using Project.Domain.Entities;
using Project.Persistence.Contexts;

namespace Project.Persistence.Repositories;

public class EmailAuthenticatorRepository
    : EfRepositoryBase<EmailAuthenticator, Guid, BaseDbContext>,
        IEmailAuthenticatorRepository
{
    public EmailAuthenticatorRepository(BaseDbContext context)
        : base(context) { }
}