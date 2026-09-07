using System.Security.Cryptography;
using System.Text.Json;

namespace Orvian.Browser;

public sealed record SavedCredential(string Site, string Username, string Password);

public sealed class PasswordVault
{
    private readonly string _file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orvian", "vault.dat");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public IReadOnlyList<SavedCredential> LoadAll()
    {
        if (!File.Exists(_file)) return [];
        try
        {
            var protectedBytes = File.ReadAllBytes(_file);
            try
            {
                var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                try
                {
                    var list = JsonSerializer.Deserialize<List<SavedCredential>>(plain, Json);
                    if (list != null) return list;

                    var legacy = JsonSerializer.Deserialize<SavedCredential>(plain, Json);
                    return legacy == null ? [] : [legacy];
                }
                finally { CryptographicOperations.ZeroMemory(plain); }
            }
            finally { CryptographicOperations.ZeroMemory(protectedBytes); }
        }
        catch { return []; }
    }

    public void Save(SavedCredential credential)
    {
        if (string.IsNullOrWhiteSpace(credential.Site) || string.IsNullOrWhiteSpace(credential.Username))
            throw new ArgumentException("Website und Benutzername sind erforderlich.");

        var entries = LoadAll().Where(x => !x.Site.Equals(credential.Site, StringComparison.OrdinalIgnoreCase) || !x.Username.Equals(credential.Username, StringComparison.OrdinalIgnoreCase)).ToList();
        entries.Insert(0, credential);
        Write(entries);
    }

    public void Delete(SavedCredential credential)
    {
        var entries = LoadAll().Where(x => !(x.Site.Equals(credential.Site, StringComparison.OrdinalIgnoreCase) && x.Username.Equals(credential.Username, StringComparison.OrdinalIgnoreCase))).ToList();
        Write(entries);
    }

    public SavedCredential? Load() => LoadAll().FirstOrDefault();

    private void Write(IReadOnlyList<SavedCredential> credentials)
    {
        var directory = Path.GetDirectoryName(_file);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Der Passwort-Tresorpfad konnte nicht ermittelt werden.");
        Directory.CreateDirectory(directory);

        var plain = JsonSerializer.SerializeToUtf8Bytes(credentials, Json);
        try
        {
            var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            try
            {
                var temp = _file + ".tmp";
                File.WriteAllBytes(temp, protectedBytes);
                File.Move(temp, _file, true);
            }
            finally { CryptographicOperations.ZeroMemory(protectedBytes); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
}
