﻿using Core.Application.Responses;
using Core.Security.JWT;

namespace Project.Application.Features.Authentication.Commands.Register;

public class RegisteredResponse : IResponse
{
    public AccessToken AccessToken { get; set; }
    public Domain.Entities.RefreshToken RefreshToken { get; set; }

    public RegisteredResponse()
    {
        AccessToken = null!;
        RefreshToken = null!;
    }

    public RegisteredResponse(AccessToken accessToken, Domain.Entities.RefreshToken refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }
}