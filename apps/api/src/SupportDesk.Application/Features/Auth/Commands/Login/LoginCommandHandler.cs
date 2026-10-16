using FluentValidation;
using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;
using ApplicationValidationException = SupportDesk.Application.Exceptions.ValidationException;

namespace SupportDesk.Application.Features.Auth.Commands.Login;

/// <summary>
/// Authenticates an agent using email and password, generating a JWT token on success (AU-4).
/// Inactive agents and failed credential attempts produce an identical 401 response.
/// </summary>
public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ILogger<LoginCommandHandler> logger,
    IValidator<LoginRequest>? validator = null)
{
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        if (validator is not null)
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                throw new ApplicationValidationException(validationResult.Errors
                    .GroupBy(f => f.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()));
            }
        }

        var agent = await agents.GetByEmailAsync(request.Email, ct);

        // Security requirement AU-4:
        // Wrong email, wrong password, or inactive agent (Yuki) return the same generic 401.
        if (agent is null || !agent.IsActive || !passwordHasher.VerifyPassword(request.Password, agent.PasswordHash))
        {
            logger.LogWarning("Authentication failed for email {Email}", request.Email);
            throw new AuthenticationFailedException();
        }

        var (token, expiresAtUtc) = tokenService.GenerateToken(agent.Id, agent.FullName, agent.Email);

        logger.LogInformation("Agent {AgentId} ({Email}) authenticated successfully.", agent.Id, agent.Email);

        return new LoginResponse(token, expiresAtUtc, new AuthUserDto(agent.Id, agent.FullName, agent.Email));
    }
}
