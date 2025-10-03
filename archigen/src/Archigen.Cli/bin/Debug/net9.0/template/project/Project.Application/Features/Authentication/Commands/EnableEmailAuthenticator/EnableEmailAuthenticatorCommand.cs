using System.Web;

using Core.Application.Pipelines.Authorization;
using Core.Mailing;
using Core.Security.Enums;

using MediatR;

using MimeKit;

using Project.Application.Features.Authentication.Rules;
using Project.Application.Services.AuthenticatorService;
using Project.Application.Services.Repositories;
using Project.Application.Services.UsersService;
using Project.Domain.Entities;


namespace Project.Application.Features.Authentication.Commands.EnableEmailAuthenticator;

public class EnableEmailAuthenticatorCommand : IRequest, ISecuredRequest
{
    public Guid UserId { get; set; }
    public string VerifyEmailUrlPrefix { get; set; }

    public string[] Roles => [];

    public EnableEmailAuthenticatorCommand()
    {
        VerifyEmailUrlPrefix = string.Empty;
    }

    public EnableEmailAuthenticatorCommand(Guid userId, string verifyEmailUrlPrefix)
    {
        UserId = userId;
        VerifyEmailUrlPrefix = verifyEmailUrlPrefix;
    }

    public class EnableEmailAuthenticatorCommandHandler : IRequestHandler<EnableEmailAuthenticatorCommand>
    {
        private readonly AuthenticationBusinessRules _authBusinessRules;
        private readonly IAuthenticatorService _authenticatorService;
        private readonly IEmailAuthenticatorRepository _emailAuthenticatorRepository;
        private readonly IMailService _mailService;
        private readonly IUserService _userService;

        public EnableEmailAuthenticatorCommandHandler(
            IUserService userService,
            IEmailAuthenticatorRepository emailAuthenticatorRepository,
            IMailService mailService,
            AuthenticationBusinessRules authBusinessRules,
            IAuthenticatorService authenticatorService
        )
        {
            _userService = userService;
            _emailAuthenticatorRepository = emailAuthenticatorRepository;
            _mailService = mailService;
            _authBusinessRules = authBusinessRules;
            _authenticatorService = authenticatorService;
        }

        public async Task Handle(EnableEmailAuthenticatorCommand request, CancellationToken cancellationToken)
        {
            User? user = await _userService.GetAsync(
                predicate: u => u.Id == request.UserId,
                cancellationToken: cancellationToken
            );
            await _authBusinessRules.UserShouldBeExistsWhenSelected(user);
            await _authBusinessRules.UserShouldNotBeHaveAuthenticator(user!);

            user!.AuthenticatorType = AuthenticatorType.Email;
            await _userService.UpdateAsync(user);

            EmailAuthenticator emailAuthenticator = await _authenticatorService.CreateEmailAuthenticator(user);
            EmailAuthenticator addedEmailAuthenticator = await _emailAuthenticatorRepository.AddAsync(emailAuthenticator);

            var toEmailList = new List<MailboxAddress> { new(name: user.Email, user.Email) };

            _mailService.SendMail(
                new Mail
                {
                    ToList = toEmailList,
                    Subject = "Verify Your Email - Project sampleproject",
                    TextBody =
                        $"Click on the link to verify your email: {request.VerifyEmailUrlPrefix}?ActivationKey={HttpUtility.UrlEncode(addedEmailAuthenticator.ActivationKey)}"
                }
            );
        }
    }
}
