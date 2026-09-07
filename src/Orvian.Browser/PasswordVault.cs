using System.IO;
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
        var directory = Path.GetDirectoryName(_file);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Der Passwort-Tresorpfad konnte nicht ermittelt werden.");

        Directory.CreateDirectory(directory);

        var plain = JsonSerializer.SerializeToUtf8Bytes(credential);
        try
        {
            var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            try
            {
                File.WriteAllBytes(_file, protectedBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public SavedCredential? Load()
    {
        if (!File.Exists(_file)) return null;

        var protectedBytes = File.ReadAllBytes(_file);
        try
        {
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
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }
}
