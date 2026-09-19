using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MIS.Domain.Entities;
using MIS.Infrastructure.Persistence;

namespace MIS.Infrastructure.Authentication;

public sealed class JwtSigningKeyAccessor
{
    public const string SettingKey = "Jwt:SecretKey";

    private SymmetricSecurityKey? _key;

    public SymmetricSecurityKey SecurityKey =>
        _key ?? throw new InvalidOperationException("The JWT signing key has not been initialized.");

    public void Set(string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        if (bytes.Length < JwtOptions.MinimumSecretBytes)
            throw new InvalidOperationException($"Jwt:SecretKey must be at least {JwtOptions.MinimumSecretBytes} bytes long.");
        _key = new SymmetricSecurityKey(bytes);
    }

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken token = default)
    {
        var accessor = services.GetRequiredService<JwtSigningKeyAccessor>();
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(token);

        var setting = await db.RuntimeSettings.SingleOrDefaultAsync(x => x.Key == SettingKey, token);
        if (setting is null)
        {
            setting = new RuntimeSetting(SettingKey, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)), DateTimeOffset.UtcNow);
            db.RuntimeSettings.Add(setting);
            await db.SaveChangesAsync(token);
        }

        accessor.Set(setting.Value);
    }
}
