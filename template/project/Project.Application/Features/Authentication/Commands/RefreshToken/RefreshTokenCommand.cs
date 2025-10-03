using Core.Security.JWT;

using MediatR;

using Project.Application.Features.Authentication.Rules;
using Project.Application.Services.AuthenticationService;
using Project.Application.Services.UsersService;
using Project.Domain.Entities;

namespace Project.Application.Features.Authentication.Commands.RefreshToken;

public class RefreshTokenCommand : IRequest<RefreshedTokensResponse>
{
    public string RefreshToken { get; set; }
    public string IpAddress { get; set; }

    public RefreshTokenCommand()
    {
        RefreshToken = string.Empty;
        IpAddress = string.Empty;
    }

    public RefreshTokenCommand(string refreshToken, string ipAddress)
    {
        RefreshToken = refreshToken;
        IpAddress = ipAddress;
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshedTokensResponse>
    {
        private readonly IAuthService _authService;
        private readonly IUserService _userService;
        private readonly AuthenticationBusinessRules _authBusinessRules;

        public RefreshTokenCommandHandler(IAuthService authService, IUserService userService, AuthenticationBusinessRules authBusinessRules)
        {
            _authService = authService;
            _userService = userService;
            _authBusinessRules = authBusinessRules;
        }

        public async Task<RefreshedTokensResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            Domain.Entities.RefreshToken? refreshToken = await _authService.GetRefreshTokenByToken(request.RefreshToken);
            await _authBusinessRules.RefreshTokenShouldBeExists(refreshToken);

            if (refreshToken!.RevokedDate != null)
                await _authService.RevokeDescendantRefreshTokens(
                    refreshToken,
                    request.IpAddress,
                    reason: $"Attempted reuse of revoked ancestor token: {refreshToken.Token}"
                );
            await _authBusinessRules.RefreshTokenShouldBeActive(refreshToken);

            User? user = await _userService.GetAsync(
                predicate: u => u.Id == refreshToken.UserId,
                cancellationToken: cancellationToken
            );
            await _authBusinessRules.UserShouldBeExistsWhenSelected(user);

            Domain.Entities.RefreshToken newRefreshToken = await _authService.RotateRefreshToken(
                user: user!,
                refreshToken,
                request.IpAddress
            );
            Domain.Entities.RefreshToken addedRefreshToken = await _authService.AddRefreshToken(newRefreshToken);
            await _authService.DeleteOldRefreshTokens(refreshToken.UserId);

            AccessToken createdAccessToken = await _authService.CreateAccessToken(user!);

            RefreshedTokensResponse refreshedTokensResponse =
                new() { AccessToken = createdAccessToken, RefreshToken = addedRefreshToken };
            return refreshedTokensResponse;
        }
    }
}
