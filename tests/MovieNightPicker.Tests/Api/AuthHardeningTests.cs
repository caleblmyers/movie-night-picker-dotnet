using System.Security.Cryptography;
using MovieNightPicker.Api.Auth;

namespace MovieNightPicker.Tests.Api;

/// <summary>
/// Auth hardening (Wave-5): password-hash cost migration via
/// <see cref="PasswordHasher.NeedsRehash"/>. The auth rate-limiter wiring is exercised
/// end-to-end through the host in <see cref="AuthFlowTests"/>.
/// </summary>
public class AuthHardeningTests
{
    /// <summary>The hasher's current iteration count (private const mirror for the tests).</summary>
    private const int CurrentIterations = 100_000;

    /// <summary>Crafts a valid <c>{iterations}.{salt}.{key}</c> hash at an arbitrary cost.</summary>
    private static string MakeHash(string password, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return string.Join('.', iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    [Fact]
    public void NeedsRehash_true_for_below_current_iteration_count()
    {
        var hasher = new PasswordHasher();

        var weak = MakeHash("password123", CurrentIterations / 2);

        Assert.True(hasher.NeedsRehash(weak));
    }

    [Fact]
    public void NeedsRehash_false_for_current_parameter_hash()
    {
        var hasher = new PasswordHasher();

        // Hash() always uses the current iteration count.
        var current = hasher.Hash("password123");

        Assert.False(hasher.NeedsRehash(current));
    }

    [Fact]
    public void NeedsRehash_true_for_malformed_hash()
    {
        var hasher = new PasswordHasher();

        Assert.True(hasher.NeedsRehash("not-a-valid-hash"));
        Assert.True(hasher.NeedsRehash("abc.def.ghi"));
    }

    [Fact]
    public void Rehashed_password_still_verifies()
    {
        var hasher = new PasswordHasher();

        var weak = MakeHash("password123", CurrentIterations / 2);
        Assert.True(hasher.NeedsRehash(weak));

        // A weak hash still verifies (self-describing iteration count), and re-hashing
        // yields one that no longer needs migration.
        Assert.True(hasher.Verify("password123", weak));
        var migrated = hasher.Hash("password123");
        Assert.False(hasher.NeedsRehash(migrated));
        Assert.True(hasher.Verify("password123", migrated));
    }
}
