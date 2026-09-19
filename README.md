# shareman

A utility that uploads files to Google Drive, generates a public link, and sends it to Telegram recipients from your personal user account.

Integrates with the Windows Explorer **Send to** context menu for one-click sharing.


## Daily Usage

1. In Windows Explorer, right-click the file/video you want to share.
2. Select **Send to** -> **shareman**.
3. Pick a chat from the list. Optionally add a comment.
4. Click **Upload to Drive and send**.
5. The program uploads the file in the background, sets link permissions to `anyone with the link`, delivers the message to the recipient, and closes.

You can also drag and drop files directly into the application window.


## Initial Setup

### 1. Connect Telegram Account

1. Sign in to [my.telegram.org](https://my.telegram.org) with your phone number.
2. Open the **API development tools** section.
3. Create an application (for example, title `shareman`, platform Desktop).
4. Copy your **App api_id** and **App api_hash** values.
5. In the app settings, paste both values, enter your phone number, and click **Log in to Telegram**.
6. Enter the verification code sent to your Telegram client.
7. Once connected, your account name appears. The session persists in local storage, so subsequent launches do not require re-authenticating.


### 2. Connect Google Drive

1. Open the [Google Cloud Console](https://console.cloud.google.com/) and create a project (or select an existing one).
2. Under **APIs & Services** -> **Library**, find and enable the **Google Drive API**.
3. Under **APIs & Services** -> **OAuth consent screen**:
   - Select the user type **External**.
   - Fill in an application name and a user support email.
   - In the **Test users** section, add your Google account email.
4. Under **APIs & Services** -> **Credentials**:
   - Click **Create Credentials** -> **OAuth client ID**.
   - Choose **Desktop app**.
   - Download the generated JSON credentials file `client_secret_...json`.
5. In the application settings tab, click **Load client_secret.json** to populate the fields automatically.
6. Click **Authorize Google Drive** and approve access in the browser window that opens.


### 3. Register the Context Menu Shortcut

Under the Windows shell settings, click **Add to "Send to" Menu**. This creates a shortcut in `%APPDATA%\Microsoft\Windows\SendTo`.

**shareman** will now appear in your Windows Explorer right-click menu under **Send to**.


## Stored Credentials

Local data is kept in:
`UserData\` (in the application directory)

- `settings.json`: Application preferences and API configuration
- `telegram.session`: WTelegramClient user session
- `google_tokens\`: Cached OAuth tokens