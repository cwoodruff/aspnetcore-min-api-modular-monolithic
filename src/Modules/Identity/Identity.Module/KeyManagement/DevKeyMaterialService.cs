using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Modules.KeyManagement;

public sealed class DevKeyMaterialService : IKeyMaterialService
{
    private readonly string _kid;
    private readonly RSAParameters _publicParams;
    private readonly RsaSecurityKey _rsaKey;
    private readonly SigningCredentials _signingCredentials;

    public DevKeyMaterialService()
    {
        using var rsa = RSA.Create(2048);
        var privateParams = rsa.ExportParameters(true);
        _publicParams = rsa.ExportParameters(false);
        _rsaKey = new RsaSecurityKey(privateParams);
        _kid = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(8));
        _rsaKey.KeyId = _kid;
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
}
