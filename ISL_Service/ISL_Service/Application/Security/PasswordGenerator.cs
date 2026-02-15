using System.Security.Cryptography;

namespace ISL_Service.Application.Security;

public static class PasswordGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*-_";

    public static string Generate(int length)
    {
        if (length < 8) throw new ArgumentOutOfRangeException(nameof(length));

        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];

        for (int i = 0; i < length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];

        return new string(chars);
    }
}
