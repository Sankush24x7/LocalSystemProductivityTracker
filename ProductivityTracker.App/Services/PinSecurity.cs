using System.Security.Cryptography;
using System.Text;

namespace ProductivityTracker.App.Services;

public static class PinSecurity
{
    public static string HashPin(string pin)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(pin);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public static bool Verify(string pin, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string hash = HashPin(pin);
        return hash.Equals(storedHash, StringComparison.OrdinalIgnoreCase);
    }
}
