using System.Security.Cryptography;
using System.Text.Json;
using Identity.Modules.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Modules.KeyManagement;

public sealed class DevKeyMaterialService : IKeyMaterialService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _kid;
    private readonly RSAParameters _publicParams;
    private readonly RsaSecurityKey _rsaKey;
    private readonly SigningCredentials _signingCredentials;

    public DevKeyMaterialService(IOptions<JwtAuthOptions> options, IHostEnvironment environment)
    {
        var keyFilePath = ResolveKeyPath(options.Value.DevelopmentKeyPath, environment.ContentRootPath);
        var material = LoadOrCreateMaterial(keyFilePath);
        _kid = material.Kid;
        _publicParams = material.PublicParams;
        _rsaKey = new RsaSecurityKey(material.PrivateParams)
        {
            KeyId = _kid
        };
        _signingCredentials = new SigningCredentials(_rsaKey, SecurityAlgorithms.RsaSha256);
    }

    public SigningCredentials GetCurrentSigningCredentials()
    {
        return _signingCredentials;
    }

    public IEnumerable<SecurityKey> GetValidationKeys()
    {
        yield return _rsaKey;
    }

    public string GetCurrentKeyId()
    {
        return _kid;
    }

    public object GetJwksDocument()
    {
        // Build a minimal JWKS for the single RSA key from stored public parameters
        var n = Base64UrlEncoder.Encode(_publicParams.Modulus!);
        var e = Base64UrlEncoder.Encode(_publicParams.Exponent!);
        return new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = SecurityAlgorithms.RsaSha256,
                    kid = _kid,
                    n,
                    e
                }
            }
        };
    }

    private static (string Kid, RSAParameters PrivateParams, RSAParameters PublicParams) LoadOrCreateMaterial(string keyPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        var lockPath = $"{keyPath}.lock";

        using var lockStream = AcquireFileLock(lockPath);
        if (TryLoadPersistedKey(keyPath, out var persistedMaterial))
        {
            return persistedMaterial;
        }

        using var rsa = RSA.Create(2048);
        var kid = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(8));
        var persisted = new PersistedDevKey(kid, Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
        var tempPath = $"{keyPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(persisted, JsonOptions));
            File.Move(tempPath, keyPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        return (kid, rsa.ExportParameters(true), rsa.ExportParameters(false));
    }

    private static string ResolveKeyPath(string configuredPath, string contentRootPath)
    {
        var keyPath = string.IsNullOrWhiteSpace(configuredPath)
            ? "data/identity/dev-jwt-signing-key.json"
            : configuredPath;

        if (Path.IsPathRooted(keyPath))
        {
            return keyPath;
        }

        var root = string.IsNullOrWhiteSpace(contentRootPath) ? Directory.GetCurrentDirectory() : contentRootPath;
        return Path.GetFullPath(Path.Combine(root, keyPath));
    }

    private static bool TryLoadPersistedKey(string keyPath,
        out (string Kid, RSAParameters PrivateParams, RSAParameters PublicParams) material)
    {
        material = default;

        if (!File.Exists(keyPath))
        {
            return false;
        }

        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedDevKey>(File.ReadAllText(keyPath));
            if (persisted is null || string.IsNullOrWhiteSpace(persisted.Kid) ||
                string.IsNullOrWhiteSpace(persisted.PrivateKeyPkcs8Base64))
            {
                return false;
            }

            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(persisted.PrivateKeyPkcs8Base64), out _);
            material = (persisted.Kid, rsa.ExportParameters(true), rsa.ExportParameters(false));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static FileStream AcquireFileLock(string lockPath)
    {
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                Thread.Sleep(25);
            }
        }
    }

    private sealed record PersistedDevKey(string Kid, string PrivateKeyPkcs8Base64);
}
