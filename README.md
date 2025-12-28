# MyBlog

A lightweight, self-hosted blogging platform built with .NET 10 and Blazor Server.

## Features

- **Markdown-based content**: Write posts in Markdown with live preview
- **Image management**: Upload and manage images stored in the database
- **Admin dashboard**: Manage posts, images, and settings
- **OpenTelemetry**: Built-in observability with file-based telemetry export
- **Cross-platform**: Runs on Windows, Linux, and macOS
- **CI/CD ready**: GitHub Actions workflow for automated testing and deployment

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

### Running Locally

```bash
# Clone the repository
git clone https://github.com/yourusername/dotnetcms.git
cd dotnetcms/src

# Restore and run
dotnet restore MyBlog.slnx
cd MyBlog.Web
dotnet run
```

The application will start at `http://localhost:5000` (or the next available port).

### Default Credentials

- **Username**: `admin`
- **Password**: `ChangeMe123!` (or value of `MYBLOG_ADMIN_PASSWORD` environment variable)

> **Important**: The default password is only used when creating the initial admin user. Once the user exists, you must change the password through the website.

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `MYBLOG_ADMIN_PASSWORD` | Initial admin password (only used on first run) | `ChangeMe123!` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=myblog.db"
  },
  "Authentication": {
    "SessionTimeoutMinutes": 30,
    "DefaultAdminPassword": "ChangeMe123!"
  },
  "Application": {
    "Title": "MyBlog"
  }
}
```

### Database Location

The SQLite database is stored in a platform-specific location:

| Platform | Path |
|----------|------|
| Linux | `~/.local/share/MyBlog/myblog.db` |
| macOS | `~/Library/Application Support/MyBlog/myblog.db` |
| Windows | `%LOCALAPPDATA%\MyBlog\myblog.db` |

## Admin Features

### Dashboard (`/admin`)

The admin dashboard provides an overview of your blog with quick access to all management features.

### Managing Posts

- **Create Post** (`/admin/posts/new`): Write a new blog post in Markdown
- **Edit Post** (`/admin/posts/edit/{id}`): Modify existing posts
- **Post List** (`/admin/posts`): View and manage all posts

### Managing Images

- **Upload Images** (`/admin/images`): Upload images to use in posts
- **Image Library**: Browse and delete uploaded images
- **Usage**: Reference images in Markdown using `/api/images/{id}`

### Changing Your Password

Navigate to `/admin/change-password` to change your admin password:

1. Enter your current password
2. Enter your new password (minimum 8 characters)
3. Confirm the new password
4. Click "Change Password"

> **Note**: The `MYBLOG_ADMIN_PASSWORD` environment variable only affects the initial password when the admin user is first created. It does not override existing passwords.

## Deployment

### GitHub Actions (Automated)

The repository includes a GitHub Actions workflow that:

1. Builds and tests on Windows, Linux, and macOS
2. Deploys to your server via WebDeploy (on main/master/develop branches)

#### Required Secrets

Set these in your repository settings under **Settings > Secrets and variables > Actions**:

| Secret | Description | Example |
|--------|-------------|---------|
| `WEBSITE_NAME` | IIS site name | `MyBlog` |
| `SERVER_COMPUTER_NAME` | Server hostname | `myserver.example.com` |
| `SERVER_USERNAME` | WebDeploy username | `deploy-user` |
| `SERVER_PASSWORD` | WebDeploy password | (your password) |

#### Repository Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `MYBLOG_ADMIN_PASSWORD` | Initial admin password | (strong password) |

> **Note**: `MYBLOG_ADMIN_PASSWORD` should be set as a **secret**, not a variable, if you want it to remain hidden in logs.

### Manual Deployment

```bash
# Publish for Windows
dotnet publish src/MyBlog.Web/MyBlog.Web.csproj -c Release -o ./publish -r win-x64

# Publish for Linux
dotnet publish src/MyBlog.Web/MyBlog.Web.csproj -c Release -o ./publish -r linux-x64
```

Copy the contents of `./publish` to your server.

### IIS Configuration

1. Install the [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Create a new IIS site pointing to your publish folder
3. Set the Application Pool to "No Managed Code"
4. Ensure the Application Pool identity has write access to the database directory

## Troubleshooting

### ERROR_FILE_IN_USE During Deployment

This occurs when the application is running and DLLs are locked.

**Solution**: The workflow now includes `-enableRule:AppOffline` which automatically:
1. Creates `app_offline.htm` to stop the application
2. Waits for the app to release file locks
3. Deploys the new files
4. Removes `app_offline.htm` to restart the app

### Password Not Changing After Setting MYBLOG_ADMIN_PASSWORD

The environment variable only works when **no users exist** in the database.

**To reset with a new password**:

1. Stop the application
2. Delete the database file (see Database Location above)
3. Set `MYBLOG_ADMIN_PASSWORD` to your desired password
4. Start the application

Or, log in with the current password and use `/admin/change-password`.

### Database Locked Errors

SQLite can have locking issues with concurrent access.

**Solutions**:
- Ensure only one instance of the application is running
- Check that no database tools have the file open
- Verify file permissions on the database directory

## Development

### Running Tests

```bash
cd src
dotnet test MyBlog.slnx
```

### Project Structure

```
src/
├── MyBlog.Core/           # Domain models and interfaces
├── MyBlog.Infrastructure/ # Data access, services
├── MyBlog.Web/           # Blazor Server application
└── MyBlog.Tests/         # xUnit test project
```

### Adding New Features

1. Define interfaces in `MyBlog.Core/Interfaces`
2. Implement in `MyBlog.Infrastructure/Services`
3. Add UI in `MyBlog.Web/Components/Pages`
4. Write tests in `MyBlog.Tests`

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/images/{id}` | GET | Retrieve an image by ID |

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
