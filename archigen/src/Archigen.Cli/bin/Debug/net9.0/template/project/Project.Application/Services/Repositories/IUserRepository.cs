using Core.Persistence.Repositories;

using Project.Domain.Entities;

namespace Project.Application.Services.Repositories;

public interface IUserRepository : IAsyncRepository<User, Guid>, IRepository<User, Guid> { }