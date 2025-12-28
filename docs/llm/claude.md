I am trying to develop a CMS / blog type website using dotnet 10 and blazor. 
please refine the prompt to be precise, concise, and perfect as a one shot prompt so claude generates the pefect shell script that gives me everything I need in one go. 

in this project, we develop a blog / cms software using the latest dotnet 10 technology. 
we will use asp dotnet and blazor. 
we don't support the users uploading files to this cms. 
if you can somehow make images fit in the sqlite database itself, we can host images but we definitely don't want to get in the business of hosting documents or movies at this time. 
please put all posts that users make in a sane relational database in sqlite. 
I don't want any external dependencies on postgresql or sql server. 
open-telemetry-hello-world shows how we can save open telemetry stuff into the file system. we should use xdg guidelines where possible and if the folder is not available, we should write to the same folder as we are in (with timestamps because we are nice) and if we can't even do that, we should keep going even without logging because the show must go on. 
the point of this application is a cross platform application that 
1. we can create / edit / and delete posts. we will require that users log in with a username and password to do this.
1. we can read posts. this does not require log in. 
1. the website should work fine with or without https. because some free of cost iis hosts such as monster asp require payment for this
we should save this otel stuff to both files and sqlite as well. 
as a guiding principle, we should stick to as few third party nuget packages as possible 
as a non-negotiable strict rule, we MUST NEVER EVER use nuget packages that are non-free. 
ban packages with a vengeance even if they allow "non commercial" or "open source" applications 
for example, fluent assertions, mass transit and so on are completely banned 
nuget packages by controversial people should also be banned 
for example, moq is banned from this repository. 
prefer fewer dependencies and more code written by us 
prefer long term stable code over flashy dependencies 
the code should be cross platform -- windows, macOS, and Linux 
as such it should be possible to run -- and stop -- the application within automated test environments such as github actions. 
generate a shell script that will then write the complete application in one shot. 
assume the shell script will run on a standard fedora linux workstation. 
current folder information is available on `output.txt` 
current folder contents is available in `dump.txt` 
dump.txt is generated with `export.sh` and will be kept up to date. 
I have created an `src` folder. 
all code including all unit tests and shell scripts live inside this src folder. 
do not write anything outside this src folder, do not delete anything outside this src folder. 
be kind and always explain in detail what you are doing and more importantly why for the next person or bot who needs to follow your actions
use xunit 3 for unit tests. 
try to keep up with the latest nuget packages. 
of course, where possible do NOT use packages at all. 
but it is not always possible. 
for example, it is probably better to use polly than to write it ourselves. 
always use and pass cancellation tokens where it makes sense 
always write async code where it makes sense 
always follow best practices 
always write testable code 
assume we will host the git repository publicly on github and generate github actions to build and test this repository on every single push or pull request to any branch 
and any push to `master`, `main`, or `develop` branches should deploy the application. 
```xml
<?xml version="1.0" encoding="utf-8"?>
<publishData>
  <publishProfile
    profileName="{siteName}-WebDeploy"
    publishMethod="MSDeploy"
    publishUrl="{siteName}.siteasp.net"
    msdeploySite="{siteName}"
    userName="{siteName}"
    userPWD="{sitePassword}"
    destinationAppUrl="http://{siteSubdomain}.runasp.net/"
    />
</publishData>
```
sample yaml 
```yaml
name: Build, publish and deploy to MonsterASP.NET
on: [push]

jobs:
  build_and_deploy:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0
          
      - name: Install dependencies
        run: dotnet restore
        
      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Publish
        run: dotnet publish --configuration Release --output ./publish --runtime win-x86 
        
      - name: Test with .NET
        run: dotnet test

      - name: Deploy to MonsterASP.NET via WebDeploy
        uses: rasmusbuchholdt/simply-web-deploy@2.1.0
        with:
          website-name: ${{ secrets.WEBSITE_NAME }}
          server-computer-name: ${{ secrets.SERVER_COMPUTER_NAME }}
          server-username: ${{ secrets.SERVER_USERNAME }}
          server-password: ${{ secrets.SERVER_PASSWORD }}
```
more information is at https://help.monsterasp.net/books/github/page/how-to-deploy-website-via-github-actions




I'll analyze your requirements and create a refined, precise one-shot prompt that will generate a complete shell script for your .NET 10 Blazor CMS/blog application.

Let me first understand the key requirements and then produce the refined prompt:I've created a refined, comprehensive one-shot prompt. Let me explain the key improvements I made:

**What the refined prompt includes:**

1. **Clear solution structure** - Explicitly defines the folder hierarchy so there's no ambiguity about where files go

2. **Precise database schema** - Defines all tables with exact column names and types, including the image BLOB storage you requested

3. **Authentication approach** - Specifies cookie-based auth with just the password hasher from Identity (avoiding the full Identity framework bloat)

4. **Explicit package allowlist/blocklist** - Lists exactly which packages are permitted and which are banned, including your specific bans (Moq, FluentAssertions, etc.)

5. **XDG-compliant fallback chain** - Clearly defines the three-tier fallback: XDG location → local ./telemetry/ → continue without file logging

6. **GitHub Actions workflow** - Provides the exact YAML structure for build/test on all pushes and deploy only on main/master/develop

7. **Code quality requirements** - Specifies CancellationToken usage, async patterns, and other best practices

8. **Testing scope** - Lists exactly what tests should be written

9. **Shell script format** - Specifies heredocs, idempotency, and progress messages

**Key refinements from your original:**

- Removed ambiguity about "latest" packages by specifying exact versions
- Made the auth strategy explicit (cookie + password hasher only)
- Defined the image storage approach (BLOB in SQLite)
- Clarified that HTTPS is configurable, not hardcoded
- Specified Bootstrap via CDN (no npm/node dependencies)
- Made the custom Markdown parser requirement explicit
- Added the telemetry-to-SQLite requirement you mentioned

Would you like me to adjust anything in this prompt, or shall I proceed to generate the actual shell script based on this refined prompt?





# One-Shot Prompt: Generate Complete .NET 10 Blazor CMS Application

Generate a single bash shell script (`src/generate-myblog.sh`) that creates a complete, production-ready blog/CMS application. The script will run on Fedora Linux and write all files to the `src/` folder.

## Project Overview

**Application**: MyBlog - A cross-platform blog/CMS built with .NET 10 and Blazor Server  
**Target**: Self-contained deployment to MonsterASP.NET (win-x86) and local development (Linux/macOS/Windows)

## Architecture Requirements

### Solution Structure
```
src/
├── MyBlog.sln (or .slnx)
├── Directory.Build.props
├── Directory.Packages.props
├── .editorconfig
├── MyBlog.Core/                 # Domain models, interfaces, constants
├── MyBlog.Infrastructure/       # SQLite data access, OpenTelemetry exporters
├── MyBlog.Web/                  # Blazor Server app (main entry point)
├── MyBlog.Tests/                # xUnit 3 tests
└── .github/workflows/
    └── build-deploy.yml
```

### Data Storage (SQLite Only)
- **Posts table**: Id (GUID), Title, Slug (unique), Content (Markdown), Summary, AuthorId, CreatedAt, UpdatedAt, PublishedAt (nullable), IsPublished
- **Users table**: Id (GUID), Username (unique), PasswordHash (using ASP.NET Core Identity's PasswordHasher), Email, DisplayName, CreatedAt
- **Images table**: Id (GUID), FileName, ContentType, Data (BLOB - base64 stored as byte[]), PostId (nullable FK), UploadedAt, UploadedBy
- **TelemetryLogs table**: Id, Timestamp, Level, Message, Exception, TraceId, SpanId, Properties (JSON)

Use EF Core with SQLite. Database file location: Follow XDG_DATA_HOME on Linux, %LOCALAPPDATA% on Windows, ~/Library/Application Support on macOS. Fallback to `./data/` relative to executable.

### Authentication
- Cookie-based authentication (NOT ASP.NET Core Identity - too heavy)
- Custom minimal auth: `IPasswordHasher<User>` from Microsoft.AspNetCore.Identity for password hashing only
- Login required for: Create, Edit, Delete posts and image upload
- No login required for: Reading posts, viewing images
- Seed a default admin user on first run (username: `admin`, password from environment variable `MYBLOG_ADMIN_PASSWORD` or default `ChangeMe123!`)

### Blazor Server Features
1. **Public pages** (no auth): Home (paginated post list), Post detail (by slug), About
2. **Admin pages** (auth required): Dashboard, Post editor (create/edit with Markdown preview), Post list with delete, Image manager
3. **Components**: Markdown renderer (custom, no third-party), Post card, Pagination
4. Works with or without HTTPS (configure in appsettings.json, not hardcoded)

### OpenTelemetry (Following open-telemetry-hello-world Pattern)
- Custom file exporters for traces, metrics, and logs (adapt from the provided FileActivityExporter, FileLogExporter, FileMetricExporter patterns)
- ALSO write telemetry to SQLite TelemetryLogs table
- XDG-compliant directory selection with graceful fallback:
  1. Try XDG_DATA_HOME/MyBlog/telemetry (or platform equivalent)
  2. Fallback to ./telemetry/ in current directory
  3. If both fail, continue without file logging (log to console only)
- Never crash due to telemetry failures

### Strict Package Rules

**ALLOWED packages only**:
```xml
<!-- Core Framework -->
<PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
<PackageVersion Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0" />
<PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.0" />

<!-- OpenTelemetry (official packages only) -->
<PackageVersion Include="OpenTelemetry" Version="1.14.0" />
<PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.14.0" />
<PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.14.0" />

<!-- Testing (xUnit 3) -->
<PackageVersion Include="xunit.v3" Version="1.0.0" />
<PackageVersion Include="xunit.runner.visualstudio" Version="3.0.0" />
<PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
```

**BANNED** (do not use under any circumstances):
- FluentAssertions, Moq, NSubstitute, FakeItEasy (use xUnit assertions + manual test doubles)
- MassTransit, MediatR, AutoMapper
- Any package with non-MIT/Apache-2.0 license
- Markdig or any Markdown library (write a simple custom parser)
- Any UI component library (use plain Bootstrap CSS via CDN)

### GitHub Actions Workflow

```yaml
name: Build, Test, and Deploy

on:
  push:
    branches: ['**']
  pull_request:
    branches: ['**']

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          dotnet-quality: 'preview'
      - run: dotnet restore src/MyBlog.sln
      - run: dotnet build src/MyBlog.sln -c Release --no-restore
      - run: dotnet test src/MyBlog.sln -c Release --no-build

  deploy:
    needs: build-test
    if: github.ref == 'refs/heads/main' || github.ref == 'refs/heads/master' || github.ref == 'refs/heads/develop'
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
          dotnet-quality: 'preview'
      - run: dotnet publish src/MyBlog.Web/MyBlog.Web.csproj -c Release -o ./publish -r win-x86 --self-contained false
      - uses: rasmusbuchholdt/simply-web-deploy@2.1.0
        with:
          website-name: ${{ secrets.WEBSITE_NAME }}
          server-computer-name: ${{ secrets.SERVER_COMPUTER_NAME }}
          server-username: ${{ secrets.SERVER_USERNAME }}
          server-password: ${{ secrets.SERVER_PASSWORD }}
```

### Code Quality Requirements
- All async methods accept and pass `CancellationToken`
- All database operations are async
- Use `IAsyncDisposable` where appropriate
- Interfaces for all services (testability)
- No static state except for ActivitySource/Meter definitions
- Comprehensive XML documentation on public APIs
- Use record types for DTOs
- Use init-only properties where appropriate
- Nullable reference types enabled
- Treat warnings as errors

### Shell Script Requirements
The generated `src/generate-myblog.sh` script must:
1. Start with `#!/bin/bash` and `set -e`
2. Print clear progress messages explaining each step
3. Create all directories first
4. Write each file using heredocs (`cat << 'EOF' > filename`)
5. Make the script idempotent (safe to run multiple times)
6. End with instructions on how to run the application
7. Be executable (`chmod +x`)

### Testing Requirements
Include tests for:
- Password hashing (verify hash, verify wrong password fails)
- Post repository CRUD operations (use in-memory SQLite)
- Markdown parser (basic cases: headings, bold, italic, links, code blocks)
- Authentication service (login success/failure)
- Slug generation from title

## Output
Generate ONLY the shell script content. No explanations before or after. The script should be complete and immediately runnable with `bash src/generate-myblog.sh`.


