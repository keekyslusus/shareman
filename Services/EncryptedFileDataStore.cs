using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Util.Store;
using Newtonsoft.Json;

namespace GDriveTelegramSender.Services;

public class EncryptedFileDataStore : IDataStore
{
    private readonly string _folderPath;

    public EncryptedFileDataStore(string folderPath)
    {
        _folderPath = folderPath;
        if (!Directory.Exists(_folderPath))
        {
            Directory.CreateDirectory(_folderPath);
        }
    }

    private string GetFilePath(string key, Type t)
    {
        return Path.Combine(_folderPath, $"{t.FullName}-{key}");
    }

    public Task ClearAsync()
    {
        if (Directory.Exists(_folderPath))
        {
            foreach (var file in Directory.GetFiles(_folderPath))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to delete file {file}: {ex.Message}");
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(string key)
    {
        string filePath = GetFilePath(key, typeof(T));
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete file {filePath}: {ex.Message}");
            }
        }
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        string filePath = GetFilePath(key, typeof(T));
        if (!File.Exists(filePath))
        {
            return Task.FromResult<T?>(default);
        }

        try
        {
            byte[] raw = File.ReadAllBytes(filePath);
            string json;
            if (DataEncryptionService.IsEncrypted(raw))
            {
                byte[] decrypted = DataEncryptionService.Decrypt(raw);
                json = Encoding.UTF8.GetString(decrypted);
            }
            else
            {
                // Migrate legacy unencrypted token file immediately
                json = Encoding.UTF8.GetString(raw);
                try
                {
                    byte[] encrypted = DataEncryptionService.Encrypt(raw);
                    File.WriteAllBytes(filePath, encrypted);
                }
                catch { }
            }

            var result = JsonConvert.DeserializeObject<T>(json);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load encrypted token for {key}: {ex.Message}");
            return Task.FromResult<T?>(default);
        }
    }

    public Task StoreAsync<T>(string key, T value)
    {
        string filePath = GetFilePath(key, typeof(T));
        string json = JsonConvert.SerializeObject(value);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        byte[] encrypted = DataEncryptionService.Encrypt(jsonBytes);
        File.WriteAllBytes(filePath, encrypted);
        return Task.CompletedTask;
    }
}
