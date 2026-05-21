using System.Security.Cryptography;
using System.Text;

namespace MapleClaude.Net.Session;

/// <summary>
/// Deterministic 16-byte client fingerprint: SHA-256 of the OS machine
/// hostname truncated to 16 bytes. The Kinoko server doesn't enforce any
/// specific format — it just compares the bytes you sent in CheckPassword
/// against the bytes you echo back in MigrateIn for binding.
/// </summary>
public static class MachineIdProvider
{
    public static byte[] Generate()
    {
        var seed = Environment.MachineName + "|" + Environment.UserName;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var id = new byte[16];
        Array.Copy(hash, id, 16);
        return id;
    }

    /// <summary>A stable 17-char hex string used as a fake MAC-address response.</summary>
    public static string GetFakeMacAddress()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("MapleClaude|MAC|" + Environment.MachineName));
        var sb = new StringBuilder(17);
        for (var i = 0; i < 6; i++)
        {
            if (i > 0)
            {
                sb.Append('-');
            }
            sb.Append(hash[i].ToString("X2"));
        }
        return sb.ToString();
    }

    /// <summary>A stable hex string used as a fake MAC+HDDSerial response.</summary>
    public static string GetFakeMacAddressWithHddSerial()
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("MapleClaude|MACHDD|" + Environment.MachineName));
        return GetFakeMacAddress() + "_" + Convert.ToHexString(hash, 0, 8);
    }
}
