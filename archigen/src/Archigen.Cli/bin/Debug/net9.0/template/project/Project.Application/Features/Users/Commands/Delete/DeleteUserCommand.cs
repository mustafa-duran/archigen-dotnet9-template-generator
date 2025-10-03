using AutoMapper;

using Core.Application.Pipelines.Authorization;

using MediatR;

using Project.Application.Features.Users.Constants;
using Project.Application.Features.Users.Rules;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

using static Project.Application.Features.Users.Constants.UsersOperationClaims;

namespace Project.Application.Features.Users.Commands.Delete;

public class DeleteUserCommand : IRequest<DeletedUserResponse>, ISecuredRequest
{
    public Guid Id { get; set; }

    public string[] Roles => new[] { Admin, Write, UsersOperationClaims.Delete };

    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, DeletedUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly UserBusinessRules _userBusinessRules;

        public DeleteUserCommandHandler(IUserRepository userRepository, IMapper mapper, UserBusinessRules userBusinessRules)
        {
            _userRepository = userRepository;
            _mapper = mapper;
            _userBusinessRules = userBusinessRules;
        }

        public async Task<DeletedUserResponse> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            User? user = await _userRepository.GetAsync(
                predicate: u => u.Id.Equals(request.Id),
                cancellationToken: cancellationToken
            );
            await _userBusinessRules.UserShouldBeExistsWhenSelected(user);

            await _userRepository.DeleteAsync(user!);

            DeletedUserResponse response = _mapper.Map<DeletedUserResponse>(user);
            return response;
        }
    }
}
