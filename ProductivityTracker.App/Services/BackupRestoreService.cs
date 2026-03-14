using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ProductivityTracker.App.Services;

public sealed class BackupRestoreService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PTBK1");

    public string CreateBackupEncrypted(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Backup password is required in Settings.");
        }

        LocalPaths.EnsureAll();
        string outPath = LocalPaths.GetBackupFile(DateTime.Now);

        byte[] zipBytes = BuildZipBytes();
        byte[] encrypted = Encrypt(zipBytes, password);
        File.WriteAllBytes(outPath, encrypted);
        return outPath;
    }

    public void RestoreBackup(string backupFilePath, string password)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("Backup file not found.", backupFilePath);
        }

        LocalPaths.EnsureAll();

        byte[] raw = File.ReadAllBytes(backupFilePath);
        byte[] zipBytes = IsEncrypted(raw)
            ? Decrypt(raw, password)
            : raw;

        using var ms = new MemoryStream(zipBytes);
        using ZipArchive archive = new(ms, ZipArchiveMode.Read, leaveOpen: false);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName))
            {
                continue;
            }

            string destination = Path.Combine(LocalPaths.DataRoot, entry.FullName);
            string? dir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
            {
                continue;
            }

            entry.ExtractToFile(destination, overwrite: true);
        }
    }

    private static byte[] BuildZipBytes()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddFileIfExists(archive, LocalPaths.TasksFile, "tasks.json");
            AddFileIfExists(archive, LocalPaths.SettingsFile, "settings.json");
            AddDirectoryIfExists(archive, LocalPaths.ActivityDirectory, "Activity");
            AddDirectoryIfExists(archive, LocalPaths.ReportsDirectory, "Reports");
            AddDirectoryIfExists(archive, LocalPaths.ScreenshotsDirectory, "Screenshots");
        }

        return ms.ToArray();
    }

    private static bool IsEncrypted(byte[] data)
    {
        if (data.Length < Magic.Length)
        {
            return false;
        }

        for (int i = 0; i < Magic.Length; i++)
        {
            if (data[i] != Magic[i])
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] Encrypt(byte[] plain, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] iv = RandomNumberGenerator.GetBytes(16);

        using var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] key = kdf.GetBytes(32);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encMs = new MemoryStream();
        using (var crypto = new CryptoStream(encMs, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            crypto.Write(plain, 0, plain.Length);
            crypto.FlushFinalBlock();
        }

        byte[] cipher = encMs.ToArray();

        using var outMs = new MemoryStream();
        outMs.Write(Magic);
        outMs.Write(salt);
        outMs.Write(iv);
        outMs.Write(cipher);
        return outMs.ToArray();
    }

    private static byte[] Decrypt(byte[] payload, string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Backup password is required to restore encrypted backup.");
        }

        int offset = Magic.Length;
        byte[] salt = payload[offset..(offset + 16)];
        offset += 16;
        byte[] iv = payload[offset..(offset + 16)];
        offset += 16;
        byte[] cipher = payload[offset..];

        using var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        byte[] key = kdf.GetBytes(32);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var cipherMs = new MemoryStream(cipher);
        using var crypto = new CryptoStream(cipherMs, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var plainMs = new MemoryStream();
        crypto.CopyTo(plainMs);
        return plainMs.ToArray();
    }

    private static void AddFileIfExists(ZipArchive archive, string filePath, string entryName)
    {
        if (File.Exists(filePath))
        {
            archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
        }
    }

    private static void AddDirectoryIfExists(ZipArchive archive, string dirPath, string rootEntry)
    {
        if (!Directory.Exists(dirPath))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dirPath, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(dirPath, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"{rootEntry}/{rel}", CompressionLevel.Optimal);
        }
    }
}
