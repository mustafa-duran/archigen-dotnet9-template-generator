using MediatR;

using Project.Application.Features.Authentication.Rules;
using Project.Application.Services.Repositories;
using Project.Domain.Entities;

namespace Project.Application.Features.Authentication.Commands.VerifyEmailAuthenticator;

public class VerifyEmailAuthenticatorCommand : IRequest
{
    public string ActivationKey { get; set; }

    public VerifyEmailAuthenticatorCommand()
    {
        ActivationKey = string.Empty;
    }

    public VerifyEmailAuthenticatorCommand(string activationKey)
    {
        ActivationKey = activationKey;
    }

    public class VerifyEmailAuthenticatorCommandHandler : IRequestHandler<VerifyEmailAuthenticatorCommand>
    {
        private readonly AuthenticationBusinessRules _authBusinessRules;
        private readonly IEmailAuthenticatorRepository _emailAuthenticatorRepository;

        public VerifyEmailAuthenticatorCommandHandler(
            IEmailAuthenticatorRepository emailAuthenticatorRepository,
            AuthenticationBusinessRules authBusinessRules
        )
        {
            _emailAuthenticatorRepository = emailAuthenticatorRepository;
            _authBusinessRules = authBusinessRules;
        }

        public async Task Handle(VerifyEmailAuthenticatorCommand request, CancellationToken cancellationToken)
        {
            EmailAuthenticator? emailAuthenticator = await _emailAuthenticatorRepository.GetAsync(
                predicate: e => e.ActivationKey == request.ActivationKey,
                cancellationToken: cancellationToken
            );
            await _authBusinessRules.EmailAuthenticatorShouldBeExists(emailAuthenticator);
            await _authBusinessRules.EmailAuthenticatorActivationKeyShouldBeExists(emailAuthenticator!);

            emailAuthenticator!.ActivationKey = null;
            emailAuthenticator.IsVerified = true;
            await _emailAuthenticatorRepository.UpdateAsync(emailAuthenticator);
        }
    }
}