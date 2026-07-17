using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace GastNyahp.Api.Auth;

public sealed record OAuthClient(string ClientId, IReadOnlyList<string> RedirectUris, DateTimeOffset CreatedAt);
public sealed record OAuthAuthorizationCode(
    string ClientId, string RedirectUri, string CodeChallenge, string? Resource,
    string AccessToken, DateTimeOffset ExpiresAt);

public sealed class OAuthFlowStore
{
    readonly ConcurrentDictionary<string, OAuthClient> _clients = new();
    readonly ConcurrentDictionary<string, OAuthAuthorizationCode> _codes = new();

    public OAuthClient Register(IEnumerable<string> redirectUris)
    {
        var uris = redirectUris.Distinct(StringComparer.Ordinal).ToArray();
        if (uris.Length == 0 || uris.Any(uri => !IsSafeRedirectUri(uri)))
            throw new ArgumentException("redirect_uris contiene una URL inválida.");

        var client = new OAuthClient(NewSecret(24), uris, DateTimeOffset.UtcNow);
        _clients[client.ClientId] = client;
        return client;
    }

    public bool IsRedirectAllowed(string clientId, string redirectUri) =>
        _clients.TryGetValue(clientId, out var client) &&
        client.RedirectUris.Contains(redirectUri, StringComparer.Ordinal);

    public string IssueCode(string clientId, string redirectUri, string challenge, string? resource, string accessToken)
    {
        PurgeExpired();
        var raw = NewSecret(32);
        _codes[Hash(raw)] = new OAuthAuthorizationCode(
            clientId, redirectUri, challenge, resource, accessToken, DateTimeOffset.UtcNow.AddMinutes(5));
        return raw;
    }

    public OAuthAuthorizationCode? RedeemCode(string rawCode, string clientId, string redirectUri, string verifier)
    {
        if (!_codes.TryRemove(Hash(rawCode), out var code)) return null;
        if (code.ExpiresAt <= DateTimeOffset.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(code.ClientId), Encoding.UTF8.GetBytes(clientId)) ||
            !string.Equals(code.RedirectUri, redirectUri, StringComparison.Ordinal) ||
            !VerifyPkce(verifier, code.CodeChallenge))
            return null;
        return code;
    }

    static bool IsSafeRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttps ||
               (uri.Scheme == Uri.UriSchemeHttp && (uri.Host == "localhost" || uri.IsLoopback));
    }

    static bool VerifyPkce(string verifier, string expectedChallenge)
    {
        if (string.IsNullOrWhiteSpace(verifier)) return false;
        var actual = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expectedChallenge));
    }

    void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in _codes.Where(x => x.Value.ExpiresAt <= now))
            _codes.TryRemove(item.Key, out _);
    }

    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static string NewSecret(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));
    static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}