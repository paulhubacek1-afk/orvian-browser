using System.Security.Cryptography;
using System.Text.Json;

namespace Orvian.Browser;

public sealed record SavedCredential(string Site, string Username, string Password);

public sealed class PasswordVault
{
    private readonly string _file = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Orvian", "vault.dat");

    public void Save(SavedCredential credential)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var plain = JsonSerializer.SerializeToUtf8Bytes(credential);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        CryptographicOperations.ZeroMemory(plain);
        File.WriteAllBytes(_file, protectedBytes);
    }

    public SavedCredential? Load()
    {
        if (!File.Exists(_file)) return null;
        var protectedBytes = File.ReadAllBytes(_file);
        var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        try
        {
            return JsonSerializer.Deserialize<SavedCredential>(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }
}
