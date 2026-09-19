using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;

namespace GDriveTelegramSender.Services;

public record UploadProgressInfo(int Percent, long BytesSent, long TotalBytes, double BytesPerSecond);

public class GoogleDriveService
{
    private readonly SettingsService _settingsService = SettingsService.Instance;
    private DriveService? _driveService;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settingsService.Settings.GoogleClientId) &&
        !string.IsNullOrWhiteSpace(_settingsService.Settings.GoogleClientSecret);

    public async Task<bool> IsAuthorizedAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return false;

        try
        {
            var dataStore = new EncryptedFileDataStore(_settingsService.GoogleTokensDirectory);
            var token = await dataStore.GetAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>("user");
            return token != null && (!string.IsNullOrEmpty(token.RefreshToken) || !token.IsStale);
        }
        catch
        {
            return false;
        }
    }

    private async Task<UserCredential> GetCredentialAsync(CancellationToken ct)
    {
        var secrets = new ClientSecrets
        {
            ClientId = _settingsService.Settings.GoogleClientId.Trim(),
            ClientSecret = _settingsService.Settings.GoogleClientSecret.Trim()
        };

        var dataStore = new EncryptedFileDataStore(_settingsService.GoogleTokensDirectory);

        var flow = new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow(new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = secrets,
            Scopes = new[] { DriveService.Scope.DriveFile, DriveService.Scope.DriveMetadataReadonly },
            DataStore = dataStore,
            Prompt = "select_account"
        });

        return await new Google.Apis.Auth.OAuth2.AuthorizationCodeInstalledApp(flow, new Google.Apis.Auth.OAuth2.LocalServerCodeReceiver())
            .AuthorizeAsync("user", ct);
    }

    public async Task<bool> AuthorizeAsync(CancellationToken ct = default)
    {
        try
        {
            var credential = await GetCredentialAsync(ct);
            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "shareman"
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Google Drive Auth Error: {ex.Message}");
            throw;
        }
    }

    private string? _cachedUserEmail;

    public async Task<string?> GetUserEmailAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(_cachedUserEmail)) return _cachedUserEmail;
        try
        {
            if (_driveService == null)
            {
                if (!await IsAuthorizedAsync(ct)) return null;
                var credential = await GetCredentialAsync(ct);
                _driveService = new DriveService(new BaseClientService.Initializer
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "shareman"
                });
            }

            var request = _driveService.About.Get();
            request.Fields = "user(displayName,emailAddress)";
            var about = await request.ExecuteAsync(ct);
            _cachedUserEmail = about.User?.EmailAddress ?? about.User?.DisplayName;
            return _cachedUserEmail;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get user info: {ex.Message}");
            return null;
        }
    }

    public void ResetAuthorization()
    {
        _cachedUserEmail = null;
        _driveService?.Dispose();
        _driveService = null;

        if (Directory.Exists(_settingsService.GoogleTokensDirectory))
        {
            try
            {
                Directory.Delete(_settingsService.GoogleTokensDirectory, true);
                Directory.CreateDirectory(_settingsService.GoogleTokensDirectory);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to reset Google tokens: {ex.Message}");
            }
        }
    }

    public async Task<string> UploadAndShareAsync(
        string filePath,
        IProgress<UploadProgressInfo>? progress = null,
        CancellationToken ct = default)
    {
        if (_driveService == null)
        {
            await AuthorizeAsync(ct);
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(Loc.Get("Exception_FileNotFound"), filePath);
        }

        var fileInfo = new FileInfo(filePath);
        var fileMetadata = new Google.Apis.Drive.v3.Data.File
        {
            Name = fileInfo.Name
        };

        string mimeType = GetMimeType(filePath);

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var request = _driveService!.Files.Create(fileMetadata, stream, mimeType);
        request.Fields = "id, name, webViewLink";

        var stopwatch = Stopwatch.StartNew();
        long lastBytes = 0;
        var speedStopwatch = Stopwatch.StartNew();

        request.ProgressChanged += uploadProgress =>
        {
            if (uploadProgress.Status == UploadStatus.Uploading)
            {
                long sent = uploadProgress.BytesSent;
                int percent = (int)((sent * 100) / fileInfo.Length);
                if (percent > 100) percent = 100;

                double elapsedSec = speedStopwatch.Elapsed.TotalSeconds;
                double speed = 0;
                if (elapsedSec >= 0.5)
                {
                    speed = (sent - lastBytes) / elapsedSec;
                    lastBytes = sent;
                    speedStopwatch.Restart();
                }

                progress?.Report(new UploadProgressInfo(percent, sent, fileInfo.Length, speed));
            }
        };

        var result = await request.UploadAsync(ct);

        if (result.Status != UploadStatus.Completed)
        {
            throw new Exception(Loc.Format("Exception_GDriveUploadFailed", result.Exception?.Message ?? result.Status.ToString()));
        }

        var uploaded = request.ResponseBody;
        if (uploaded == null || string.IsNullOrEmpty(uploaded.Id))
        {
            throw new Exception(Loc.Get("Exception_GDriveIdFailed"));
        }

        // Make file public ("anyone with the link can view")
        var permission = new Google.Apis.Drive.v3.Data.Permission
        {
            Role = "reader",
            Type = "anyone"
        };

        await _driveService.Permissions.Create(permission, uploaded.Id).ExecuteAsync(ct);

        string webViewLink = uploaded.WebViewLink;
        if (string.IsNullOrEmpty(webViewLink))
        {
            webViewLink = $"https://drive.google.com/file/d/{uploaded.Id}/view?usp=sharing";
        }

        return webViewLink;
    }

    private static string GetMimeType(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp4" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".webm" => "video/webm",
            ".wmv" => "video/x-ms-wmv",
            ".flv" => "video/x-flv",
            ".m4v" => "video/x-m4v",
            ".3gp" => "video/3gpp",
            ".ts" => "video/mp2t",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".rar" => "application/vnd.rar",
            _ => "application/octet-stream"
        };
    }
}
