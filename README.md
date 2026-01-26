# MyBlog

> **⚠️ AI-Assisted Development Notice**
>
> This project contains code generated with assistance from Large Language Models (LLMs), including Claude (Anthropic) and Gemini (Google). All code—whether explicitly marked or not—should be considered experimental. The AI assistance was used for code generation, architecture decisions, debugging, documentation, and test creation. Human review and testing have been applied, but users should exercise appropriate caution when deploying to production environments.

A lightweight, self-hosted blogging platform built with .NET 10 and Blazor Server, following Clean Architecture principles.

**Live Demo:** [kush.runasp.net](https://kush.runasp.net)

---

## Table of Contents

1. [Overview](#overview)
2. [Features](#features)
3. [Architecture](#architecture)
4. [Technology Stack](#technology-stack)
5. [Quick Start](#quick-start)
6. [Configuration](#configuration)
7. [Database](#database)
8. [Authentication & Security](#authentication--security)
9. [Content Management](#content-management)
10. [Markdown Specification](#markdown-specification)
11. [Image Management](#image-management)
12. [Real-Time Features](#real-time-features)
13. [Theming System](#theming-system)
14. [Observability & Telemetry](#observability--telemetry)
15. [API Reference](#api-reference)
16. [Admin Guide](#admin-guide)
17. [Testing](#testing)
18. [Deployment](#deployment)
19. [Troubleshooting](#troubleshooting)
20. [Contributing](#contributing)
21. [License](#license)

---

## Overview

MyBlog is a complete content management system designed for developers who want full control over their blogging platform. It prioritizes simplicity, security, and self-sufficiency—requiring no external services, JavaScript frameworks, or CSS libraries.

### Design Philosophy

| Principle | Implementation |
|-----------|----------------|
| **Zero External Dependencies** | No npm, Node.js, or CSS frameworks. Pure .NET with custom CSS. |
| **Self-Contained** | Single deployable unit with SQLite database. No external DB servers. |
| **Cross-Platform** | Runs identically on Windows, Linux, and macOS. |
| **Security First** | Rate limiting, secure password hashing, input sanitization. |
| **Observable** | Built-in OpenTelemetry with file and database logging. |
| **Testable** | Interface-based DI with comprehensive unit and integration tests. |

---

## Features

### Content Features
- **Markdown Authoring** — Write posts in Markdown with live preview
- **Custom Markdown Parser** — No external libraries; supports all common syntax
- **Automatic Image Dimensions** — Fetches and caches dimensions to prevent layout shift
- **SEO Optimization** — Open Graph, Twitter Cards, JSON-LD structured data
- **Social Sharing** — Web Share API with clipboard fallback

### Security Features
- **Progressive Rate Limiting** — Slows down brute-force attacks but never locks users out
- **Secure Password Storage** — ASP.NET Identity PasswordHasher with automatic rehashing
- **Slug Collision Prevention** — Automatic unique slug generation
- **Cookie-Based Sessions** — HttpOnly, secure cookies with sliding expiration

### Administrative Features
- **Multi-User Support** — Create and manage multiple authors
- **Image Library** — Upload, browse, and manage images stored in the database
- **Real-Time Reader Counts** — See how many people are reading each post
- **Dashboard** — Overview of posts, quick access to all management areas

### Technical Features
- **Six Color Themes** — Light, Dark, Sepia, Nord, Solarized Light, Dracula
- **OpenTelemetry Integration** — Distributed tracing, metrics, and logging
- **Automatic Log Cleanup** — Configurable retention with daily cleanup
- **XDG-Compliant Paths** — Proper data storage locations per platform
- **CI/CD Pipeline** — GitHub Actions with cross-platform testing

---

## Architecture

MyBlog follows **Clean Architecture** (also known as Onion Architecture), ensuring clear separation of concerns and testability.

```
┌─────────────────────────────────────────────────────────────────┐
│                        MyBlog.Web                                │
│                   (Presentation Layer)                           │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌───────────┐  │
│  │   Pages/    │ │   Shared/   │ │ Middleware/ │ │   Hubs/   │  │
│  │  Components │ │  Components │ │Rate Limiting│ │  SignalR  │  │
│  └─────────────┘ └─────────────┘ └─────────────┘ └───────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    MyBlog.Infrastructure                         │
│                     (Data Access Layer)                          │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌───────────┐  │
│  │Repositories │ │  Services   │ │  Telemetry  │ │   Data/   │  │
│  │   (EF Core) │ │Auth,Password│ │  Exporters  │ │ DbContext │  │
│  └─────────────┘ └─────────────┘ └─────────────┘ └───────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                       MyBlog.Core                                │
│                      (Domain Layer)                              │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌───────────┐  │
│  │   Models    │ │ Interfaces  │ │  Services   │ │ Constants │  │
│  │Post,User,..│ │IPostRepo,.. │ │Markdown,Slug│ │ AppConsts │  │
│  └─────────────┘ └─────────────┘ └─────────────┘ └───────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Project Structure

```
MyBlog/
├── src/
│   ├── MyBlog.Core/                 # Domain Layer (no external dependencies)
│   │   ├── Constants/
│   │   │   └── AppConstants.cs      # Auth cookie name, roles, limits
│   │   ├── Interfaces/
│   │   │   ├── IAuthService.cs
│   │   │   ├── IImageDimensionService.cs
│   │   │   ├── IImageRepository.cs
│   │   │   ├── IMarkdownService.cs
│   │   │   ├── IPasswordService.cs
│   │   │   ├── IPostRepository.cs
│   │   │   ├── IReaderTrackingService.cs
│   │   │   ├── ISlugService.cs
│   │   │   ├── ITelemetryLogRepository.cs
│   │   │   └── IUserRepository.cs
│   │   ├── Models/
│   │   │   ├── Image.cs
│   │   │   ├── ImageDimensionCache.cs
│   │   │   ├── Post.cs
│   │   │   ├── PostDto.cs
│   │   │   ├── TelemetryLog.cs
│   │   │   └── User.cs
│   │   └── Services/
│   │       ├── MarkdownService.cs
│   │       └── SlugService.cs
│   │
│   ├── MyBlog.Infrastructure/       # Data Access Layer
│   │   ├── Data/
│   │   │   ├── BlogDbContext.cs
│   │   │   ├── DatabasePathResolver.cs
│   │   │   └── DatabaseSchemaUpdater.cs
│   │   ├── Repositories/
│   │   │   ├── ImageRepository.cs
│   │   │   ├── PostRepository.cs
│   │   │   ├── TelemetryLogRepository.cs
│   │   │   └── UserRepository.cs
│   │   ├── Services/
│   │   │   ├── AuthService.cs
│   │   │   ├── ImageCacheWarmerService.cs
│   │   │   ├── ImageDimensionService.cs
│   │   │   ├── PasswordService.cs
│   │   │   ├── ReaderTrackingService.cs
│   │   │   └── TelemetryCleanupService.cs
│   │   ├── Telemetry/
│   │   │   ├── DatabaseLogExporter.cs
│   │   │   ├── FileLogExporter.cs
│   │   │   └── TelemetryPathResolver.cs
│   │   └── ServiceCollectionExtensions.cs
│   │
│   ├── MyBlog.Web/                  # Presentation Layer
│   │   ├── Components/
│   │   │   ├── Layout/
│   │   │   │   └── MainLayout.razor
│   │   │   ├── Pages/
│   │   │   │   ├── Admin/
│   │   │   │   │   ├── ChangePassword.razor
│   │   │   │   │   ├── Dashboard.razor
│   │   │   │   │   ├── ImageManager.razor
│   │   │   │   │   ├── PostEditor.razor
│   │   │   │   │   ├── PostList.razor
│   │   │   │   │   ├── UserEditor.razor
│   │   │   │   │   └── UserList.razor
│   │   │   │   ├── About.razor
│   │   │   │   ├── AccessDenied.razor
│   │   │   │   ├── Home.razor
│   │   │   │   ├── Login.razor
│   │   │   │   └── PostDetail.razor
│   │   │   └── Shared/
│   │   │       ├── Footer.razor
│   │   │       ├── MarkdownRenderer.razor
│   │   │       ├── Pagination.razor
│   │   │       ├── PostCard.razor
│   │   │       ├── ReaderBadge.razor
│   │   │       ├── RedirectToLogin.razor
│   │   │       └── ThemeSwitcher.razor
│   │   ├── Hubs/
│   │   │   └── ReaderHub.cs
│   │   ├── Middleware/
│   │   │   └── LoginRateLimitMiddleware.cs
│   │   └── wwwroot/
│   │       ├── css/site.css
│   │       └── js/site.js
│   │
│   ├── MyBlog.Tests/                # Test Project
│   │   ├── Integration/
│   │   │   ├── AuthServiceLongPasswordTests.cs
│   │   │   ├── AuthServiceTests.cs
│   │   │   ├── PasswordChangeTests.cs
│   │   │   ├── PostRepositoryTests.cs
│   │   │   └── TelemetryCleanupTests.cs
│   │   └── Unit/
│   │       ├── LoginRateLimitMiddlewareTests.cs
│   │       ├── MarkdownServiceTests.cs
│   │       ├── PasswordServiceTests.cs
│   │       └── SlugServiceTests.cs
│   │
│   ├── Directory.Build.props        # Shared build properties
│   ├── Directory.Packages.props     # Centralized package versions
│   └── MyBlog.slnx                  # Solution file
│
├── .github/
│   └── workflows/
│       └── build-deploy.yml         # CI/CD pipeline
│
└── README.md
```

---

## Technology Stack

### Runtime & Frameworks

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET | 10.0 |
| Web Framework | ASP.NET Core | 10.0 |
| UI Framework | Blazor Server | 10.0 |
| ORM | Entity Framework Core | 10.0.2 |
| Real-Time | SignalR | 10.0.2 |

### Database

| Component | Technology |
|-----------|------------|
| Database Engine | SQLite |
| Storage | Single file, XDG-compliant paths |
| Images | Binary BLOBs in database |

### Observability

| Component | Technology | Version |
|-----------|------------|---------|
| Telemetry | OpenTelemetry | 1.15.0 |
| Tracing | OpenTelemetry.Instrumentation.AspNetCore | 1.15.0 |
| Logging | File (JSON) + Database + Console | — |

### Testing

| Component | Technology | Version |
|-----------|------------|---------|
| Framework | xUnit | v3.2.2 |
| Test SDK | Microsoft.NET.Test.Sdk | 18.0.1 |
| Database | In-Memory SQLite | — |

### Security

| Component | Implementation |
|-----------|----------------|
| Password Hashing | ASP.NET Identity PasswordHasher (PBKDF2) |
| Authentication | Cookie-based with sliding expiration |
| Rate Limiting | Custom middleware with progressive delays |

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

### Clone and Run

```bash
# Clone the repository
git clone https://github.com/kusl/dotnetcms.git
cd dotnetcms/src

# Restore dependencies
dotnet restore MyBlog.slnx

# Run the application
cd MyBlog.Web
dotnet run
```

The application starts at `https://localhost:51226` (or the next available port).

### Default Credentials

| Field | Value |
|-------|-------|
| Username | `admin` |
| Password | `ChangeMe123!` |

> **⚠️ Important:** The default password is only used when creating the initial admin user. Once the user exists, you must change the password through the website at `/admin/change-password`.

### Running Tests

```bash
cd src
dotnet test MyBlog.slnx
```

Or run the test project directly:

```bash
dotnet run --project src/MyBlog.Tests/MyBlog.Tests.csproj
```

---

## Configuration

### Configuration Files

MyBlog uses the standard ASP.NET Core configuration system with the following files:

#### `appsettings.json` (Production)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=myblog.db"
  },
  "Authentication": {
    "SessionTimeoutMinutes": 30,
    "DefaultAdminPassword": "ChangeMe123!"
  },
  "Telemetry": {
    "RetentionDays": 30,
    "EnableFileLogging": true,
    "EnableDatabaseLogging": true
  },
  "Application": {
    "Title": "MyBlog",
    "PostsPerPage": 10,
    "RequireHttps": false,
    "GitForgeUrl": "https://github.com/kusl/dotnetcms"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

#### `appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

### Configuration Reference

#### Application Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Application:Title` | string | `"MyBlog"` | Site title displayed in header, browser tabs, and metadata |
| `Application:PostsPerPage` | int | `10` | Number of posts per page on the homepage |
| `Application:RequireHttps` | bool | `false` | Force HTTPS for authentication cookies |
| `Application:GitForgeUrl` | string | — | Link to source code repository displayed in footer |

#### Authentication Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Authentication:SessionTimeoutMinutes` | int | `30` | Session expiration time in minutes |
| `Authentication:DefaultAdminPassword` | string | `"ChangeMe123!"` | Initial admin password (first run only) |

#### Telemetry Settings

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Telemetry:RetentionDays` | int | `30` | Days to retain telemetry logs before cleanup |
| `Telemetry:EnableFileLogging` | bool | `true` | Write logs to JSON files |
| `Telemetry:EnableDatabaseLogging` | bool | `true` | Store logs in SQLite database |

### Environment Variables

Environment variables override configuration file values:

| Variable | Description | Overrides |
|----------|-------------|-----------|
| `MYBLOG_ADMIN_PASSWORD` | Initial admin password | `Authentication:DefaultAdminPassword` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment (`Development`, `Production`) | — |
| `XDG_DATA_HOME` | Linux data directory override | Database path |

**Priority order:** Environment Variables > `appsettings.{Environment}.json` > `appsettings.json`

---

## Database

### Schema

MyBlog uses SQLite with Entity Framework Core. The schema is created automatically on first run.

#### Users Table

```sql
CREATE TABLE "Users" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,
    "Username" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "Email" TEXT NOT NULL,
    "DisplayName" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL
);
CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");
```

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | GUID | Primary Key | Unique identifier |
| Username | VARCHAR(50) | Unique, Required | Login username (case-insensitive lookup) |
| PasswordHash | VARCHAR(256) | Required | PBKDF2 hash from ASP.NET Identity |
| Email | VARCHAR(256) | Required | User's email address |
| DisplayName | VARCHAR(100) | Required | Name shown on posts |
| CreatedAtUtc | DateTime | Required | Account creation timestamp |

#### Posts Table

```sql
CREATE TABLE "Posts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Posts" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Slug" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "Summary" TEXT NOT NULL,
    "AuthorId" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "UpdatedAtUtc" TEXT NOT NULL,
    "PublishedAtUtc" TEXT,
    "IsPublished" INTEGER NOT NULL,
    CONSTRAINT "FK_Posts_Users_AuthorId" FOREIGN KEY ("AuthorId") 
        REFERENCES "Users" ("Id") ON DELETE RESTRICT
);
CREATE UNIQUE INDEX "IX_Posts_Slug" ON "Posts" ("Slug");
```

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | GUID | Primary Key | Unique identifier |
| Title | VARCHAR(200) | Required | Post title |
| Slug | VARCHAR(200) | Unique, Required | URL-friendly identifier |
| Content | TEXT | Required | Markdown content |
| Summary | VARCHAR(500) | Required | Brief description for listings |
| AuthorId | GUID | Foreign Key | Reference to Users table |
| CreatedAtUtc | DateTime | Required | Creation timestamp |
| UpdatedAtUtc | DateTime | Required | Last modification timestamp |
| PublishedAtUtc | DateTime | Nullable | Publication timestamp |
| IsPublished | Boolean | Required | Visibility flag |

#### Images Table

```sql
CREATE TABLE "Images" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Images" PRIMARY KEY,
    "FileName" TEXT NOT NULL,
    "ContentType" TEXT NOT NULL,
    "Data" BLOB NOT NULL,
    "PostId" TEXT,
    "UploadedAtUtc" TEXT NOT NULL,
    "UploadedByUserId" TEXT NOT NULL,
    CONSTRAINT "FK_Images_Posts_PostId" FOREIGN KEY ("PostId") 
        REFERENCES "Posts" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Images_Users_UploadedByUserId" FOREIGN KEY ("UploadedByUserId") 
        REFERENCES "Users" ("Id") ON DELETE RESTRICT
);
```

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | GUID | Primary Key | Unique identifier |
| FileName | VARCHAR(256) | Required | Original file name |
| ContentType | VARCHAR(100) | Required | MIME type (image/jpeg, etc.) |
| Data | BLOB | Required | Binary image data |
| PostId | GUID | Nullable, FK | Optional association to a post |
| UploadedAtUtc | DateTime | Required | Upload timestamp |
| UploadedByUserId | GUID | Foreign Key | Reference to uploader |

#### ImageDimensionCache Table

```sql
CREATE TABLE "ImageDimensionCache" (
    "Url" TEXT NOT NULL CONSTRAINT "PK_ImageDimensionCache" PRIMARY KEY,
    "Width" INTEGER NOT NULL,
    "Height" INTEGER NOT NULL,
    "LastCheckedUtc" TEXT NOT NULL
);
```

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Url | VARCHAR(2048) | Primary Key | Image URL (external images) |
| Width | Integer | Required | Cached width in pixels |
| Height | Integer | Required | Cached height in pixels |
| LastCheckedUtc | DateTime | Required | When dimensions were fetched |

#### TelemetryLogs Table

```sql
CREATE TABLE "TelemetryLogs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TelemetryLogs" PRIMARY KEY AUTOINCREMENT,
    "TimestampUtc" TEXT NOT NULL,
    "Level" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "Exception" TEXT,
    "TraceId" TEXT,
    "SpanId" TEXT,
    "Properties" TEXT
);
CREATE INDEX "IX_TelemetryLogs_TimestampUtc" ON "TelemetryLogs" ("TimestampUtc");
```

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | Integer | Auto-increment PK | Unique identifier |
| TimestampUtc | DateTime | Required, Indexed | Log timestamp |
| Level | VARCHAR(20) | Required | Log level (Information, Warning, Error) |
| Category | VARCHAR(256) | Required | Logger category/source |
| Message | TEXT | Required | Log message |
| Exception | TEXT | Nullable | Exception details if any |
| TraceId | TEXT | Nullable | Distributed trace ID |
| SpanId | TEXT | Nullable | Span ID within trace |
| Properties | TEXT | Nullable | Additional properties as JSON |

### Database Locations (XDG-Compliant)

The database file location follows platform conventions:

| Platform | Path |
|----------|------|
| Linux | `~/.local/share/MyBlog/myblog.db` |
| macOS | `~/Library/Application Support/MyBlog/myblog.db` |
| Windows | `%LOCALAPPDATA%\MyBlog\myblog.db` |
| Fallback | `{AppDirectory}/data/myblog.db` |

The system automatically:
1. Checks for XDG_DATA_HOME environment variable (Linux)
2. Falls back to platform-specific standard directories
3. Falls back to local `data/` directory if the preferred location is not writable

### Database Initialization

On application startup:

1. **EnsureCreated** — Creates the database and all tables if they don't exist
2. **DatabaseSchemaUpdater.ApplyUpdatesAsync** — Adds new tables to existing databases (for upgrades)
3. **AuthService.EnsureAdminUserAsync** — Creates default admin user if no users exist

### Schema Updates

Since the project doesn't use formal EF Core migrations, the `DatabaseSchemaUpdater` class handles incremental schema updates:

```csharp
public static async Task ApplyUpdatesAsync(BlogDbContext db)
{
    // Check and create ImageDimensionCache table if it doesn't exist
    await EnsureImageDimensionCacheTableAsync(db);
}
```

This approach is idempotent—safe to run multiple times without side effects.

---

## Authentication & Security

### Authentication Flow

```
┌─────────┐     ┌─────────────────┐     ┌─────────────┐
│  User   │────▶│ Rate Limiting   │────▶│   Login     │
│         │     │   Middleware    │     │    Page     │
└─────────┘     └─────────────────┘     └─────────────┘
                       │                       │
                       │ Delay if needed       │ POST /login
                       ▼                       ▼
              ┌─────────────────┐     ┌─────────────┐
              │  Track Attempt  │     │ AuthService │
              │  (per IP)       │     │ .Authenticate│
              └─────────────────┘     └─────────────┘
                                              │
                                              ▼
                                      ┌─────────────┐
                                      │ Password    │
                                      │ Service     │
                                      │ .Verify     │
                                      └─────────────┘
                                              │
                              Success ◄───────┴───────► Failure
                                 │                         │
                                 ▼                         ▼
                         ┌─────────────┐          ┌─────────────┐
                         │ Create      │          │ Show Error  │
                         │ Claims &    │          │ Message     │
                         │ Sign In     │          │             │
                         └─────────────┘          └─────────────┘
```

### Password Security

Passwords are hashed using ASP.NET Identity's `PasswordHasher<T>`:

**Algorithm:** PBKDF2 with HMAC-SHA256
- 128-bit salt
- 256-bit subkey
- 10,000+ iterations (version-dependent)

```csharp
public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(string password)
    {
        return _hasher.HashPassword(null!, password);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(null!, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
```

**Key behaviors:**
- Same password produces different hashes each time (random salt)
- Automatic rehashing detection for algorithm upgrades
- Supports passwords up to 512+ characters

### Rate Limiting

The `LoginRateLimitMiddleware` protects against brute-force attacks using **progressive delays**:

| Attempt # | Delay |
|-----------|-------|
| 1-5 | None |
| 6 | 1 second |
| 7 | 2 seconds |
| 8 | 4 seconds |
| 9 | 8 seconds |
| 10 | 16 seconds |
| 11+ | 30 seconds (max) |

**Key design decisions:**
- **Never blocks users** — Only delays, ensuring legitimate users can always try again
- **Per-IP tracking** — Each IP address has independent attempt counters
- **15-minute window** — Counters reset after 15 minutes of inactivity
- **Testable** — Delay function is injectable for unit testing

```csharp
// Delay calculation formula
var delayMultiplier = record.Count - AttemptsBeforeDelay;  // attempts - 5
var delaySeconds = Math.Min(Math.Pow(2, delayMultiplier), MaxDelaySeconds);  // 2^n, max 30
```

### Session Management

Sessions use cookie-based authentication:

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "MyBlog.Auth";
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);  // Configurable
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;  // XSS protection
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;  // Or Always for HTTPS
    });
```

### Claims Structure

When a user logs in, the following claims are created:

| Claim Type | Value |
|------------|-------|
| `ClaimTypes.NameIdentifier` | User's GUID |
| `ClaimTypes.Name` | Username |
| `DisplayName` | User's display name |
| `ClaimTypes.Role` | "Admin" |

---

## Content Management

### Post Lifecycle

```
┌─────────────────────────────────────────────────────────────────┐
│                        Post Creation                             │
└─────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌─────────────┐  Generate  ┌─────────────┐  Check    ┌─────────────┐
│ User enters │──────────▶│ SlugService │─────────▶│ IsSlugTaken │
│ Title       │   Slug     │ .Generate   │  Unique?  │ Repository  │
└─────────────┘            └─────────────┘           └─────────────┘
                                                           │
                           ┌───────────────────────────────┤
                           │                               │
                      Unique                          Not Unique
                           │                               │
                           ▼                               ▼
                    ┌─────────────┐              ┌─────────────┐
                    │ Use slug    │              │ Append -1,  │
                    │ as-is       │              │ -2, etc.    │
                    └─────────────┘              └─────────────┘
                           │                               │
                           └───────────────┬───────────────┘
                                           ▼
                                   ┌─────────────┐
                                   │ Save Post   │
                                   │ to Database │
                                   └─────────────┘
```

### Slug Generation Algorithm

The `SlugService` converts titles to URL-friendly slugs:

```csharp
public string GenerateSlugOrUuid(string title)
{
    var slug = GenerateSlug(title);
    return !string.IsNullOrWhiteSpace(slug) 
        ? slug 
        : $"post-{Guid.CreateVersion7().ToString()}";
}

private string GenerateSlug(string title)
{
    // 1. Normalize Unicode (decompose accented characters)
    var normalized = title.Normalize(NormalizationForm.FormD);
    
    // 2. Remove diacritical marks
    // 3. Convert to lowercase
    // 4. Replace spaces/underscores with hyphens
    // 5. Remove non-alphanumeric characters (except hyphens)
    // 6. Collapse multiple hyphens
    // 7. Trim hyphens from ends
}
```

**Examples:**

| Input | Output |
|-------|--------|
| `"Hello World"` | `hello-world` |
| `"Hello, World! How's it going?"` | `hello-world-hows-it-going` |
| `"Café résumé"` | `cafe-resume` |
| `"Top 10 Tips for 2024"` | `top-10-tips-for-2024` |
| `"hello_world_test"` | `hello-world-test` |
| `""` (empty) | `post-{guid}` |
| `"   "` (whitespace only) | `post-{guid}` |

### Post Data Transfer Objects

The system uses DTOs to separate concerns:

```csharp
// For list views (lightweight)
public sealed record PostListItemDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string AuthorDisplayName,
    DateTime? PublishedAtUtc,
    bool IsPublished);

// For detail views (full content)
public sealed record PostDetailDto(
    Guid Id,
    string Title,
    string Slug,
    string Content,
    string Summary,
    string AuthorDisplayName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? PublishedAtUtc,
    bool IsPublished);

// For creating posts
public sealed record CreatePostDto(
    string Title,
    string Content,
    string Summary,
    bool IsPublished);

// For updating posts
public sealed record UpdatePostDto(
    string Title,
    string Content,
    string Summary,
    bool IsPublished);
```

---

## Markdown Specification

MyBlog uses a **custom-built Markdown parser** with no external dependencies. This section documents the exact specification for compatibility.

### Supported Syntax

#### Headings (ATX Style)

```markdown
# Heading 1
## Heading 2
### Heading 3
#### Heading 4
##### Heading 5
###### Heading 6
```

**Output:**
```html
<h1>Heading 1</h1>
<h2>Heading 2</h2>
<!-- etc. -->
```

#### Bold Text

```markdown
**bold text**
__also bold__
```

**Output:**
```html
<strong>bold text</strong>
<strong>also bold</strong>
```

#### Italic Text

```markdown
*italic text*
_also italic_
```

**Output:**
```html
<em>italic text</em>
<em>also italic</em>
```

#### Links

```markdown
[Link Text](https://example.com)
```

**Output:**
```html
<a href="https://example.com">Link Text</a>
```

#### Images

```markdown
![Alt Text](https://example.com/image.png)
```

**Output (with dimension lookup):**
```html
<img src="https://example.com/image.png" alt="Alt Text" width="800" height="600" />
```

**Output (without dimensions):**
```html
<img src="https://example.com/image.png" alt="Alt Text" />
```

#### Inline Code

```markdown
Use `code` here
```

**Output:**
```html
Use <code>code</code> here
```

#### Fenced Code Blocks

````markdown
```
var x = 1;
console.log(x);
```
````

**Output:**
```html
<pre><code>var x = 1;
console.log(x);</code></pre>
```

> Note: Language specification after the backticks is accepted but not used for syntax highlighting.

#### Blockquotes

```markdown
> This is a quote
```

**Output:**
```html
<blockquote><p>This is a quote</p></blockquote>
```

#### Unordered Lists

```markdown
- Item 1
- Item 2
- Item 3
```

or

```markdown
* Item 1
* Item 2
```

**Output:**
```html
<ul>
<li>Item 1</li>
<li>Item 2</li>
<li>Item 3</li>
</ul>
```

#### Ordered Lists

```markdown
1. First
2. Second
3. Third
```

**Output:**
```html
<ol>
<li>First</li>
<li>Second</li>
<li>Third</li>
</ol>
```

#### Horizontal Rules

```markdown
---
```

or `***` or `___` (3+ characters)

**Output:**
```html
<hr />
```

#### Paragraphs

Regular text separated by blank lines becomes paragraphs:

```markdown
This is paragraph one.

This is paragraph two.
```

**Output:**
```html
<p>This is paragraph one.</p>
<p>This is paragraph two.</p>
```

### Image Dimension Caching

The Markdown service automatically fetches and caches dimensions for external images:

1. **On render:** Check `ImageDimensionCache` table for URL
2. **If cached:** Use stored width/height
3. **If not cached:** Fetch image headers, parse dimensions, store in cache
4. **If fetch fails:** Render image without dimensions (graceful degradation)

**Supported image formats for dimension detection:**
- PNG (dimensions at bytes 16-23)
- GIF (dimensions at bytes 6-9)
- JPEG (requires scanning for SOF marker)
- WebP (VP8, VP8L, VP8X variants)

### HTML Escaping

All user content is HTML-escaped before processing:

```csharp
text = HttpUtility.HtmlEncode(text);
```

This prevents XSS attacks while allowing Markdown syntax.

---

## Image Management

### Upload Specifications

| Specification | Value |
|---------------|-------|
| Maximum Size | 5 MB (5,242,880 bytes) |
| Allowed Types | `image/jpeg`, `image/png`, `image/gif`, `image/webp` |
| Storage | Binary BLOB in SQLite database |

### Upload Process

```
┌─────────────┐     ┌─────────────────┐     ┌─────────────┐
│ InputFile   │────▶│ Validate Size   │────▶│ Validate    │
│ Component   │     │ (< 5MB)         │     │ Content Type│
└─────────────┘     └─────────────────┘     └─────────────┘
                                                   │
                                                   ▼
                    ┌─────────────────┐     ┌─────────────┐
                    │ Save to         │◀────│ Read to     │
                    │ Database        │     │ MemoryStream│
                    └─────────────────┘     └─────────────┘
```

### Using Images in Posts

After uploading, reference images in Markdown using:

```markdown
![Description](/api/images/{guid})
```

Example:
```markdown
![My Photo](/api/images/550e8400-e29b-41d4-a716-446655440000)
```

### Image API Endpoint

**GET** `/api/images/{id}`

Returns the image binary data with appropriate `Content-Type` header.

**Response Headers:**
```
Content-Type: image/jpeg
Content-Length: 12345
```

---

## Real-Time Features

### Reader Tracking

MyBlog tracks how many users are currently reading each post using SignalR.

#### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Browser                                   │
│  ┌─────────────┐                                                │
│  │ ReaderBadge │◀──────SignalR Connection──────────────────────┐│
│  │ Component   │                                                ││
│  └─────────────┘                                                ││
└─────────────────────────────────────────────────────────────────┘│
                                                                   │
┌─────────────────────────────────────────────────────────────────┐│
│                        Server                                    ││
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐       ││
│  │ ReaderHub   │────▶│ Tracking    │────▶│ Concurrent  │       ││
│  │ (SignalR)   │     │ Service     │     │ Dictionary  │       ││
│  └─────────────┘     └─────────────┘     └─────────────┘       ││
│         │                                       │                ││
│         └──────────Broadcast Count──────────────┘◀───────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

#### SignalR Hub Methods

```csharp
public class ReaderHub : Hub
{
    // Client calls when viewing a post
    public async Task JoinPage(string slug);
    
    // Client calls when leaving a post
    public async Task LeavePage(string slug);
    
    // Automatically called on disconnect
    public override async Task OnDisconnectedAsync(Exception? exception);
}
```

#### Client Events

| Event | Direction | Payload | Description |
|-------|-----------|---------|-------------|
| `JoinPage` | Client → Server | `string slug` | Register viewer for post |
| `LeavePage` | Client → Server | `string slug` | Unregister viewer |
| `UpdateCount` | Server → Client | `int count` | Broadcast new count |

#### Tracking Service Implementation

```csharp
public class ReaderTrackingService : IReaderTrackingService
{
    // Maps Slug → Count of active readers
    private readonly ConcurrentDictionary<string, int> _slugCounts = new();
    
    // Maps ConnectionId → Slug (for disconnect handling)
    private readonly ConcurrentDictionary<string, string> _connectionMap = new();
}
```

**Thread-safety:** All operations use `ConcurrentDictionary` with atomic operations.

---

## Theming System

### Available Themes

| Theme | Type | Description |
|-------|------|-------------|
| `light` | Light | Clean, professional default |
| `dark` | Dark | Easy on the eyes for night reading |
| `sepia` | Light | Warm, paper-like reading experience |
| `nord` | Dark | Inspired by Arctic landscapes |
| `solarized-light` | Light | Ethan Schoonover's classic palette |
| `dracula` | Dark | Popular dark theme with vibrant accents |

### CSS Variables

Each theme defines the following CSS custom properties:

```css
:root, [data-theme="light"] {
    --color-bg: #ffffff;
    --color-bg-alt: #f8f9fa;
    --color-bg-elevated: #ffffff;
    --color-text: #1a1a2e;
    --color-text-muted: #5a5a6e;
    --color-primary: #2563eb;
    --color-primary-hover: #1d4ed8;
    --color-primary-muted: #3b82f6;
    --color-border: #d1d5db;
    --color-border-light: #e5e7eb;
    --color-danger: #dc2626;
    --color-danger-hover: #b91c1c;
    --color-success: #059669;
    --color-success-hover: #047857;
    --color-warning: #d97706;
    --color-info: #0891b2;
    --color-code-bg: #f1f5f9;
    --color-blockquote-border: #3b82f6;
    --color-selection-bg: #bfdbfe;
    --color-selection-text: #1e3a5f;
    --color-focus-ring: #3b82f6;
    --color-shadow: rgba(0, 0, 0, 0.1);
    --color-overlay: rgba(0, 0, 0, 0.5);
    color-scheme: light;
}
```

### Theme Persistence

Themes are stored in `localStorage`:

```javascript
localStorage.setItem('myblog-theme', 'dark');
```

### System Preference Detection

The system automatically detects and respects the user's OS preference:

```javascript
window.matchMedia('(prefers-color-scheme: dark)').matches
```

If no theme is saved, the system preference is used.

### Flash Prevention

A blocking inline script runs before page render to prevent theme flash:

```javascript
(function() {
    var theme = localStorage.getItem('myblog-theme');
    if (!theme) {
        theme = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    document.documentElement.setAttribute('data-theme', theme);
})();
```

---

## Observability & Telemetry

### OpenTelemetry Integration

MyBlog uses OpenTelemetry for distributed tracing, metrics, and logging.

#### Configuration

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "MyBlog.Web", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("MyBlog.Web")
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());
```

### Log Exporters

#### File Exporter

Writes JSON-formatted logs to rotating files:

```
~/.local/share/MyBlog/telemetry/logs/logs_20260126_063830.json
```

**Format:**
```json
[
{
  "Timestamp": "2026-01-26T06:38:30.1234567Z",
  "Level": "Information",
  "Category": "MyBlog.Web.Pages.Home",
  "Message": "Page loaded",
  "TraceId": "abc123...",
  "SpanId": "def456...",
  "Exception": null
},
...
]
```

**Rotation:** Files rotate at 25 MB with sequential numbering.

#### Database Exporter

Writes structured logs to the `TelemetryLogs` table for queryable storage.

### Automatic Cleanup

The `TelemetryCleanupService` runs daily to remove old logs:

```csharp
public sealed class TelemetryCleanupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run cleanup immediately on startup
        await CleanupAsync(stoppingToken);

        // Then run daily
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupAsync(stoppingToken);
        }
    }
}
```

**Retention:** Controlled by `Telemetry:RetentionDays` (default: 30 days).

---

## API Reference

### Public Endpoints

#### GET `/`
Homepage with paginated published posts.

**Query Parameters:**
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | int | 1 | Page number |

#### GET `/post/{slug}`
View a single published post.

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `slug` | string | Post's URL slug |

**Response:** HTML page with SEO metadata, Open Graph tags, and JSON-LD structured data.

#### GET `/about`
About page with application information.

#### GET `/login`
Login page.

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `returnUrl` | string | URL to redirect after login |

#### POST `/login`
Authenticate user (handled by Blazor form).

**Form Data:**
| Field | Type | Required |
|-------|------|----------|
| `username` | string | Yes |
| `password` | string | Yes |

#### POST `/logout`
Sign out current user. Requires authentication.

#### GET `/api/images/{id}`
Retrieve an uploaded image.

**Path Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | GUID | Image identifier |

**Response:**
- `200 OK` with image binary and `Content-Type` header
- `404 Not Found` if image doesn't exist

### Admin Endpoints (Require Authentication)

| Path | Description |
|------|-------------|
| `/admin` | Dashboard |
| `/admin/posts` | Post list |
| `/admin/posts/new` | Create post |
| `/admin/posts/edit/{id}` | Edit post |
| `/admin/users` | User list |
| `/admin/users/new` | Create user |
| `/admin/users/edit/{id}` | Edit user |
| `/admin/images` | Image manager |
| `/admin/change-password` | Change own password |

### SignalR Hub

**Endpoint:** `/readerHub`

| Method | Direction | Parameters | Description |
|--------|-----------|------------|-------------|
| `JoinPage` | Client → Server | `string slug` | Start tracking for a post |
| `LeavePage` | Client → Server | `string slug` | Stop tracking for a post |
| `UpdateCount` | Server → Client | `int count` | Receive updated reader count |

---

## Admin Guide

### Dashboard (`/admin`)

The dashboard provides:
- Total post count
- 5 most recently updated posts
- Quick links to all management areas

### Managing Posts

#### Creating a Post (`/admin/posts/new`)

1. Enter a **Title** (required)
2. Enter a **Summary** (shown in listings)
3. Write **Content** in Markdown
4. Check **Published** to make visible
5. Click **Save**

The slug is automatically generated from the title. If a collision occurs, `-1`, `-2`, etc. is appended.

#### Editing a Post (`/admin/posts/edit/{id}`)

Same as creating, with the current content pre-filled. The slug is regenerated if the title changes.

#### Live Preview

The editor shows a live preview of the rendered Markdown on the right side.

### Managing Users

#### Creating a User (`/admin/users/new`)

| Field | Requirements |
|-------|--------------|
| Username | Unique, required |
| Display Name | Required, shown on posts |
| Email | Required |
| Password | Minimum 8 characters |

#### Editing a User (`/admin/users/edit/{id}`)

All fields except password can be updated. Leave password blank to keep current password.

#### Deleting a User

Users can be deleted except for the currently logged-in user.

### Managing Images (`/admin/images`)

#### Uploading

1. Click the file input or drag-and-drop
2. Select an image (JPEG, PNG, GIF, or WebP)
3. Image uploads automatically

#### Using in Posts

Copy the URL shown below each image thumbnail and paste into your Markdown:

```markdown
![Description](/api/images/550e8400-e29b-41d4-a716-446655440000)
```

#### Deleting

Click **Delete** below any image. This action is immediate and permanent.

### Changing Your Password (`/admin/change-password`)

1. Enter **Current Password**
2. Enter **New Password** (minimum 8 characters)
3. **Confirm New Password**
4. Click **Change Password**

The `MYBLOG_ADMIN_PASSWORD` environment variable does NOT override existing passwords.

---

## Testing

### Test Project Structure

```
MyBlog.Tests/
├── Integration/
│   ├── AuthServiceLongPasswordTests.cs
│   ├── AuthServiceTests.cs
│   ├── PasswordChangeTests.cs
│   ├── PostRepositoryTests.cs
│   └── TelemetryCleanupTests.cs
└── Unit/
    ├── LoginRateLimitMiddlewareTests.cs
    ├── MarkdownServiceTests.cs
    ├── PasswordServiceTests.cs
    └── SlugServiceTests.cs
```

### Test Categories

#### Unit Tests

Test individual components in isolation with mock dependencies.

**PasswordServiceTests (5 tests)**

| Test | Purpose |
|------|---------|
| `HashPassword_ReturnsNonEmptyHash` | Verify hashing produces output |
| `HashPassword_ReturnsDifferentHashForSamePassword` | Verify salt randomization |
| `VerifyPassword_WithCorrectPassword_ReturnsTrue` | Verify correct password matches |
| `VerifyPassword_WithWrongPassword_ReturnsFalse` | Verify incorrect password fails |
| `VerifyPassword_WithEmptyPassword_ReturnsFalse` | Verify empty password fails |

**SlugServiceTests (7 tests)**

| Test | Purpose |
|------|---------|
| `GenerateSlug_WithSimpleTitle_ReturnsLowercaseWithHyphens` | Basic transformation |
| `GenerateSlug_WithSpecialCharacters_RemovesThem` | Punctuation removal |
| `GenerateSlug_WithMultipleSpaces_CollapsesToSingleHyphen` | Space normalization |
| `GenerateSlug_WithUnicode_RemovesDiacritics` | Unicode handling |
| `GenerateSlug_WithLeadingTrailingSpaces_TrimsHyphens` | Edge trimming |
| `GenerateSlug_WithNumbers_PreservesNumbers` | Number preservation |
| `GenerateSlug_WithEmptyStringOrWhitespace_ReturnsGuidWithPrefix` | Fallback behavior |

**MarkdownServiceTests (18 tests)**

| Test | Purpose |
|------|---------|
| `ToHtml_WithHeading1_ReturnsH1Tag` | H1 parsing |
| `ToHtml_WithHeading2_ReturnsH2Tag` | H2 parsing |
| `ToHtml_WithHeading6_ReturnsH6Tag` | H6 parsing |
| `ToHtml_WithBoldText_ReturnsStrongTag` | Bold parsing |
| `ToHtml_WithItalicText_ReturnsEmTag` | Italic parsing |
| `ToHtml_WithLink_ReturnsAnchorTag` | Link parsing |
| `ToHtml_WithImage_InjectsDimensions_IfResolvable` | Image with dimensions |
| `ToHtml_WithImage_NoDimensions_IfUnresolvable` | Image without dimensions |
| `ToHtml_WithImage_WhenServiceThrows_StillRendersImage` | Error handling |
| `ToHtml_WithInlineCode_ReturnsCodeTag` | Inline code |
| `ToHtml_WithCodeBlock_ReturnsPreCodeTags` | Code blocks |
| `ToHtml_WithBlockquote_ReturnsBlockquoteTag` | Blockquotes |
| `ToHtml_WithUnorderedList_ReturnsUlLiTags` | Unordered lists |
| `ToHtml_WithOrderedList_ReturnsOlLiTags` | Ordered lists |
| `ToHtml_WithHorizontalRule_ReturnsHrTag` | Horizontal rules |
| `ToHtml_WithEmptyString_ReturnsEmpty` | Empty input |
| `ToHtml_WithNull_ReturnsEmpty` | Null input |
| `ToHtml_WithMultipleImages_ProcessesAll` | Multiple images |

**LoginRateLimitMiddlewareTests (8 tests)**

| Test | Purpose |
|------|---------|
| `InvokeAsync_NonLoginRequest_PassesThroughImmediately` | Non-login requests unaffected |
| `InvokeAsync_GetLoginRequest_PassesThroughImmediately` | GET requests unaffected |
| `InvokeAsync_FirstFiveAttempts_NoDelay` | Grace period |
| `InvokeAsync_SixthAttempt_HasOneSecondDelay` | First delay |
| `InvokeAsync_ProgressiveDelays_IncreaseExponentially` | Exponential backoff |
| `InvokeAsync_DelayCappedAt30Seconds` | Maximum delay cap |
| `InvokeAsync_AfterManyAttempts_NeverBlocks` | Never blocks (100 attempts) |
| `InvokeAsync_DifferentIPs_IndependentTracking` | Per-IP isolation |

#### Integration Tests

Test components with real database (in-memory SQLite).

**AuthServiceTests (5 tests)**

| Test | Purpose |
|------|---------|
| `AuthenticateAsync_WithValidCredentials_ReturnsUser` | Successful login |
| `AuthenticateAsync_WithInvalidPassword_ReturnsNull` | Wrong password |
| `AuthenticateAsync_WithNonExistentUser_ReturnsNull` | Unknown user |
| `EnsureAdminUserAsync_WhenNoUsersExist_CreatesAdmin` | Initial setup |
| `EnsureAdminUserAsync_WhenUsersExist_DoesNotCreateAnother` | Idempotence |

**AuthServiceLongPasswordTests (8 tests)**

| Test | Purpose |
|------|---------|
| `AuthenticateAsync_With128CharacterPassword_Succeeds` | Long password support |
| `AuthenticateAsync_With256CharacterPassword_Succeeds` | Very long password |
| `AuthenticateAsync_With512CharacterPassword_Succeeds` | Extra long password |
| `ChangePasswordAsync_With128CharacterNewPassword_Succeeds` | Long password change |
| `AuthenticateAsync_WithComplexLongPassword_Succeeds` | Mixed character password |
| `AuthenticateAsync_After100FailedAttempts_StillAllowsLogin` | No lockout (100) |
| `AuthenticateAsync_After1000FailedAttempts_StillAllowsLogin` | No lockout (1000) |
| `AuthenticateAsync_InterleavedFailuresAndSuccesses_NeverLocks` | Interleaved attempts |

**PasswordChangeTests (7 tests)**

| Test | Purpose |
|------|---------|
| `ChangePasswordAsync_WithCorrectCurrentPassword_ReturnsTrue` | Successful change |
| `ChangePasswordAsync_WithCorrectPassword_AllowsLoginWithNewPassword` | New password works |
| `ChangePasswordAsync_WithWrongCurrentPassword_ReturnsFalse` | Wrong current password |
| `ChangePasswordAsync_WithWrongPassword_DoesNotChangePassword` | Failed change preserves old |
| `ChangePasswordAsync_WithNonExistentUser_ReturnsFalse` | Invalid user |
| `ResetPasswordAsync_SetsNewPassword` | Admin reset |
| `ResetPasswordAsync_WithNonExistentUser_ThrowsException` | Invalid user throws |

**PostRepositoryTests (8 tests)**

| Test | Purpose |
|------|---------|
| `CreateAsync_AddsPostToDatabase` | Create post |
| `GetByIdAsync_WithExistingId_ReturnsPost` | Retrieve by ID |
| `GetByIdAsync_WithNonExistingId_ReturnsNull` | Non-existent ID |
| `GetBySlugAsync_WithExistingSlug_ReturnsPost` | Retrieve by slug |
| `GetPublishedPostsAsync_ReturnsOnlyPublishedPosts` | Published filter |
| `UpdateAsync_ModifiesPost` | Update post |
| `DeleteAsync_RemovesPost` | Delete post |
| `GetPublishedPostsAsync_ReturnsCorrectCount` | Pagination count |

**TelemetryCleanupTests (3 tests)**

| Test | Purpose |
|------|---------|
| `DeleteOlderThanAsync_RemovesOldLogs` | Cleanup old logs |
| `DeleteOlderThanAsync_WithNoOldLogs_ReturnsZero` | No-op when nothing to clean |
| `DeleteOlderThanAsync_WithEmptyTable_ReturnsZero` | Empty table handling |

### Running Tests

```bash
# Run all tests
cd src
dotnet test MyBlog.slnx

# Run with detailed output
dotnet test MyBlog.slnx --verbosity normal

# Run specific test class
dotnet test MyBlog.slnx --filter "FullyQualifiedName~SlugServiceTests"

# Run specific test
dotnet test MyBlog.slnx --filter "FullyQualifiedName~GenerateSlug_WithUnicode"

# Generate TRX report
dotnet test MyBlog.slnx --logger trx --results-directory TestResults
```

### Test Database Strategy

Integration tests use **in-memory SQLite**:

```csharp
var options = new DbContextOptionsBuilder<BlogDbContext>()
    .UseSqlite("Data Source=:memory:")
    .Options;

_context = new BlogDbContext(options);
_context.Database.OpenConnection();  // Keep connection open
_context.Database.EnsureCreated();
```

**Benefits:**
- Fast execution (no disk I/O)
- Isolated per test class
- Cross-platform compatible
- No cleanup required

### Testing Rate Limiting

The rate limiting middleware uses an injectable delay function for testing:

```csharp
// Production: Real delays
public LoginRateLimitMiddleware(RequestDelegate next, ILogger logger)
    : this(next, logger, null) { }

// Testing: No-op delay function
public LoginRateLimitMiddleware(
    RequestDelegate next,
    ILogger logger,
    Func<TimeSpan, CancellationToken, Task>? delayFunc)
{
    _delayFunc = delayFunc;
}

// In InvokeAsync:
if (_delayFunc != null)
    await _delayFunc(delay, context.RequestAborted);  // Test path
else
    await Task.Delay(delay, context.RequestAborted);  // Production path
```

This allows tests to run instantly while still verifying delay calculations.

---

## Deployment

### GitHub Actions Pipeline

The project includes a complete CI/CD pipeline (`.github/workflows/build-deploy.yml`):

```yaml
name: Build, Test, and Deploy

on:
  push:
    branches: ['**']
  pull_request:
    branches: ['**']

jobs:
  build-test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore src/MyBlog.slnx

      - name: Build solution
        run: dotnet build src/MyBlog.slnx -c Release --no-restore

      - name: Run tests
        run: dotnet run --project src/MyBlog.Tests/MyBlog.Tests.csproj

  deploy:
    needs: build-test
    if: github.ref == 'refs/heads/main' || github.ref == 'refs/heads/master' || github.ref == 'refs/heads/develop'
    runs-on: windows-latest
    
    # ... deployment steps
```

### Required GitHub Secrets

| Secret | Description | Example |
|--------|-------------|---------|
| `WEBSITE_NAME` | IIS site name | `MyBlog` |
| `SERVER_COMPUTER_NAME` | Server hostname | `myserver.example.com` |
| `SERVER_USERNAME` | WebDeploy username | `deploy-user` |
| `SERVER_PASSWORD` | WebDeploy password | (secure) |

### WebDeploy Configuration

The deployment uses WebDeploy with important features:

```powershell
& $msdeployPath -verb:sync $sourceArg $destArg `
    -allowUntrusted `
    -enableRule:DoNotDeleteRule `    # Preserve existing files
    -enableRule:AppOffline `          # Graceful shutdown
    -retryAttempts:3 `
    -retryInterval:3000
```

**AppOffline Rule:** Creates `app_offline.htm` to:
1. Stop the application gracefully
2. Release file locks
3. Allow deployment
4. Remove the file to restart

### Manual Deployment

#### Windows (IIS)

```bash
# Build for Windows
dotnet publish src/MyBlog.Web/MyBlog.Web.csproj -c Release -o ./publish -r win-x64

# Copy to server
xcopy /s /y publish \\server\wwwroot\MyBlog

# Or use WebDeploy
msdeploy -verb:sync -source:contentPath=./publish -dest:contentPath=MyBlog,...
```

#### Linux

```bash
# Build for Linux
dotnet publish src/MyBlog.Web/MyBlog.Web.csproj -c Release -o ./publish -r linux-x64

# Copy to server
rsync -avz publish/ user@server:/var/www/myblog/

# Create systemd service
sudo nano /etc/systemd/system/myblog.service
```

Example systemd service:

```ini
[Unit]
Description=MyBlog Web Application
After=network.target

[Service]
WorkingDirectory=/var/www/myblog
ExecStart=/var/www/myblog/MyBlog.Web
Restart=always
RestartSec=10
KillSignal=SIGINT
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

### IIS Configuration

1. Install [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Create a new IIS site pointing to the publish folder
3. Set Application Pool to **"No Managed Code"**
4. Ensure the Application Pool identity has **write access** to:
   - `%LOCALAPPDATA%\MyBlog\` (database)
   - Telemetry directory (if file logging enabled)

---

## Troubleshooting

### ERROR_FILE_IN_USE During Deployment

**Cause:** Application DLLs are locked because the app is running.

**Solution:** Use the `-enableRule:AppOffline` flag in WebDeploy (included in the GitHub Actions workflow).

### Password Not Changing After Setting MYBLOG_ADMIN_PASSWORD

**Cause:** The environment variable only works when **no users exist** in the database.

**Solution:**
1. Stop the application
2. Delete the database file
3. Set `MYBLOG_ADMIN_PASSWORD`
4. Start the application

Or use `/admin/change-password` to change the password through the UI.

### Database Locked Errors

**Cause:** SQLite can have locking issues with concurrent access.

**Solutions:**
- Ensure only one instance of the application is running
- Check that no database tools have the file open
- Verify file permissions on the database directory

### Theme Not Persisting

**Cause:** localStorage might be blocked or cleared.

**Solutions:**
- Check browser privacy settings
- Ensure JavaScript is enabled
- Clear browser cache and try again

### Images Not Displaying

**Cause:** Image URL might be incorrect or image was deleted.

**Solutions:**
- Verify the GUID in the URL matches an existing image
- Check `/admin/images` to confirm the image exists
- Ensure the URL format is `/api/images/{guid}`

### Rate Limiting Delays

**Note:** Rate limiting never blocks, only delays.

**Expected delays after failed login attempts:**
- Attempts 1-5: No delay
- Attempt 6: 1 second
- Attempt 7: 2 seconds
- etc., up to 30 seconds maximum

**Solution:** Wait for the delay window (15 minutes) to reset, or use the correct password.

### SignalR Connection Issues

**Symptoms:** Reader count not updating.

**Solutions:**
- Check browser console for WebSocket errors
- Verify `/readerHub` endpoint is accessible
- Check for proxy/firewall blocking WebSocket connections

---

## Contributing

### Development Setup

1. Clone the repository
2. Install .NET 10 SDK
3. Open `src/MyBlog.slnx` in your IDE
4. Run `dotnet restore`
5. Run `dotnet build`
6. Run `dotnet test`

### Code Style

The project uses `.editorconfig` for consistent formatting:

- File-scoped namespaces
- 4-space indentation
- LF line endings
- Private fields prefixed with `_`

### Pull Request Process

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Write tests for new functionality
4. Ensure all tests pass
5. Commit changes (`git commit -m 'Add amazing feature'`)
6. Push to branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

### Testing Requirements

- All new features must have corresponding tests
- All tests must pass on Windows, Linux, and macOS
- Code coverage should not decrease

---

## License

MIT License

Copyright (c) 2026

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

---

## Acknowledgments

- Built with [.NET](https://dotnet.microsoft.com/) and [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
- Observability powered by [OpenTelemetry](https://opentelemetry.io/)
- Inspired by the simplicity of static site generators with the power of dynamic applications
- AI assistance provided by [Claude](https://www.anthropic.com/claude) (Anthropic) and [Gemini](https://deepmind.google/technologies/gemini/) (Google)

---

<p align="center">
  Built with ❤️ using .NET 10 and Blazor Server
</p>

























