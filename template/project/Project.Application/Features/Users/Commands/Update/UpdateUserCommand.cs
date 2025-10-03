using AutoMapper;

using Core.Application.Pipelines.Authorization;
using Core.Security.Hashing;

using MediatR;

using Project.Application.Features.Users.Constants;
using Project.Application.Features.Users.Rules;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

using static Project.Application.Features.Users.Constants.UsersOperationClaims;

namespace Project.Application.Features.Users.Commands.Update;

public class UpdateUserCommand : IRequest<UpdatedUserResponse>, ISecuredRequest
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }

    public UpdateUserCommand()
    {
        Email = string.Empty;
        Password = string.Empty;
    }

    public UpdateUserCommand(Guid id, string firstName, string lastName, string email, string password)
    {
        Id = id;
        Email = email;
        Password = password;
    }

    public string[] Roles => new[] { Admin, Write, UsersOperationClaims.Update };

    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UpdatedUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly UserBusinessRules _userBusinessRules;

        public UpdateUserCommandHandler(IUserRepository userRepository, IMapper mapper, UserBusinessRules userBusinessRules)
        {
            _userRepository = userRepository;
            _mapper = mapper;
            _userBusinessRules = userBusinessRules;
        }

        public async Task<UpdatedUserResponse> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            User? user = await _userRepository.GetAsync(
                predicate: u => u.Id.Equals(request.Id),
                cancellationToken: cancellationToken
            );
            await _userBusinessRules.UserShouldBeExistsWhenSelected(user);
            await _userBusinessRules.UserEmailShouldNotExistsWhenUpdate(user!.Id, user.Email);
            user = _mapper.Map(request, user);

            HashingHelper.CreatePasswordHash(
                request.Password,
                passwordHash: out byte[] passwordHash,
                passwordSalt: out byte[] passwordSalt
            );
            user!.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;
            await _userRepository.UpdateAsync(user);

            UpdatedUserResponse response = _mapper.Map<UpdatedUserResponse>(user);
            return response;
        }
    }
}
