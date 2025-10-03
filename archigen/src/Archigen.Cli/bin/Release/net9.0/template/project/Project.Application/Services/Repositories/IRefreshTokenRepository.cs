using Core.Persistence.Repositories;

using Project.Domain.Entities;

namespace Project.Application.Services.Repositories;

public interface IRefreshTokenRepository : IAsyncRepository<RefreshToken, Guid>, IRepository<RefreshToken, Guid>
{
    Task<List<RefreshToken>> GetOldRefreshTokensAsync(Guid userId, int refreshTokenTTL);
}