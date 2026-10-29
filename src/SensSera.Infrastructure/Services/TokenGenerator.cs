using System.Security.Cryptography;
using System.Text;

namespace SensSera.Infrastructure.Services;

/// <summary>
/// Machine tokens for devices and nodes: <c>{id}.{secret}</c>. Only the SHA-256 hash of the secret
/// is stored; the id part lets the auth handler look the row up without scanning hashes.
/// </summary>
internal static class TokenGenerator
{
    public static (string RawToken, string TokenHash) Create(Guid id)
    {
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        return ($"{id}.{secret}", hash);
    }
}
