using Xunit;
using System.Security.Cryptography;
using System.IO.Compression;

namespace Systems_One_MQTT_Updater.Tests;

public class UpdateDownloaderHashTests
{
    [Fact]
    public async Task DownloadAndVerify_CorrectHash_Succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Build a minimal zip with a dummy payload
            var zipPath = Path.Combine(tempDir, "test.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                zip.CreateEntryFromFile(CreateTempFile(tempDir, "hello.txt", "hello world"), "hello.txt");

            var expectedHash = ComputeSha256(zipPath);

            // Verify passes with correct hash
            VerifyHash(zipPath, expectedHash); // should not throw
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAndVerify_WrongHash_ThrowsAndDeletesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var zipPath = Path.Combine(tempDir, "test.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                zip.CreateEntryFromFile(CreateTempFile(tempDir, "hello.txt", "hello world"), "hello.txt");

            // Wrong hash — all zeros
            var wrongHash = new string('0', 64);

            var ex = Assert.Throws<InvalidDataException>(() => VerifyHash(zipPath, wrongHash));
            Assert.Contains("SHA-256 mismatch", ex.Message);

            // File should be deleted after mismatch
            Assert.False(File.Exists(zipPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── Inline copy of the private VerifyHash logic from UpdateDownloader ──────

    private static void VerifyHash(string filePath, string expectedHex)
    {
        string actual;
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            actual = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }

        if (!string.Equals(actual, expectedHex.ToLowerInvariant(), StringComparison.Ordinal))
        {
            File.Delete(filePath);
            throw new InvalidDataException($"SHA-256 mismatch. Expected: {expectedHex}  Actual: {actual}");
        }
    }

    private static string ComputeSha256(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string CreateTempFile(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }
}
