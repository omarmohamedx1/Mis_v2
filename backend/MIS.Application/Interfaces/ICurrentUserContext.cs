namespace MIS.Application.Interfaces;

public interface ICurrentUserContext
{
    Guid UserId { get; }

    string Username { get; }

    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
}
