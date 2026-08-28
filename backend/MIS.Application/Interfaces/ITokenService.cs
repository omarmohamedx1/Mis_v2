using MIS.Domain.Entities;

namespace MIS.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions);
}
