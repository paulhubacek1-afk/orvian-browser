using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Orvian.Browser;

public sealed record SavedCredential(string Site, string Username, string Password);

public sealed class PasswordVault
{
    private readonly string _file = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Orvian", "vault.dat");

    public IReadOnlyList<SavedCredential> LoadAll()
    {
        if (!File.Exists(_file)) return [];

        var protectedBytes = File.ReadAllBytes(_file);
        try
        {
            var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            try
            {
                try
                {
                    return JsonSerializer.Deserialize<List<SavedCredential>>(plain) ?? [];
                }
                catch (JsonException)
                {
                    // Compatibility with the old single-entry vault format.
                    var old = JsonSerializer.Deserialize<SavedCredential>(plain);
                    return old is null ? [] : [old];
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException("Der Passwort-Tresor kann mit dem aktuellen Windows-Benutzer nicht entschlüsselt werden.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public void Save(SavedCredential credential)
    {
        var items = LoadAll().ToList();
        var index = items.FindIndex(x =>
            x.Site.Equals(credential.Site, StringComparison.OrdinalIgnoreCase) &&
            x.Username.Equals(credential.Username, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) items[index] = credential;
        else items.Add(credential);
        SaveAll(items);
    }

    public void Delete(SavedCredential credential)
    {
        var items = LoadAll().Where(x =>
            !(x.Site.Equals(credential.Site, StringComparison.OrdinalIgnoreCase) &&
              x.Username.Equals(credential.Username, StringComparison.OrdinalIgnoreCase))).ToList();
        SaveAll(items);
    }

    private void SaveAll(IReadOnlyList<SavedCredential> credentials)
    {
        var directory = Path.GetDirectoryName(_file);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Der Passwort-Tresorpfad konnte nicht ermittelt werden.");
        Directory.CreateDirectory(directory);

        var plain = JsonSerializer.SerializeToUtf8Bytes(credentials);
        try
        {
            var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            try
            {
                var temp = _file + ".tmp";
                File.WriteAllBytes(temp, protectedBytes);
                File.Move(temp, _file, true);
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

    public SavedCredential? Load() => LoadAll().FirstOrDefault();
}
