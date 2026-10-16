using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using Xunit;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class LoginCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _agentsMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _agentsMock.Object,
            _hasherMock.Object,
            _tokenServiceMock.Object,
            NullLogger<LoginCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsTokenAndUser()
    {
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, DateTime.UtcNow);
        agent.SetPasswordHash("valid_hash");

        _agentsMock.Setup(r => r.GetByEmailAsync("alex.turner@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _hasherMock.Setup(h => h.VerifyPassword("LocalDev123!", "valid_hash"))
            .Returns(true);

        var expiry = DateTime.UtcNow.AddHours(8);
        _tokenServiceMock.Setup(t => t.GenerateToken(agent.Id, agent.FullName, agent.Email))
            .Returns(("jwt_token_123", expiry));

        var request = new LoginRequest("alex.turner@northwind-support.example", "LocalDev123!");

        var response = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("jwt_token_123", response.Token);
        Assert.Equal(expiry, response.ExpiresAtUtc);
        Assert.Equal(agent.Id, response.User.Id);
        Assert.Equal("Alex Turner", response.User.FullName);
        Assert.Equal("alex.turner@northwind-support.example", response.User.Email);
    }

    [Fact]
    public async Task HandleAsync_WithWrongPassword_ThrowsAuthenticationFailedException()
    {
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, DateTime.UtcNow);
        agent.SetPasswordHash("valid_hash");

        _agentsMock.Setup(r => r.GetByEmailAsync("alex.turner@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _hasherMock.Setup(h => h.VerifyPassword("WrongPassword!", "valid_hash"))
            .Returns(false);

        var request = new LoginRequest("alex.turner@northwind-support.example", "WrongPassword!");

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentEmail_ThrowsAuthenticationFailedException()
    {
        _agentsMock.Setup(r => r.GetByEmailAsync("unknown@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var request = new LoginRequest("unknown@example.com", "Password123!");

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithInactiveAgent_ThrowsAuthenticationFailedException()
    {
        var yuki = new Agent("Yuki Tanaka", "yuki.tanaka@northwind-support.example", 8, DateTime.UtcNow);
        yuki.SetPasswordHash("valid_hash");
        yuki.Deactivate(); // Inactive agent AU-4

        _agentsMock.Setup(r => r.GetByEmailAsync("yuki.tanaka@northwind-support.example", It.IsAny<CancellationToken>()))
            .ReturnsAsync(yuki);

        var request = new LoginRequest("yuki.tanaka@northwind-support.example", "LocalDev123!");

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(request, CancellationToken.None));
    }
}
