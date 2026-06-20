using System.Security.Cryptography;

namespace MovieNightPicker.Api.Auth;

/// <summary>
/// Salted PBKDF2 password hashing (HMAC-SHA256), no external dependency. The hash
/// string packs the iteration count, salt, and derived key so <see cref="Verify"/>
/// is self-describing and survives parameter changes.
/// </summary>
public sealed class PasswordHasher
{
    private const int SaltSize = 16;       // 128-bit salt
    private const int KeySize = 32;        // 256-bit derived key
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>Hashes a password to a <c>{iterations}.{salt}.{key}</c> (base64) string.</summary>
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Join('.', Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    /// <summary>Verifies a candidate password against a stored hash, constant-time on the key.</summary>
    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] key;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            key = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var candidate = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, key.Length);
        return CryptographicOperations.FixedTimeEquals(candidate, key);
    }

    /// <summary>
    /// True when the stored hash was produced with fewer iterations than the current
    /// <see cref="Iterations"/> cost — i.e. it should be re-hashed (e.g. on a successful
    /// login) to migrate it to the stronger parameters. Unparseable hashes also return
    /// true so a malformed/legacy hash gets replaced.
    /// </summary>
    public bool NeedsRehash(string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return true;
        }

        return iterations < Iterations;
    }
}
