using Microsoft.IdentityModel.Tokens;

namespace Identity.Modules.KeyManagement;

internal interface IKeyMaterialService
{
    SigningCredentials GetCurrentSigningCredentials();
    IEnumerable<SecurityKey> GetValidationKeys();
    string GetCurrentKeyId();
    object GetJwksDocument();
}
