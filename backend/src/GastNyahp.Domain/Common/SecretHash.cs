using System.Security.Cryptography;
using System.Text;

namespace GastNyahp.Domain.Common;

/// <summary>
/// Tokens and invite codes are NEVER persisted raw (DOMAIN_MODEL.md §17.1): events and read models carry the
/// SHA-256 hex only; the raw value is returned exactly once at issuance. Possession of the raw value is what
/// grants access — the modern translation of "possession of the JSON file" from the original app.
/// </summary>
public static class SecretHash
{
    public static string Compute(string rawSecret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawSecret)));
}
