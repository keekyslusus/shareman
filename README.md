# Google Drive to Telegram Share (Windows SendTo)

A C# (.NET 9 WPF) utility that uploads files to Google Drive, generates a public link, and sends it to Telegram recipients from your personal user account.

Integrates with the Windows Explorer **Send to** context menu for one-click sharing.

---

## Daily Usage

1. In Windows Explorer, right-click the file or video you want to share.
2. Select **Send to** > **Google Drive & Telegram**.
3. In the application dialog, pick a contact or chat from the list (or search by name). Optionally add a text note.
4. Click **Upload to Drive and send** (`Загрузить на Drive и отправить`).
5. The program uploads the file in the background, sets link permissions to "anyone with the link", delivers the message to the recipient, and closes.

You can also drag and drop files directly into the application window.

---

## Initial Setup

### 1. Launch Settings

Run `GDriveTelegramSender.exe` from `bin\Release\net9.0-windows\win-x64\publish\` or start the project with `dotnet run`. If credentials are not yet configured, the application opens directly to the **Settings** tab.

---

### 2. Connect Telegram Account

1. Sign in to [my.telegram.org](https://my.telegram.org) with your phone number.
2. Open the **API development tools** section.
3. Create an application if needed (for example, title `DriveShareApp`, platform Desktop).
4. Copy your **App api_id** and **App api_hash** values.
5. In the app settings, paste both values into their respective fields, enter your phone number in international format, and click **Log in to Telegram** (`Войти в Telegram`).
6. Enter the verification code sent to your Telegram client (and your two-step verification password if enabled).
7. Once connected, your account name appears in green. The session persists in local storage, so subsequent launches do not require re-authenticating.

---

### 3. Connect Google Drive

1. Open the [Google Cloud Console](https://console.cloud.google.com/) and create a project (or select an existing one).
2. Under **APIs & Services** > **Library**, find and enable the **Google Drive API**.
3. Under **APIs & Services** > **OAuth consent screen**:
   - Select user type **External**.
   - Fill in an application name and user support email.
   - In the **Test users** section, add your Google account email.
4. Under **APIs & Services** > **Credentials**:
   - Click **Create Credentials** > **OAuth client ID**.
   - Choose **Desktop app**.
   - Download the generated JSON credentials file (`client_secret_...json`).
5. In the application settings tab, click **Load client_secret.json** (`📄 Загрузить client_secret.json`) to populate the fields automatically.
6. Click **Authorize Google Drive** (`🔑 Авторизовать Google Drive`) and approve access in the browser window that opens.

---

### 4. Register the Context Menu Shortcut

Under the Windows shell settings, click **Add to "Send to" Menu** (`➕ Добавить в меню «Отправить»`).

This creates a shortcut in `%APPDATA%\Microsoft\Windows\SendTo`. **Google Drive & Telegram** will now appear in your Windows Explorer right-click menu under **Send to**.

---

## Stored Credentials

Local data is kept in:
`%APPDATA%\GDriveTelegramSender\`

- `settings.json`: Application preferences and API configuration
- `telegram.session`: WTelegramClient user session
- `google_tokens\`: Cached OAuth tokens

