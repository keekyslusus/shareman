using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GDriveTelegramSender.Models;

namespace GDriveTelegramSender.Services;

public enum SendWorkflowStage
{
    Preparing,
    UploadingToDrive,
    SendingTelegram,
    Completed
}

public record SendWorkflowProgress(
    SendWorkflowStage Stage,
    UploadProgressInfo? UploadInfo = null
);

public class SendWorkflowCoordinator
{
    private readonly GoogleDriveService _driveService;
    private readonly TelegramClientService _telegramService;
    private readonly SettingsService _settingsService;

    public SendWorkflowCoordinator(
        GoogleDriveService driveService,
        TelegramClientService telegramService,
        SettingsService settingsService)
    {
        _driveService = driveService ?? throw new ArgumentNullException(nameof(driveService));
        _telegramService = telegramService ?? throw new ArgumentNullException(nameof(telegramService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public bool IsDriveConfigured => _driveService.IsConfigured;
    public bool IsTelegramConfigured => _telegramService.IsConfigured && _telegramService.HasSessionFile;

    public async Task<string> ExecuteAsync(
        string filePath,
        TelegramChatItem recipient,
        string comment,
        IProgress<SendWorkflowProgress>? progress,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("Selected file does not exist.", filePath);
        }

        if (recipient == null || recipient.Peer == null)
        {
            throw new ArgumentNullException(nameof(recipient), "Recipient peer cannot be null.");
        }

        if (!_driveService.IsConfigured)
        {
            throw new InvalidOperationException("Google Drive is not configured.");
        }

        // 1. Preparing
        progress?.Report(new SendWorkflowProgress(SendWorkflowStage.Preparing));

        var uploadProgress = new Progress<UploadProgressInfo>(info =>
        {
            progress?.Report(new SendWorkflowProgress(SendWorkflowStage.UploadingToDrive, info));
        });

        // 2. Upload to Google Drive
        string publicLink = await _driveService.UploadAndShareAsync(filePath, uploadProgress, ct);

        // 3. Format & Send via Telegram
        progress?.Report(new SendWorkflowProgress(SendWorkflowStage.SendingTelegram));

        string fileName = Path.GetFileName(filePath);
        var (messageToSend, entities) = TelegramClientService.FormatFileMessage(comment.Trim(), fileName, publicLink);

        await _telegramService.SendMessageAsync(recipient.Peer, messageToSend, entities);

        // 4. Completed
        progress?.Report(new SendWorkflowProgress(SendWorkflowStage.Completed));

        return publicLink;
    }
}
