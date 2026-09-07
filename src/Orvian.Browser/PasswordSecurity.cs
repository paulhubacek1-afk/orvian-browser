using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Orvian.Browser;

public static class PasswordSecurity
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<string> CheckPwnedAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(password))
            return "Kein Passwort eingegeben.";

        using var sha1 = SHA1.Create();
        var hash = Convert.ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(password)));
        var prefix = hash[..5];
        var suffix = hash[5..];

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.pwnedpasswords.com/range/{prefix}");

        request.Headers.UserAgent.ParseAdd("Orvian-Browser/1.0");
        request.Headers.Add("Add-Padding", "true");

        using var response = await Http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Trim().Split(':', 2);

            if (parts.Length == 2 &&
                parts[0].Equals(suffix, StringComparison.OrdinalIgnoreCase) &&
                long.TryParse(parts[1], out var count))
            {
                return $"⚠ Dieses Passwort wurde in Datenlecks gefunden ({count:N0} Treffer). Bitte nicht weiterverwenden.";
            }
        }

        return "✓ Dieses Passwort wurde in der abgefragten Pwned-Passwords-Datenbank nicht gefunden.";
    }
}
