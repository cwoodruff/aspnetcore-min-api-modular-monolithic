using Microsoft.IdentityModel.Tokens;

namespace Identity.Modules.KeyManagement;

public interface IKeyMaterialService
{
    SigningCredentials GetCurrentSigningCredentials();
    IEnumerable<SecurityKey> GetValidationKeys();
    string GetCurrentKeyId();
    object GetJwksDocument();
}
