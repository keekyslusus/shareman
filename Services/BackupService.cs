using System;
using System.IO;
using System.IO.Compression;

namespace GDriveTelegramSender.Services;

public record BackupValidationResult(bool IsValid, bool HasSettings, bool HasTelegramSession, bool HasGoogleTokens, string? ErrorMessage);

public static class BackupService
{
    public const string BackupFileExtension = ".gdtbak";
    public const string DialogFilter = "shareman Backup (*.gdtbak)|*.gdtbak|Zip Archive (*.zip)|*.zip|All Files (*.*)|*.*";

    public static void CreateBackup(string destinationFilePath)
    {
        string dataDir = SettingsService.Instance.DataDirectory;
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        string? targetDir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string tempZip = Path.Combine(Path.GetTempPath(), $"gdtbak_{Guid.NewGuid():N}.tmp");
        try
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }

            using (var zipFileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, false))
            {
                var dirInfo = new DirectoryInfo(dataDir);
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(dataDir, file.FullName).Replace('\\', '/');
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);

                    using var sourceStream = OpenReadWithRetry(file.FullName);
                    using var entryStream = entry.Open();
                    sourceStream.CopyTo(entryStream);
                }
            }

            if (File.Exists(destinationFilePath))
            {
                File.Delete(destinationFilePath);
            }

            File.Move(tempZip, destinationFilePath);
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
    }

    private static FileStream OpenReadWithRetry(string filePath, int maxRetries = 3, int delayMs = 150)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                System.Threading.Thread.Sleep(delayMs);
            }
        }
        return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    public static BackupValidationResult ValidateBackup(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
        {
            return new BackupValidationResult(false, false, false, false, "File not found.");
        }

        try
        {
            using var archive = ZipFile.OpenRead(backupFilePath);
            bool hasSettings = false;
            bool hasSession = false;
            bool hasTokens = false;

            foreach (var entry in archive.Entries)
            {
                string name = entry.FullName.Replace('\\', '/').ToLowerInvariant();
                if (name == "settings.json") hasSettings = true;
                if (name.StartsWith("telegram.session")) hasSession = true;
                if (name.StartsWith("google_tokens/")) hasTokens = true;
            }

            bool isValid = hasSettings || hasSession || hasTokens;
            return new BackupValidationResult(
                isValid,
                hasSettings,
                hasSession,
                hasTokens,
                isValid ? null : "The archive does not contain application data.");
        }
        catch (Exception ex)
        {
            return new BackupValidationResult(false, false, false, false, ex.Message);
        }
    }

    public static void RestoreBackup(string backupFilePath)
    {
        var validation = ValidateBackup(backupFilePath);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.ErrorMessage ?? "Invalid backup file.");
        }

        string dataDir = SettingsService.Instance.DataDirectory;
        string tempExtractDir = Path.Combine(Path.GetTempPath(), $"gdt_restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempExtractDir);

        try
        {
            ZipFile.ExtractToDirectory(backupFilePath, tempExtractDir, true);

            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            CopyDirectoryRecursive(tempExtractDir, dataDir);

            SettingsService.Instance.EnsureUserDataEncrypted();
        }
        finally
        {
            if (Directory.Exists(tempExtractDir))
            {
                try { Directory.Delete(tempExtractDir, true); } catch { }
            }
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSubDir);
        }
    }
}
