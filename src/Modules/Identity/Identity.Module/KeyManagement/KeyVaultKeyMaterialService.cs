using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Identity.Modules.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MicrosoftJsonWebKey = Microsoft.IdentityModel.Tokens.JsonWebKey;

namespace Identity.Modules.KeyManagement;

public sealed class KeyVaultKeyMaterialService : IKeyMaterialService
{
    private readonly string _kid;
    private readonly string _n;
    private readonly string _e;
    private readonly SigningCredentials _signingCredentials;
    private readonly SecurityKey[] _validationKeys;

    public KeyVaultKeyMaterialService(IOptions<JwtAuthOptions> options)
    {
        var jwtOptions = options.Value;

        if (!Uri.TryCreate(jwtOptions.KeyVaultVaultUri, UriKind.Absolute, out var vaultUri))
        {
            throw new InvalidOperationException("Jwt:KeyVaultVaultUri must be a valid absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.KeyVaultKeyName))
        {
            throw new InvalidOperationException("Jwt:KeyVaultKeyName must be configured when Jwt:KeyProvider=KeyVault.");
        }

        var credential = new DefaultAzureCredential();
        var keyClient = new KeyClient(vaultUri, credential);
        var keyVaultKey = keyClient.GetKey(jwtOptions.KeyVaultKeyName).Value;

        if (keyVaultKey.Key.N is null || keyVaultKey.Key.E is null)
        {
            throw new InvalidOperationException(
                $"Azure Key Vault key '{jwtOptions.KeyVaultKeyName}' must expose RSA public material.");
        }

        _kid = !string.IsNullOrWhiteSpace(keyVaultKey.Key.Id)
            ? keyVaultKey.Key.Id
            : keyVaultKey.Id.AbsoluteUri;
        _n = Base64UrlEncoder.Encode(keyVaultKey.Key.N);
        _e = Base64UrlEncoder.Encode(keyVaultKey.Key.E);

        var signingKey = new KeyVaultRsaSecurityKey(keyVaultKey, credential)
        {
            KeyId = _kid
        };
        _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);
        _validationKeys =
        [
            new MicrosoftJsonWebKey
            {
                Kty = JsonWebAlgorithmsKeyTypes.RSA,
                Use = JsonWebKeyUseNames.Sig,
                Alg = SecurityAlgorithms.RsaSha256,
                Kid = _kid,
                N = _n,
                E = _e
            }
        ];
    }

    public SigningCredentials GetCurrentSigningCredentials()
    {
        return _signingCredentials;
    }

    public IEnumerable<SecurityKey> GetValidationKeys()
    {
        return _validationKeys;
    }

    public string GetCurrentKeyId()
    {
        return _kid;
    }

    public object GetJwksDocument()
    {
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
                    n = _n,
                    e = _e
                }
            }
        };
    }

    private sealed class KeyVaultRsaSecurityKey : SecurityKey
    {
        public KeyVaultRsaSecurityKey(KeyVaultKey keyVaultKey, TokenCredential credential)
        {
            CryptographyClient = new CryptographyClient(keyVaultKey.Id, credential);
            KeySize = keyVaultKey.Key.N?.Length * 8 ?? 0;
            CryptoProviderFactory = KeyVaultCryptoProviderFactory.Instance;
        }

        public override int KeySize { get; }

        public CryptographyClient CryptographyClient { get; }
    }

    private sealed class KeyVaultCryptoProviderFactory : CryptoProviderFactory
    {
        public static KeyVaultCryptoProviderFactory Instance { get; } = new();

        public override SignatureProvider CreateForSigning(SecurityKey key, string algorithm)
        {
            if (key is KeyVaultRsaSecurityKey keyVaultKey)
            {
                return new KeyVaultSignatureProvider(keyVaultKey, algorithm);
            }

            return base.CreateForSigning(key, algorithm);
        }

        public override SignatureProvider CreateForSigning(SecurityKey key, string algorithm, bool cacheProvider)
        {
            if (key is KeyVaultRsaSecurityKey keyVaultKey)
            {
                return new KeyVaultSignatureProvider(keyVaultKey, algorithm);
            }

            return base.CreateForSigning(key, algorithm, cacheProvider);
        }

        public override bool IsSupportedAlgorithm(string algorithm, SecurityKey key)
        {
            if (key is KeyVaultRsaSecurityKey)
            {
                return string.Equals(algorithm, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal);
            }

            return base.IsSupportedAlgorithm(algorithm, key);
        }
    }

    private sealed class KeyVaultSignatureProvider : SignatureProvider
    {
        private readonly KeyVaultRsaSecurityKey _key;

        public KeyVaultSignatureProvider(KeyVaultRsaSecurityKey key, string algorithm)
            : base(key, algorithm)
        {
            _key = key;
            WillCreateSignatures = true;
        }

        public override byte[] Sign(byte[] input)
        {
            if (!string.Equals(Algorithm, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
            {
                throw new NotSupportedException($"Unsupported JWT signing algorithm '{Algorithm}'.");
            }

            return _key.CryptographyClient.SignData(SignatureAlgorithm.RS256, input).Signature;
        }

        public override bool Verify(byte[] input, byte[] signature)
        {
            if (!string.Equals(Algorithm, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
            {
                return false;
            }

            return _key.CryptographyClient.VerifyData(SignatureAlgorithm.RS256, input, signature).IsValid;
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
