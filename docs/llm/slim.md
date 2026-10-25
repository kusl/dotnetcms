```yaml docker-compose.e2e.yml
services:
  myblog-web:
    build:
      context: ./src
      dockerfile: MyBlog.Web/Dockerfile
    container_name: myblog-web
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:5000
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/myblog.db
      - Authentication__DefaultAdminPassword=ChangeMe123!
    volumes:
      - myblog-data:/app/data:Z
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/"]
      interval: 5s
      timeout: 10s
      retries: 10
      start_period: 30s
    networks:
      - myblog-network

  myblog-e2e:
    build:
      context: ./src
      dockerfile: MyBlog.E2E/Dockerfile
    container_name: myblog-e2e
    depends_on:
      myblog-web:
        condition: service_healthy
    environment:
      - MYBLOG_BASE_URL=http://myblog-web:5000
      - PLAYWRIGHT_HEADLESS=true
    networks:
      - myblog-network

networks:
  myblog-network:
    driver: bridge

volumes:
  myblog-data:
```

```yaml docker-compose.yml
services:
  myblog:
    build:
      context: ./src
      dockerfile: MyBlog.Web/Dockerfile
    container_name: myblog
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:5000
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/myblog.db
      - Authentication__DefaultAdminPassword=ChangeMe123!
    volumes:
      - myblog-data:/app/data:Z
    restart: unless-stopped

volumes:
  myblog-data:
```

```ini .editorconfig
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{cs,csx,razor}]
dotnet_sort_system_directives_first = true
csharp_using_directive_placement = outside_namespace:warning

csharp_style_namespace_declarations = file_scoped:error
csharp_prefer_braces = true:warning

csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion

csharp_style_expression_bodied_properties = true:suggestion
csharp_style_expression_bodied_methods = true:suggestion

dotnet_naming_rule.private_fields_should_be_underscored.severity = suggestion
dotnet_naming_rule.private_fields_should_be_underscored.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_underscored.style = prefix_underscore

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.prefix_underscore.capitalization = camel_case
dotnet_naming_style.prefix_underscore.required_prefix = _

[*.{json,yaml,yml}]
indent_size = 2

[*.md]
trim_trailing_whitespace = false
```

```yaml .github/workflows/build-deploy.yml
name: Build, Test, and Deploy

on:
  push:
    branches: ['**']
  pull_request:
    branches: ['**']

env:
  DOTNET_VERSION: '10.0.x'
  DOTNET_NOLOGO: 1
  DOTNET_CLI_TELEMETRY_OPTOUT: 1

jobs:
  build-test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}

    steps:
      - name: Checkout repository
        uses: actions/checkout@v7

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore src/MyBlog.slnx

      - name: Build solution
        run: dotnet build src/MyBlog.slnx -c Release --no-restore

      - name: Run unit tests
        run: dotnet run --project src/MyBlog.Tests/MyBlog.Tests.csproj -c Release --no-build

  e2e-tests:
    runs-on: ubuntu-latest
    needs: build-test

    steps:
      - name: Checkout repository
        uses: actions/checkout@v7

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore src/MyBlog.slnx

      - name: Build solution
        run: dotnet build src/MyBlog.slnx -c Release --no-restore

      - name: Install Playwright browsers
        run: |
          script="$(find src/MyBlog.E2E/bin/Release -name playwright.ps1 -print -quit)"
          test -n "$script"
          pwsh "$script" install chromium --with-deps

      - name: Start MyBlog application
        run: |
          dotnet run --project src/MyBlog.Web/MyBlog.Web.csproj -c Release --no-build --no-launch-profile > app.log 2>&1 &
          echo $! > app.pid
        env:
          ASPNETCORE_URLS: http://localhost:5000
          ASPNETCORE_ENVIRONMENT: Development
          Authentication__DefaultAdminPassword: ChangeMe123!

      - name: Wait for application to be ready
        run: |
          for i in {1..60}; do
            if curl -sf http://localhost:5000/ > /dev/null; then
              echo "Application is ready"
              exit 0
            fi
            sleep 2
          done
          echo "Application failed to start within 120 seconds"
          cat app.log
          exit 1

      - name: Run E2E tests
        run: dotnet run --project src/MyBlog.E2E/MyBlog.E2E.csproj -c Release --no-build
        env:
          MYBLOG_BASE_URL: http://localhost:5000
          PLAYWRIGHT_HEADLESS: true

      - name: Stop application
        if: always()
        run: |
          if [ -f app.pid ]; then
            kill "$(cat app.pid)" || true
          fi

      - name: Upload application log
        uses: actions/upload-artifact@v7
        if: always()
        with:
          name: e2e-app-log
          path: app.log
          retention-days: 7

  deploy:
    needs: [build-test, e2e-tests]
    if: github.ref == 'refs/heads/main'
    runs-on: windows-latest
    name: deploy (${{ matrix.target }})
    strategy:
      fail-fast: false
      matrix:
        include:
          - target: primary
            prefix: ''
          - target: nice
            prefix: 'NICE_'

    steps:
      - name: Checkout repository
        uses: actions/checkout@v7

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Publish application
        run: dotnet publish src/MyBlog.Web/MyBlog.Web.csproj -c Release -o ./publish -r win-x86 --self-contained false

      - name: Inject OTLP configuration into web.config
        shell: pwsh
        env:
          HONEYCOMB_API_KEY: ${{ secrets[format('{0}HONEYCOMB_API_KEY', matrix.prefix)] }}
          OTLP_ENDPOINT: ${{ secrets[format('{0}OTLP_ENDPOINT', matrix.prefix)] }}
          SECRET_PREFIX: ${{ matrix.prefix }}
        run: |
          $webConfigPath = "./publish/web.config"
          if (-not (Test-Path $webConfigPath)) {
            Write-Error "web.config not found at $webConfigPath"
            exit 1
          }

          [xml]$xml = Get-Content $webConfigPath

          $aspNetCore = $xml.SelectSingleNode('//aspNetCore')
          if (-not $aspNetCore) {
            Write-Error "No aspNetCore element found in web.config"
            exit 1
          }

          $envVars = $aspNetCore.SelectSingleNode('environmentVariables')
          if (-not $envVars) {
            $envVars = $xml.CreateElement('environmentVariables')
            $aspNetCore.AppendChild($envVars) | Out-Null
          }

          if (-not [string]::IsNullOrWhiteSpace($env:HONEYCOMB_API_KEY)) {
            $apiKey = $xml.CreateElement('environmentVariable')
            $apiKey.SetAttribute('name', 'Otlp__ApiKey')
            $apiKey.SetAttribute('value', $env:HONEYCOMB_API_KEY)
            $envVars.AppendChild($apiKey) | Out-Null
            Write-Host "Added Otlp__ApiKey to web.config"
          } else {
            Write-Host "No $($env:SECRET_PREFIX)HONEYCOMB_API_KEY secret configured; OTLP export will be disabled"
          }

          $endpoint = if (-not [string]::IsNullOrWhiteSpace($env:OTLP_ENDPOINT)) { $env:OTLP_ENDPOINT } else { "https://api.honeycomb.io" }
          $endpointVar = $xml.CreateElement('environmentVariable')
          $endpointVar.SetAttribute('name', 'Otlp__Endpoint')
          $endpointVar.SetAttribute('value', $endpoint)
          $envVars.AppendChild($endpointVar) | Out-Null
          Write-Host "Added Otlp__Endpoint=$endpoint to web.config"

          $xml.Save((Resolve-Path $webConfigPath).Path)

      - name: Deploy via WebDeploy
        shell: pwsh
        env:
          DEPLOY_SOURCE: ${{ github.workspace }}\publish
          DEPLOY_SITE: ${{ secrets[format('{0}WEBSITE_NAME', matrix.prefix)] }}
          DEPLOY_HOST: ${{ secrets[format('{0}SERVER_COMPUTER_NAME', matrix.prefix)] }}
          DEPLOY_USER: ${{ secrets[format('{0}SERVER_USERNAME', matrix.prefix)] }}
          DEPLOY_PASSWORD: ${{ secrets[format('{0}SERVER_PASSWORD', matrix.prefix)] }}
        run: |
          $msdeployPath = "C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe"

          if (-not (Test-Path $msdeployPath)) {
            choco install webdeploy -y --no-progress
          }

          Write-Host "Deploying to $env:DEPLOY_HOST..."

          $sourceArg = "-source:contentPath=$env:DEPLOY_SOURCE"
          $destArg = "-dest:contentPath=$env:DEPLOY_SITE,computerName=https://$($env:DEPLOY_HOST):8172/MsDeploy.axd?site=$env:DEPLOY_SITE,userName=$env:DEPLOY_USER,password=$env:DEPLOY_PASSWORD,AuthType='Basic'"

          & $msdeployPath -verb:sync $sourceArg $destArg `
            -allowUntrusted `
            -enableRule:DoNotDeleteRule `
            -enableRule:AppOffline `
            -retryAttempts:3 `
            -retryInterval:3000

          if ($LASTEXITCODE -ne 0) {
            Write-Error "Deployment failed with exit code $LASTEXITCODE"
            exit 1
          }

          Write-Host "Deployment completed successfully"
```

```bash run-e2e.sh
#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")"

COMPOSE_FILE="docker-compose.e2e.yml"
RESULTS_DIR="test-results"
BUILD=true

log() { printf '\033[0;32m[INFO]\033[0m %s\n' "$1"; }
fail() { printf '\033[0;31m[ERROR]\033[0m %s\n' "$1" >&2; }

compose() { podman-compose -f "$COMPOSE_FILE" "$@"; }

for arg in "$@"; do
    case "$arg" in
        --no-build) BUILD=false ;;
        --clean)
            log "Removing E2E containers, networks and volumes..."
            compose down -v --remove-orphans || true
            exit 0
            ;;
        *)
            fail "Unknown argument: $arg (supported: --no-build, --clean)"
            exit 2
            ;;
    esac
done

cleanup() {
    mkdir -p "$RESULTS_DIR"
    compose logs myblog-web > "$RESULTS_DIR/myblog-web.log" 2>&1 || true
    compose logs myblog-e2e > "$RESULTS_DIR/myblog-e2e.log" 2>&1 || true
    compose down -v --remove-orphans > /dev/null 2>&1 || true
}

compose down -v --remove-orphans > /dev/null 2>&1 || true
trap cleanup EXIT

if [[ "$BUILD" == true ]]; then
    log "Building images (layer cache makes unchanged builds fast)..."
    compose build
else
    log "Skipping build (--no-build); using existing images"
fi

log "Starting MyBlog..."
compose up -d myblog-web

for attempt in $(seq 1 60); do
    if podman exec myblog-web curl -sf http://localhost:5000/ > /dev/null 2>&1; then
        log "MyBlog is ready"
        break
    fi
    if [[ "$attempt" -eq 60 ]]; then
        fail "MyBlog failed to start within 120 seconds"
        compose logs myblog-web || true
        exit 1
    fi
    sleep 2
done

log "Running E2E tests..."
compose up --no-deps myblog-e2e || true

E2E_EXIT_CODE="$(podman inspect --format '{{.State.ExitCode}}' myblog-e2e 2>/dev/null || echo 1)"

if [[ "$E2E_EXIT_CODE" -eq 0 ]]; then
    log "E2E tests passed"
else
    fail "E2E tests failed with exit code $E2E_EXIT_CODE (logs in $RESULTS_DIR/)"
fi

exit "$E2E_EXIT_CODE"
```

```bash run.sh
#!/usr/bin/env bash
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null || printf '%s' "$SCRIPT_DIR")"
SRC_DIR="$REPO_ROOT/src"
OUTPUT_DIR="$REPO_ROOT/docs/llm/output"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

mkdir -p "$OUTPUT_DIR"
LOG_FILE="$OUTPUT_DIR/$(date +%Y-%m-%d-%H-%M-%S).txt"
: > "$LOG_FILE"

STEP_LABELS=()
STEP_CODES=()
STEP_TIMES=()
FAILURES=0

have() { command -v "$1" >/dev/null 2>&1; }

run_step() {
    local label="$1"; shift
    local index=$(( ${#STEP_LABELS[@]} + 1 ))
    printf '[%02d] %s ...\n' "$index" "$label" >&2
    {
        printf '\n###############################################################################\n'
        printf '# STEP %d: %s\n' "$index" "$label"
        printf '###############################################################################\n'
        printf '$ %s\n\n' "$*"
    } >> "$LOG_FILE"

    local start=$SECONDS rc
    { "$@"; } < /dev/null >> "$LOG_FILE" 2>&1
    rc=$?
    local duration=$(( SECONDS - start ))

    printf '\n[result] exit=%d duration=%ds\n' "$rc" "$duration" >> "$LOG_FILE"
    STEP_LABELS+=("$label")
    STEP_CODES+=("$rc")
    STEP_TIMES+=("$duration")

    if [[ "$rc" -ne 0 ]]; then
        FAILURES=$(( FAILURES + 1 ))
        printf '[%02d] %s -> FAILED (exit %d, %ds)\n' "$index" "$label" "$rc" "$duration" >&2
    else
        printf '[%02d] %s -> ok (%ds)\n' "$index" "$label" "$duration" >&2
    fi
}

{
    printf 'MyBlog run.sh\n'
    printf '  Started    : %s\n' "$(date --iso-8601=seconds)"
    printf '  Repository : %s\n' "$REPO_ROOT"
    printf '  Branch     : %s\n' "$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || echo unknown)"
    printf '  Commit     : %s\n' "$(git -C "$REPO_ROOT" rev-parse HEAD 2>/dev/null || echo unknown)"
    printf '  OS         : %s\n' "$(uname -srmo 2>/dev/null || echo unknown)"
} >> "$LOG_FILE"

printf 'run.sh: logging to %s\n' "$LOG_FILE" >&2

if [[ ! -d "$SRC_DIR" ]]; then
    printf 'run.sh: source directory not found: %s\n' "$SRC_DIR" >&2
    exit 1
fi

cd "$SRC_DIR" || exit 1

if have dotnet; then
    run_step "dotnet --info" dotnet --info
    run_step "dotnet format" dotnet format
    run_step "dotnet restore" dotnet restore
    run_step "dotnet clean" dotnet clean
    run_step "dotnet build" dotnet build
    run_step "dotnet run (MyBlog.Tests)" dotnet run --project MyBlog.Tests/MyBlog.Tests.csproj
    run_step "dotnet list package" dotnet list package
    run_step "dotnet list package --outdated" dotnet list package --outdated
else
    run_step "dotnet CLI available" false
fi

for tool in podman podman-compose; do
    if have "$tool"; then
        run_step "$tool --version" "$tool" --version
    fi
done

cd "$REPO_ROOT" || exit 1

run_step "git status" git --no-pager status --short --branch
run_step "export.sh" bash "$REPO_ROOT/export.sh"
run_step "run-e2e.sh" bash "$REPO_ROOT/run-e2e.sh"

SUMMARY="$(
    printf '\n###############################################################################\n# SUMMARY\n###############################################################################\n'
    for i in "${!STEP_LABELS[@]}"; do
        status=PASS
        [[ "${STEP_CODES[$i]}" -ne 0 ]] && status=FAIL
        printf '  [%s] %-36s exit=%-4s %ss\n' "$status" "${STEP_LABELS[$i]}" "${STEP_CODES[$i]}" "${STEP_TIMES[$i]}"
    done
    printf '  Steps: %d  Failures: %d\n  Log: %s\n' "${#STEP_LABELS[@]}" "$FAILURES" "$LOG_FILE"
)"
printf '%s\n' "$SUMMARY" >> "$LOG_FILE"
printf '%s\n' "$SUMMARY" >&2

[[ "$FAILURES" -eq 0 ]]
```

```xml src/Directory.Build.props
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest</AnalysisLevel>
  </PropertyGroup>

  <PropertyGroup Condition="$(MSBuildProjectName.EndsWith('.Tests')) Or $(MSBuildProjectName.EndsWith('.E2E'))">
    <IsPackable>false</IsPackable>
    <OutputType>Exe</OutputType>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
</Project>
```

```xml src/Directory.Packages.props
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
    <MicrosoftPackagesVersion>10.0.13</MicrosoftPackagesVersion>
    <OpenTelemetryVersion>1.20.1</OpenTelemetryVersion>
    <OtelAspNetCoreInstrVersion>1.20.1</OtelAspNetCoreInstrVersion>
    <OtelHttpInstrVersion>1.20.1</OtelHttpInstrVersion>

    <TestSdkVersion>18.11.0</TestSdkVersion>
    <XUnitVersion>4.0.2</XUnitVersion>
    <PlaywrightVersion>1.64.1</PlaywrightVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Microsoft.AspNetCore.SignalR.Client" Version="$(MicrosoftPackagesVersion)" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="$(MicrosoftPackagesVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="$(MicrosoftPackagesVersion)" />
    <PackageVersion Include="OpenTelemetry" Version="$(OpenTelemetryVersion)" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="$(OpenTelemetryVersion)" />
    <PackageVersion Include="OpenTelemetry.Exporter.Console" Version="$(OpenTelemetryVersion)" />
    <PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="$(OpenTelemetryVersion)" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="$(OtelAspNetCoreInstrVersion)" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="$(OtelHttpInstrVersion)" />

    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="$(TestSdkVersion)" />
    <PackageVersion Include="xunit.v3" Version="$(XUnitVersion)" />
    <PackageVersion Include="Microsoft.Playwright" Version="$(PlaywrightVersion)" />
  </ItemGroup>
</Project>
```

```text src/.dockerignore
**/bin/
**/obj/
**/TestResults/
```

```json src/global.json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "major"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

```csharp src/MyBlog.Core/Constants/AppConstants.cs
namespace MyBlog.Core.Constants;

public static class AppConstants
{
    public const string AuthCookieName = "MyBlog.Auth";

    public const string AdminRole = "Admin";

    public const int DefaultPageSize = 10;

    public const int MaxImageSizeBytes = 5 * 1024 * 1024;

    public static readonly string[] AllowedImageTypes =
        ["image/jpeg", "image/png", "image/gif", "image/webp"];
}
```

```csharp src/MyBlog.Core/Interfaces/IAuthService.cs
using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

public interface IAuthService
{
    Task<User?> AuthenticateAsync(
        string username, string password, CancellationToken cancellationToken = default);

    Task EnsureAdminUserAsync(CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}
```

```csharp src/MyBlog.Core/Interfaces/IImageDimensionService.cs
namespace MyBlog.Core.Interfaces;

public interface IImageDimensionService
{
    Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default);
}
```

```csharp src/MyBlog.Core/Interfaces/IImageRepository.cs
using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

public interface IImageRepository
{
    Task<IReadOnlyList<Image>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Image> CreateAsync(Image image, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

```csharp src/MyBlog.Core/Interfaces/IMarkdownService.cs
namespace MyBlog.Core.Interfaces;

public interface IMarkdownService
{
    Task<string> ToHtmlAsync(string markdown);
}
```

```csharp src/MyBlog.Core/Interfaces/IPasswordService.cs
namespace MyBlog.Core.Interfaces;

public interface IPasswordService
{
    string HashPassword(string password);

    bool VerifyPassword(string hashedPassword, string providedPassword);
}
```

```csharp src/MyBlog.Core/Interfaces/IPostRepository.cs
using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

public interface IPostRepository
{
    Task<(IReadOnlyList<PostListItemDto> Posts, int TotalCount)> GetPublishedPostsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PostListItemDto>> GetAllPostsAsync(
        CancellationToken cancellationToken = default);

    Task<PostDetailDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Post> CreateAsync(Post post, CancellationToken cancellationToken = default);

    Task UpdateAsync(Post post, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(CancellationToken cancellationToken = default);

    Task<int> GetPublishedCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PostListItemDto>> GetRecentPostsAsync(
        int count, CancellationToken cancellationToken = default);

    Task<bool> IsSlugTakenAsync(string slug, Guid? excludePostId = null, CancellationToken cancellationToken = default);
}
```

```csharp src/MyBlog.Core/Interfaces/IReaderTrackingService.cs
namespace MyBlog.Core.Interfaces;

public interface IReaderTrackingService
{
    int JoinPost(string slug, string connectionId);

    int LeavePost(string slug, string connectionId);

    (string? Slug, int NewCount) Disconnect(string connectionId);

    int GetReaderCount(string slug);
}
```

```csharp src/MyBlog.Core/Interfaces/ISlugService.cs
namespace MyBlog.Core.Interfaces;

public interface ISlugService
{
    string GenerateSlugOrUuid(string title);

    string GenerateUuidSlug();
}
```

```csharp src/MyBlog.Core/Interfaces/ITelemetryLogRepository.cs
namespace MyBlog.Core.Interfaces;

public interface ITelemetryLogRepository
{
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);

    Task<int> TrimToNewestAsync(int maxRows, CancellationToken cancellationToken = default);
}
```

```csharp src/MyBlog.Core/Interfaces/IUserRepository.cs
using MyBlog.Core.Models;

namespace MyBlog.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default);

    Task CreateAsync(User user, CancellationToken cancellationToken = default);

    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

```csharp src/MyBlog.Core/Models/Image.cs
namespace MyBlog.Core.Models;

public sealed class Image
{
    public Guid Id { get; set; }

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    public Guid? PostId { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public Guid UploadedByUserId { get; set; }

    public Post? Post { get; set; }

    public User? UploadedBy { get; set; }
}
```

```csharp src/MyBlog.Core/Models/ImageDimensionCache.cs
namespace MyBlog.Core.Models;

public sealed class ImageDimensionCache
{
    public required string Url { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public DateTime LastCheckedUtc { get; set; }
}
```

```csharp src/MyBlog.Core/Models/Post.cs
namespace MyBlog.Core.Models;

public sealed class Post
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public required string Slug { get; set; }

    public required string Content { get; set; }

    public required string Summary { get; set; }

    public Guid AuthorId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public bool IsPublished { get; set; }

    public User? Author { get; set; }

    public ICollection<Image> Images { get; set; } = [];
}
```

```csharp src/MyBlog.Core/Models/PostDto.cs
namespace MyBlog.Core.Models;

public sealed record PostListItemDto(
    Guid Id,
    string Title,
    string Slug,
    string Summary,
    string AuthorDisplayName,
    DateTime? PublishedAtUtc,
    bool IsPublished);

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
```

```csharp src/MyBlog.Core/Models/TelemetryLog.cs
namespace MyBlog.Core.Models;

public sealed class TelemetryLog
{
    public int Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public required string Level { get; set; }

    public required string Category { get; set; }

    public required string Message { get; set; }

    public string? Exception { get; set; }

    public string? TraceId { get; set; }

    public string? SpanId { get; set; }

    public string? Properties { get; set; }
}
```

```csharp src/MyBlog.Core/Models/User.cs
namespace MyBlog.Core.Models;

public sealed class User
{
    public Guid Id { get; set; }

    public required string Username { get; set; }

    public required string PasswordHash { get; set; }

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Post> Posts { get; set; } = [];

    public ICollection<Image> UploadedImages { get; set; } = [];
}
```

```xml src/MyBlog.Core/MyBlog.Core.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>
</Project>
```

````csharp src/MyBlog.Core/Services/MarkdownService.cs
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;

namespace MyBlog.Core.Services;

public sealed partial class MarkdownService(
    IImageDimensionService imageDimensionService,
    ILogger<MarkdownService>? logger = null)
    : IMarkdownService
{
    private enum ListType { None, Unordered, Ordered }

    public async Task<string> ToHtmlAsync(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var result = new StringBuilder();
        var inCodeBlock = false;
        var currentListType = ListType.None;
        var codeBlockContent = new StringBuilder();

        foreach (var rawLine in lines)
        {
            var line = rawLine;

            if (line.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    result.Append("<pre><code>");
                    result.Append(HtmlEncode(codeBlockContent.ToString().TrimEnd()));
                    result.AppendLine("</code></pre>");
                    codeBlockContent.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    result.Append(CloseList(ref currentListType));
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockContent.AppendLine(line);
                continue;
            }

            if (HorizontalRulePattern().IsMatch(line))
            {
                result.Append(CloseList(ref currentListType));
                result.AppendLine("<hr />");
                continue;
            }

            var headingMatch = HeadingPattern().Match(line);
            if (headingMatch.Success)
            {
                result.Append(CloseList(ref currentListType));
                var level = headingMatch.Groups[1].Value.Length;
                var text = await ProcessInlineAsync(headingMatch.Groups[2].Value);
                result.AppendLine($"<h{level}>{text}</h{level}>");
                continue;
            }

            if (line.StartsWith("> "))
            {
                result.Append(CloseList(ref currentListType));
                var quoteText = await ProcessInlineAsync(line[2..]);
                result.AppendLine($"<blockquote><p>{quoteText}</p></blockquote>");
                continue;
            }

            var unorderedMatch = UnorderedListPattern().Match(line);
            if (unorderedMatch.Success)
            {
                if (currentListType != ListType.Unordered)
                {
                    result.Append(CloseList(ref currentListType));
                    result.AppendLine("<ul>");
                    currentListType = ListType.Unordered;
                }
                var itemText = await ProcessInlineAsync(unorderedMatch.Groups[1].Value);
                result.AppendLine($"<li>{itemText}</li>");
                continue;
            }

            var orderedMatch = OrderedListPattern().Match(line);
            if (orderedMatch.Success)
            {
                if (currentListType != ListType.Ordered)
                {
                    result.Append(CloseList(ref currentListType));
                    result.AppendLine("<ol>");
                    currentListType = ListType.Ordered;
                }

                var itemText = await ProcessInlineAsync(orderedMatch.Groups[1].Value);
                result.AppendLine($"<li>{itemText}</li>");
                continue;
            }

            if (currentListType != ListType.None && !string.IsNullOrWhiteSpace(line))
            {
                result.Append(CloseList(ref currentListType));
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                result.Append(CloseList(ref currentListType));
                continue;
            }

            var paragraphText = await ProcessInlineAsync(line);
            result.AppendLine($"<p>{paragraphText}</p>");
        }

        result.Append(CloseList(ref currentListType));

        if (inCodeBlock)
        {
            result.Append("<pre><code>");
            result.Append(HtmlEncode(codeBlockContent.ToString().TrimEnd()));
            result.AppendLine("</code></pre>");
        }

        return result.ToString();
    }

    private static string CloseList(ref ListType listType)
    {
        var result = listType switch
        {
            ListType.Unordered => "</ul>\n",
            ListType.Ordered => "</ol>\n",
            _ => ""
        };
        listType = ListType.None;
        return result;
    }

    private static string HtmlEncode(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                case '&':
                    sb.Append("&amp;");
                    break;
                case '"':
                    sb.Append("&quot;");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private async Task<string> ProcessInlineAsync(string text)
    {
        text = HtmlEncode(text);

        text = InlineCodePattern().Replace(text, "<code>$1</code>");

        var matches = ImagePattern().Matches(text);
        if (matches.Count > 0)
        {
            for (var i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                var alt = match.Groups[1].Value;
                var url = match.Groups[2].Value;

                string imgTag;
                try
                {
                    var dimensions = await imageDimensionService.GetDimensionsAsync(WebUtility.HtmlDecode(url));

                    imgTag = dimensions.HasValue ?
                        $"<img src=\"{url}\" alt=\"{alt}\" width=\"{dimensions.Value.Width}\" height=\"{dimensions.Value.Height}\" />" :
                        $"<img src=\"{url}\" alt=\"{alt}\" />";
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to get dimensions for image {Url}. Rendering without dimensions.", url);
                    imgTag = $"<img src=\"{url}\" alt=\"{alt}\" />";
                }

                text = text.Remove(match.Index, match.Length).Insert(match.Index, imgTag);
            }
        }

        text = LinkPattern().Replace(text, "<a href=\"$2\">$1</a>");

        text = BoldPattern().Replace(text, match =>
        {
            var content = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return $"<strong>{content}</strong>";
        });

        text = ItalicPattern().Replace(text, match =>
        {
            var content = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return $"<em>{content}</em>";
        });

        return text;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^[-*]\s+(.+)$")]
    private static partial Regex UnorderedListPattern();

    [GeneratedRegex(@"^\d+\.\s+(.+)$")]
    private static partial Regex OrderedListPattern();

    [GeneratedRegex(@"^[-*_]{3,}\s*$")]
    private static partial Regex HorizontalRulePattern();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCodePattern();

    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)")]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"\*\*(.+?)\*\*|__(.+?)__")]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)|(?<!_)_(?!_)(.+?)(?<!_)_(?!_)")]
    private static partial Regex ItalicPattern();
}
````

```csharp src/MyBlog.Core/Services/SlugService.cs
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MyBlog.Core.Interfaces;

namespace MyBlog.Core.Services;

public sealed partial class SlugService : ISlugService
{
    public string GenerateSlugOrUuid(string title)
    {
        var slug = GenerateSlug(title);

        return !string.IsNullOrWhiteSpace(slug) ? slug : GenerateUuidSlug();
    }

    public string GenerateUuidSlug() => $"post-{Guid.CreateVersion7()}";

    private static string GenerateSlug(string title)
    {
        var normalized = title.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        result = SpacePattern().Replace(result, "-");

        result = NonAlphanumericPattern().Replace(result, "");

        result = MultipleHyphenPattern().Replace(result, "-");

        result = result.Trim('-');

        return result;
    }

    [GeneratedRegex(@"[\s_]+")]
    private static partial Regex SpacePattern();

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex NonAlphanumericPattern();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphenPattern();
}
```

```csharp src/MyBlog.E2E/BlazorPageExtensions.cs
using Microsoft.Playwright;

namespace MyBlog.E2E;

public static class BlazorPageExtensions
{
    public const string AdminUsername = "admin";
    public const string AdminPassword = "ChangeMe123!";

    private const string InteractiveMarkerSelector = ".layout[data-blazor-interactive='true']";

    private const float DefaultTimeoutMs = 30000;

    public static async Task WaitForBlazorInteractiveAsync(this IPage page, float timeout = DefaultTimeoutMs)
    {
        var marker = page.Locator(InteractiveMarkerSelector);
        await Assertions.Expect(marker).ToBeAttachedAsync(new() { Timeout = timeout });
    }

    public static async Task SubmitLoginAsync(this IPage page, string username, string password, string expectedUrl)
    {
        await page.GotoAsync("/login");
        await page.WaitForBlazorInteractiveAsync();

        await page.FillAsync("input[name='username']", username);
        await page.FillAsync("input[name='password']", password);

        await Task.WhenAll(
            page.WaitForURLAsync(expectedUrl, new() { Timeout = DefaultTimeoutMs }),
            page.Locator("button[type='submit']").ClickAsync());

        await page.WaitForBlazorInteractiveAsync();
    }

    public static Task LoginAsAdminAsync(this IPage page) =>
        page.SubmitLoginAsync(AdminUsername, AdminPassword, "**/admin**");
}
```

```dockerfile src/MyBlog.E2E/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY MyBlog.Core/MyBlog.Core.csproj MyBlog.Core/
COPY MyBlog.Infrastructure/MyBlog.Infrastructure.csproj MyBlog.Infrastructure/
COPY MyBlog.Web/MyBlog.Web.csproj MyBlog.Web/
COPY MyBlog.E2E/MyBlog.E2E.csproj MyBlog.E2E/

RUN dotnet restore MyBlog.E2E/MyBlog.E2E.csproj

RUN mkdir PlaywrightInstaller \
    && printf '<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="Microsoft.Playwright" /></ItemGroup></Project>\n' > PlaywrightInstaller/PlaywrightInstaller.csproj \
    && dotnet build PlaywrightInstaller/PlaywrightInstaller.csproj -c Release -o /tmp/playwright -p:TreatWarningsAsErrors=false \
    && pwsh /tmp/playwright/playwright.ps1 install chromium --with-deps \
    && find /root/.cache/ms-playwright -maxdepth 1 -name 'chromium*' | grep -q . \
    && rm -rf PlaywrightInstaller /tmp/playwright /var/lib/apt/lists/*

COPY . .

RUN dotnet build MyBlog.E2E/MyBlog.E2E.csproj -c Release

WORKDIR /src/MyBlog.E2E

ENV MYBLOG_BASE_URL=http://myblog-web:5000
ENV PLAYWRIGHT_HEADLESS=true

ENTRYPOINT ["dotnet", "run", "-c", "Release", "--no-build"]
```

```xml src/MyBlog.E2E/MyBlog.E2E.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Microsoft.Playwright" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyBlog.Web\MyBlog.Web.csproj" />
  </ItemGroup>
</Project>
```

```csharp src/MyBlog.E2E/PlaywrightCollection.cs
using Xunit;

namespace MyBlog.E2E;

[CollectionDefinition(Name)]
public sealed class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Playwright";
}
```

```csharp src/MyBlog.E2E/PlaywrightFixture.cs
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E;

public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Browser not initialized");
    public IPlaywright Playwright => _playwright ?? throw new InvalidOperationException("Playwright not initialized");

    public string BaseUrl => Environment.GetEnvironmentVariable("MYBLOG_BASE_URL") ?? "http://localhost:5000";

    public async ValueTask InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        var headless = Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADLESS") != "false";

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless,
            SlowMo = headless ? 0 : 100
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    public async Task<IBrowserContext> CreateContextAsync() =>
        await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });

    public async Task<IPage> CreatePageAsync()
    {
        var context = await CreateContextAsync();
        return await context.NewPageAsync();
    }
}
```

```csharp src/MyBlog.E2E/Tests/AboutPageTests.cs
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class AboutPageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task AboutPage_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();

        var response = await page.GotoAsync("/about");

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected OK response, got {response.Status}");
    }

    [Fact]
    public async Task AboutPage_DisplaysAboutHeading()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
        await Assertions.Expect(heading).ToContainTextAsync("About");
    }

    [Fact]
    public async Task AboutPage_DisplaysArchitectureSection()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var architectureSection = page.Locator("text=Architecture");
        await Assertions.Expect(architectureSection.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AboutPage_DisplaysTechnologyStack()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var content = await page.ContentAsync();
        Assert.Contains(".NET", content);
        Assert.Contains("Blazor", content);
    }

    [Fact]
    public async Task AboutPage_HasReaderBadge()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var readerBadge = page.Locator(".reader-badge, .reader-info");
        await Assertions.Expect(readerBadge.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AboutPage_HasNavigationBackToHome()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var homeLink = page.Locator("nav a[href='/']");
        await Assertions.Expect(homeLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AboutPage_DisplaysOverviewSection()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var overviewSection = page.Locator("text=Overview");
        await Assertions.Expect(overviewSection.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AboutPage_MentionsCleanArchitecture()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var content = await page.ContentAsync();
        Assert.Contains("Clean Architecture", content);
    }
}
```

```csharp src/MyBlog.E2E/Tests/AdminPageTests.cs
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class AdminPageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task AdminDashboard_DisplaysStatistics()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var statCard = page.Locator(".stat-card").Or(page.Locator(".dashboard-stats"));
        await Assertions.Expect(statCard.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_HasManagePostsLink()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var managePostsLink = page.Locator("a[href='/admin/posts']");
        await Assertions.Expect(managePostsLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_HasNewPostLink()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var newPostLink = page.Locator("a[href='/admin/posts/new']");
        await Assertions.Expect(newPostLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_HasManageImagesLink()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var manageImagesLink = page.Locator("a[href='/admin/images']");
        await Assertions.Expect(manageImagesLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminPosts_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/posts");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Posts", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminPosts_HasNewPostButton()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/posts");

        var newPostLink = page.Locator("a[href='/admin/posts/new']");
        await Assertions.Expect(newPostLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNewPost_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/posts/new");

        var titleInput = page.Locator("input[id='title'], input[name='title'], #title");
        await Assertions.Expect(titleInput.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNewPost_HasContentEditor()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/posts/new");

        var contentEditor = page.Locator("textarea[id='content'], textarea[name='content'], #content");
        await Assertions.Expect(contentEditor.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminImages_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        await page.GotoAsync("/admin/images");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Image", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminNavigation_CanNavigateBetweenPages()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var dashboardHeading = page.Locator("h1");
        await Assertions.Expect(dashboardHeading).ToContainTextAsync("Admin", new() { Timeout = 10000 });

        await page.ClickAsync("a[href='/admin/posts']");
        await page.WaitForURLAsync("**/admin/posts");

        var postsHeading = page.Locator("h1");
        await Assertions.Expect(postsHeading).ToContainTextAsync("Posts", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task AdminDashboard_ShowsRecentPosts()
    {
        var page = await _fixture.CreatePageAsync();
        await page.LoginAsAdminAsync();

        var recentPostsSection = page.GetByText("Recent Posts").Or(page.Locator("h2").Filter(new() { HasText = "Recent" }));
        await Assertions.Expect(recentPostsSection.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
```

```csharp src/MyBlog.E2E/Tests/AuthenticationTests.cs
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class AuthenticationTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task ProtectedRoute_RedirectsToLogin_WhenNotAuthenticated()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/admin");

        await page.WaitForURLAsync("**/login**", new() { Timeout = 10000 });

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Login");
    }

    [Fact]
    public async Task ProtectedRoute_IncludesReturnUrl_WhenRedirected()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/admin/posts");

        await page.WaitForURLAsync("**/login**", new() { Timeout = 10000 });

        Assert.Contains("returnUrl", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithDefaultCredentials_Succeeds()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        var adminHeading = page.Locator("h1");
        await Assertions.Expect(adminHeading).ToContainTextAsync("Admin", new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Login_ShowsAdminLink_WhenAuthenticated()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        await page.GotoAsync("/");

        var adminLink = page.Locator("nav a[href='/admin']");
        await Assertions.Expect(adminLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Logout_RedirectsToHome()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        var logoutButton = page.Locator("form[action='/logout'] button");
        await Assertions.Expect(logoutButton.First).ToBeVisibleAsync(new() { Timeout = 10000 });

        await Task.WhenAll(
            page.WaitForURLAsync("**/", new() { Timeout = 10000 }),
            logoutButton.First.ClickAsync()
        );

        var loginLink = page.Locator("nav a[href='/login']");
        await Assertions.Expect(loginLink).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Logout_RemovesAdminLink()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        var logoutButton = page.Locator("form[action='/logout'] button");

        await Task.WhenAll(
            page.WaitForURLAsync("**/", new() { Timeout = 10000 }),
            logoutButton.First.ClickAsync()
        );

        var adminLink = page.Locator("nav a[href='/admin']");
        await Assertions.Expect(adminLink).Not.ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.SubmitLoginAsync(BlazorPageExtensions.AdminUsername, "WrongPassword!", "**error=invalid**");

        await Assertions.Expect(page.Locator(".error-message")).ToHaveTextAsync("Invalid username or password");
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.SubmitLoginAsync("nonexistentuser", "SomePassword123!", "**error=invalid**");

        await Assertions.Expect(page.Locator(".error-message")).ToHaveTextAsync("Invalid username or password");
    }

    [Fact]
    public async Task AdminDashboard_LoadsSuccessfully_WhenAuthenticated()
    {
        var page = await _fixture.CreatePageAsync();

        await page.LoginAsAdminAsync();

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Admin");

        var managePostsLink = page.Locator("a[href='/admin/posts']");
        await Assertions.Expect(managePostsLink.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
```

```csharp src/MyBlog.E2E/Tests/HomePageTests.cs
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class HomePageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task HomePage_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();

        var response = await page.GotoAsync("/");

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected OK response, got {response.Status}");
    }

    [Fact]
    public async Task HomePage_DisplaysWelcomeHeading()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
        await Assertions.Expect(heading).ToContainTextAsync("Welcome");
    }

    [Fact]
    public async Task HomePage_HasFooter()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var footer = page.Locator("footer");
        await Assertions.Expect(footer).ToBeVisibleAsync();
    }
}
```

```csharp src/MyBlog.E2E/Tests/LoginPageTests.cs
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class LoginPageTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task LoginPage_LoadsSuccessfully()
    {
        var page = await _fixture.CreatePageAsync();

        var response = await page.GotoAsync("/login");

        Assert.NotNull(response);
        Assert.True(response.Ok, $"Expected OK response, got {response.Status}");
    }

    [Fact]
    public async Task LoginPage_DisplaysLoginHeading()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
        await Assertions.Expect(heading).ToContainTextAsync("Login");
    }

    [Fact]
    public async Task LoginPage_HasUsernameField()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var usernameInput = page.Locator("input[name='username']");
        await Assertions.Expect(usernameInput).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_HasPasswordField()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var passwordInput = page.Locator("input[name='password']");
        await Assertions.Expect(passwordInput).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_HasSubmitButton()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var submitButton = page.Locator("button[type='submit']");
        await Assertions.Expect(submitButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_FieldsAreRequired()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var usernameInput = page.Locator("input[name='username']");
        var passwordInput = page.Locator("input[name='password']");

        await Assertions.Expect(usernameInput).ToHaveAttributeAsync("required", "");
        await Assertions.Expect(passwordInput).ToHaveAttributeAsync("required", "");
    }

    [Fact]
    public async Task LoginPage_PasswordFieldIsTypePassword()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var passwordInput = page.Locator("input[name='password']");
        await Assertions.Expect(passwordInput).ToHaveAttributeAsync("type", "password");
    }

    [Fact]
    public async Task LoginPage_HasAntiforgeryToken()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var antiforgeryInput = page.Locator("input[name='__RequestVerificationToken']");
        await Assertions.Expect(antiforgeryInput).ToBeAttachedAsync();
    }

    [Fact]
    public async Task LoginPage_FormHasCorrectAction()
    {
        var page = await _fixture.CreatePageAsync();
        await page.GotoAsync("/login");

        var form = page.Locator("form[action='/account/login']");
        await Assertions.Expect(form).ToBeVisibleAsync();
    }
}
```

```csharp src/MyBlog.E2E/Tests/NavigationTests.cs
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class NavigationTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task Navigation_HomeToAbout_Works()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForBlazorInteractiveAsync();

        await page.ClickAsync("nav a[href='/about']");

        await page.WaitForURLAsync("**/about");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("About");
    }

    [Fact]
    public async Task Navigation_AboutToHome_Works()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");
        await page.WaitForBlazorInteractiveAsync();

        await page.ClickAsync("nav a[href='/']");

        await page.WaitForURLAsync("**/");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Welcome");
    }

    [Fact]
    public async Task Navigation_HomeToLogin_Works()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");
        await page.WaitForBlazorInteractiveAsync();

        await page.ClickAsync("nav a[href='/login']");

        await page.WaitForURLAsync("**/login");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Login");
    }

    [Fact]
    public async Task Navigation_AllMainLinks_Present()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var homeLink = page.Locator("nav a[href='/']");
        var aboutLink = page.Locator("nav a[href='/about']");
        var loginLink = page.Locator("nav a[href='/login']");

        await Assertions.Expect(homeLink).ToBeVisibleAsync();
        await Assertions.Expect(aboutLink).ToBeVisibleAsync();
        await Assertions.Expect(loginLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_FooterIsPresent_OnAllPages()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");
        var footer = page.Locator("footer.footer");
        await Assertions.Expect(footer).ToBeVisibleAsync();

        await page.GotoAsync("/about");
        await Assertions.Expect(footer).ToBeVisibleAsync();

        await page.GotoAsync("/login");
        await Assertions.Expect(footer).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_HeaderIsPresent_OnAllPages()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");
        var header = page.Locator("header, .header");
        await Assertions.Expect(header.First).ToBeVisibleAsync();

        await page.GotoAsync("/about");
        await Assertions.Expect(header.First).ToBeVisibleAsync();

        await page.GotoAsync("/login");
        await Assertions.Expect(header.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_LogoLinksToHome()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");
        await page.WaitForBlazorInteractiveAsync();

        await page.Locator("header a.logo").ClickAsync();
        await page.WaitForURLAsync("**/");

        var heading = page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync("Welcome");
    }

    [Fact]
    public async Task NonExistentPage_ShowsNotFound_OrRedirects()
    {
        var page = await _fixture.CreatePageAsync();

        var response = await page.GotoAsync("/this-page-does-not-exist-12345");

        Assert.NotNull(response);

        if (response.Status == 404)
        {
            Assert.Equal(404, response.Status);
        }
        else
        {
            var content = await page.ContentAsync();
            var hasNotFoundIndicator = content.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                                        content.Contains("404", StringComparison.OrdinalIgnoreCase);
            Assert.True(response.Ok || hasNotFoundIndicator);
        }
    }

    [Fact]
    public async Task PostNotFound_ShowsError()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/post/this-post-definitely-does-not-exist-12345");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var notFoundIndicator = page.GetByText("Not Found", new() { Exact = false });
        await Assertions.Expect(notFoundIndicator.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Navigation_MobileViewport_HasNavigation()
    {
        var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.BaseUrl,
            ViewportSize = new ViewportSize { Width = 375, Height = 667 }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync("/");

        var nav = page.Locator("nav");
        await Assertions.Expect(nav).ToBeAttachedAsync();
    }

    [Fact]
    public async Task Navigation_TabletViewport_HasNavigation()
    {
        var context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.BaseUrl,
            ViewportSize = new ViewportSize { Width = 768, Height = 1024 }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync("/");

        var nav = page.Locator("nav");
        await Assertions.Expect(nav).ToBeVisibleAsync();
    }
}
```

```csharp src/MyBlog.E2E/Tests/SeoTests.cs
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class SeoTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task HomePage_HasPageTitle()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var title = await page.TitleAsync();
        Assert.False(string.IsNullOrWhiteSpace(title), "Page should have a title");
    }

    [Fact]
    public async Task AboutPage_HasPageTitle()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var title = await page.TitleAsync();
        Assert.Contains("About", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginPage_HasPageTitle()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var title = await page.TitleAsync();
        Assert.Contains("Login", title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HomePage_HasMainLandmark()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var main = page.Locator("main, [role='main']");
        await Assertions.Expect(main.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task HomePage_HasNavigationLandmark()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var nav = page.Locator("nav, [role='navigation']");
        await Assertions.Expect(nav.First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AboutPage_HasProperHeadingHierarchy()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var h1Count = await page.Locator("h1").CountAsync();
        Assert.Equal(1, h1Count);
    }

    [Fact]
    public async Task Pages_HaveViewportMeta()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var viewportMeta = page.Locator("meta[name='viewport']");
        await Assertions.Expect(viewportMeta).ToBeAttachedAsync();
    }

    [Fact]
    public async Task Links_HaveVisibleText()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var homeLink = page.Locator("nav a[href='/']");
        var homeLinkText = await homeLink.TextContentAsync();
        Assert.False(string.IsNullOrWhiteSpace(homeLinkText), "Home link should have visible text");
    }

    [Fact]
    public async Task Buttons_HaveAccessibleNames()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToHaveAttributeAsync("aria-label", "Change theme");
    }

    [Fact]
    public async Task LoginForm_HasLabels()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/login");

        var usernameLabel = page.Locator("label[for='username']");
        var passwordLabel = page.Locator("label[for='password']");

        await Assertions.Expect(usernameLabel).ToBeVisibleAsync();
        await Assertions.Expect(passwordLabel).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Images_HaveAltText()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/about");

        var images = page.Locator("img");
        var imageCount = await images.CountAsync();

        for (int i = 0; i < imageCount; i++)
        {
            var img = images.Nth(i);
            var alt = await img.GetAttributeAsync("alt");
            Assert.NotNull(alt);
        }
    }

    [Fact]
    public async Task Page_HasLanguageAttribute()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var lang = await page.Locator("html").GetAttributeAsync("lang");
        Assert.False(string.IsNullOrWhiteSpace(lang), "HTML should have lang attribute");
    }
}
```

```csharp src/MyBlog.E2E/Tests/ThemeSwitcherTests.cs
using Microsoft.Playwright;
using Xunit;

namespace MyBlog.E2E.Tests;

[Collection(PlaywrightCollection.Name)]
public sealed class ThemeSwitcherTests(PlaywrightFixture fixture)
{
    private readonly PlaywrightFixture _fixture = fixture;

    [Fact]
    public async Task ThemeSwitcher_IsVisibleOnPage()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var themeSwitcher = page.Locator(".theme-switcher");
        await Assertions.Expect(themeSwitcher).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ThemeSwitcher_ButtonIsClickable()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await Assertions.Expect(themeSwitcherBtn).ToBeEnabledAsync();
    }

    [Fact]
    public async Task ThemeSwitcher_OpensMenuOnClick()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();

        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
    }

    [Fact]
    public async Task ThemeSwitcher_HasMultipleOptions()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        var options = page.Locator(".theme-option");
        var count = await options.CountAsync();

        Assert.True(count >= 2, $"Expected at least 2 theme options, got {count}");
    }

    [Fact]
    public async Task ThemeSwitcher_ChangesToDarkTheme()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        var darkOption = page.Locator(".theme-option:has-text('Dark')");
        await darkOption.ClickAsync();

        await page.WaitForFunctionAsync(
            "() => document.documentElement.getAttribute('data-theme') === 'dark'",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var newTheme = await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')");
        Assert.Equal("dark", newTheme);
    }

    [Fact]
    public async Task ThemeSwitcher_ThemePersistsOnRefresh()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        var darkOption = page.Locator(".theme-option:has-text('Dark')");
        await darkOption.ClickAsync();

        await page.WaitForFunctionAsync(
            "() => document.documentElement.getAttribute('data-theme') === 'dark'",
            null,
            new PageWaitForFunctionOptions { Timeout = 5000 });

        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        var themeAfterRefresh = await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')");
        Assert.Equal("dark", themeAfterRefresh);
    }

    [Fact]
    public async Task ThemeSwitcher_HasAccessibilityAttributes()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();

        await Assertions.Expect(themeSwitcherBtn).ToHaveAttributeAsync("aria-label", "Change theme");
        await Assertions.Expect(themeSwitcherBtn).ToHaveAttributeAsync("aria-haspopup", "true");
    }

    [Fact]
    public async Task ThemeSwitcher_MenuClosesOnOutsideClick()
    {
        var page = await _fixture.CreatePageAsync();

        await page.GotoAsync("/");

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var themeSwitcherBtn = page.Locator(".theme-switcher-btn");
        await Assertions.Expect(themeSwitcherBtn).ToBeVisibleAsync();
        await themeSwitcherBtn.ClickAsync();

        var themeMenu = page.Locator(".theme-menu.open");
        await Assertions.Expect(themeMenu).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });

        await page.Locator("main, .main").First.ClickAsync();

        await Assertions.Expect(themeMenu).Not.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5000 });
    }
}
```

```csharp src/MyBlog.Infrastructure/Data/BlogDbContext.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;

namespace MyBlog.Infrastructure.Data;

public sealed class BlogDbContext : DbContext
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<TelemetryLog> TelemetryLogs => Set<TelemetryLog>();
    public DbSet<ImageDimensionCache> ImageDimensionCache => Set<ImageDimensionCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Summary).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.Author)
                .WithMany(u => u.Posts)
                .HasForeignKey(e => e.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.HasOne(e => e.Post)
                .WithMany(p => p.Images)
                .HasForeignKey(e => e.PostId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.UploadedBy)
                .WithMany(u => u.UploadedImages)
                .HasForeignKey(e => e.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TelemetryLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Level).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.TimestampUtc);
        });

        modelBuilder.Entity<ImageDimensionCache>(entity =>
        {
            entity.HasKey(e => e.Url);
            entity.Property(e => e.Url).HasMaxLength(2048);
        });
    }
}
```

```csharp src/MyBlog.Infrastructure/Data/DatabasePathResolver.cs
namespace MyBlog.Infrastructure.Data;

public static class DatabasePathResolver
{
    public static string GetDatabasePath()
    {
        var dataDir = GetDataDirectory();
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "myblog.db");
    }

    public static string GetDataDirectory()
    {
        var preferredDir = GetPreferredDataDirectory();
        try
        {
            Directory.CreateDirectory(preferredDir);
            var testFile = Path.Combine(preferredDir, ".write-test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return preferredDir;
        }
        catch
        {
            return Path.Combine(AppContext.BaseDirectory, "data");
        }
    }

    internal static string GetPreferredDataDirectory()
    {
        string baseDir;

        if (OperatingSystem.IsWindows())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else if (OperatingSystem.IsMacOS())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            baseDir = !string.IsNullOrEmpty(xdgDataHome)
                ? xdgDataHome
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(baseDir, "MyBlog");
    }
}
```

```csharp src/MyBlog.Infrastructure/Data/DatabaseSchemaUpdater.cs
using Microsoft.EntityFrameworkCore;

namespace MyBlog.Infrastructure.Data;

public static class DatabaseSchemaUpdater
{
    public static async Task ApplyUpdatesAsync(BlogDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await EnsureImageDimensionCacheTableAsync(db);
    }

    private static async Task EnsureImageDimensionCacheTableAsync(BlogDbContext db)
    {
        var tableExists = await TableExistsAsync(db, "ImageDimensionCache");
        if (!tableExists)
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "ImageDimensionCache" (
                    "Url" TEXT NOT NULL CONSTRAINT "PK_ImageDimensionCache" PRIMARY KEY,
                    "Width" INTEGER NOT NULL,
                    "Height" INTEGER NOT NULL,
                    "LastCheckedUtc" TEXT NOT NULL
                )
                """);
        }
    }

    private static async Task<bool> TableExistsAsync(BlogDbContext db, string tableName)
    {
        var connection = db.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result) > 0;
    }
}
```

```xml src/MyBlog.Infrastructure/MyBlog.Infrastructure.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="OpenTelemetry" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="MyBlog.Tests" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyBlog.Core\MyBlog.Core.csproj" />
  </ItemGroup>
</Project>
```

```csharp src/MyBlog.Infrastructure/Repositories/ImageRepository.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

public sealed class ImageRepository : IImageRepository
{
    private readonly BlogDbContext _context;

    public ImageRepository(BlogDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Image>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Images
            .AsNoTracking()
            .OrderByDescending(i => i.UploadedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => await _context.Images.FindAsync([id], cancellationToken);

    public async Task<Image> CreateAsync(Image image, CancellationToken cancellationToken = default)
    {
        _context.Images.Add(image);
        await _context.SaveChangesAsync(cancellationToken);
        return image;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var image = await _context.Images.FindAsync([id], cancellationToken);
        if (image is not null)
        {
            _context.Images.Remove(image);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
```

```csharp src/MyBlog.Infrastructure/Repositories/PostRepository.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

public sealed class PostRepository : IPostRepository
{
    private readonly BlogDbContext _context;

    public PostRepository(BlogDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<PostListItemDto> Posts, int TotalCount)> GetPublishedPostsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Posts
            .AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var posts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Author)
            .Select(p => new PostListItemDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Summary,
                p.Author!.DisplayName,
                p.PublishedAtUtc,
                p.IsPublished))
            .ToListAsync(cancellationToken);

        return (posts, totalCount);
    }

    public async Task<IReadOnlyList<PostListItemDto>> GetAllPostsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Posts
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Include(p => p.Author)
            .Select(p => new PostListItemDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Summary,
                p.Author!.DisplayName,
                p.PublishedAtUtc,
                p.IsPublished))
            .ToListAsync(cancellationToken);

    public async Task<PostDetailDto?> GetBySlugAsync(
        string slug, CancellationToken cancellationToken = default) =>
        await _context.Posts
            .AsNoTracking()
            .Where(p => p.Slug == slug)
            .Include(p => p.Author)
            .Select(p => new PostDetailDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Content,
                p.Summary,
                p.Author!.DisplayName,
                p.CreatedAtUtc,
                p.UpdatedAtUtc,
                p.PublishedAtUtc,
                p.IsPublished))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Post?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _context.Posts
            .Include(p => p.Author)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Post> CreateAsync(Post post, CancellationToken cancellationToken = default)
    {
        _context.Posts.Add(post);
        await _context.SaveChangesAsync(cancellationToken);
        return post;
    }

    public async Task UpdateAsync(Post post, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(post).State == EntityState.Detached)
        {
            _context.Posts.Attach(post);
            _context.Entry(post).State = EntityState.Modified;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await _context.Posts.FindAsync([id], cancellationToken);
        if (post is not null)
        {
            _context.Posts.Remove(post);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default) => await _context.Posts.CountAsync(cancellationToken);

    public async Task<int> GetPublishedCountAsync(CancellationToken cancellationToken = default) =>
        await _context.Posts.CountAsync(p => p.IsPublished, cancellationToken);

    public async Task<IReadOnlyList<PostListItemDto>> GetRecentPostsAsync(
        int count, CancellationToken cancellationToken = default) =>
        await _context.Posts
            .AsNoTracking()
            .OrderByDescending(p => p.UpdatedAtUtc)
            .Take(count)
            .Include(p => p.Author)
            .Select(p => new PostListItemDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Summary,
                p.Author!.DisplayName,
                p.PublishedAtUtc,
                p.IsPublished))
            .ToListAsync(cancellationToken);

    public async Task<bool> IsSlugTakenAsync(string slug, Guid? excludePostId = null, CancellationToken cancellationToken = default)
    {
        if (excludePostId.HasValue)
        {
            return await _context.Posts
                .AnyAsync(p => p.Slug == slug && p.Id != excludePostId.Value, cancellationToken);
        }

        return await _context.Posts
            .AnyAsync(p => p.Slug == slug, cancellationToken);
    }
}
```

```csharp src/MyBlog.Infrastructure/Repositories/TelemetryLogRepository.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

public sealed class TelemetryLogRepository : ITelemetryLogRepository
{
    private readonly BlogDbContext _context;

    public TelemetryLogRepository(BlogDbContext context)
    {
        _context = context;
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        await _context.TelemetryLogs
            .Where(l => l.TimestampUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<int> TrimToNewestAsync(int maxRows, CancellationToken cancellationToken = default)
    {
        var threshold = await _context.TelemetryLogs
            .OrderByDescending(l => l.TimestampUtc)
            .Skip(maxRows)
            .Select(l => (DateTime?)l.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (threshold is null)
        {
            return 0;
        }

        return await _context.TelemetryLogs
            .Where(l => l.TimestampUtc <= threshold.Value)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
```

```csharp src/MyBlog.Infrastructure/Repositories/UserRepository.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

public sealed class UserRepository(BlogDbContext context) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => await context.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == username.ToLower(), cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Users.OrderBy(u => u.Username).ToListAsync(cancellationToken);

    public async Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
        => await context.Users.AnyAsync(cancellationToken);

    public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.FindAsync([id], cancellationToken);
        if (user is not null)
        {
            context.Users.Remove(user);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
```

```csharp src/MyBlog.Infrastructure/ServiceCollectionExtensions.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Core.Services;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;

namespace MyBlog.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString) || connectionString == "Data Source=myblog.db")
        {
            var dbPath = DatabasePathResolver.GetDatabasePath();
            connectionString = $"Data Source={dbPath}";
        }

        services.AddDbContext<BlogDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IImageRepository, ImageRepository>();
        services.AddScoped<ITelemetryLogRepository, TelemetryLogRepository>();

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ISlugService, SlugService>();

        services.AddScoped<IMarkdownService, MarkdownService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddSingleton<IReaderTrackingService, ReaderTrackingService>();

        services.AddHttpClient<IImageDimensionService, ImageDimensionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("MyBlog/1.0");
        });

        services.AddHostedService<TelemetryCleanupService>();
        services.AddHostedService<ImageCacheWarmerService>();

        return services;
    }
}
```

```csharp src/MyBlog.Infrastructure/Services/AuthService.cs
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;

namespace MyBlog.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _configuration = configuration;
    }

    public async Task<User?> AuthenticateAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return _passwordService.VerifyPassword(user.PasswordHash, password) ? user : null;
    }

    public async Task EnsureAdminUserAsync(CancellationToken cancellationToken = default)
    {
        if (await _userRepository.AnyUsersExistAsync(cancellationToken))
        {
            return;
        }

        var defaultPassword = Environment.GetEnvironmentVariable("MYBLOG_ADMIN_PASSWORD")
            ?? _configuration["Authentication:DefaultAdminPassword"]
            ?? "ChangeMe123!";

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            PasswordHash = _passwordService.HashPassword(defaultPassword),
            Email = "admin@localhost",
            DisplayName = "Administrator",
            CreatedAtUtc = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(admin, cancellationToken);
    }

    public async Task<bool> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (!_passwordService.VerifyPassword(user.PasswordHash, currentPassword))
        {
            return false;
        }

        user.PasswordHash = _passwordService.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user, cancellationToken);

        return true;
    }
}
```

```csharp src/MyBlog.Infrastructure/Services/ImageCacheWarmerService.cs
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Services;

public sealed class ImageCacheWarmerService(
    IServiceScopeFactory scopeFactory,
    ILogger<ImageCacheWarmerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        logger.LogInformation("Image Cache Warmer started. Scanning posts for uncached images...");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var dimService = scope.ServiceProvider.GetRequiredService<IImageDimensionService>();

            var regex = new Regex(@"!\[([^\]]*)\]\(([^)]+)\)");
            var distinctUrls = new HashSet<string>();

            await foreach (var content in db.Posts
                .Select(p => p.Content)
                .AsAsyncEnumerable()
                .WithCancellation(stoppingToken))
            {
                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }

                var matches = regex.Matches(content);
                foreach (Match match in matches)
                {
                    distinctUrls.Add(match.Groups[2].Value);
                }
            }

            HashSet<string> existingSet;
            try
            {
                var existingCache = await db.ImageDimensionCache
                    .Select(x => x.Url)
                    .ToListAsync(stoppingToken);
                existingSet = new HashSet<string>(existingCache);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Image Cache Warmer: Could not read existing cache. Will attempt to cache all URLs.");
                existingSet = new HashSet<string>();
            }

            var missingUrls = distinctUrls.Where(u => !existingSet.Contains(u)).ToList();

            if (missingUrls.Count == 0)
            {
                logger.LogInformation("Image Cache Warmer: All images are already cached.");
                return;
            }

            logger.LogInformation("Image Cache Warmer: Found {Count} uncached images. Fetching dimensions...", missingUrls.Count);

            var successCount = 0;
            var failCount = 0;

            foreach (var url in missingUrls.TakeWhile(_ => !stoppingToken.IsCancellationRequested))
            {
                try
                {
                    var dimensions = await dimService.GetDimensionsAsync(url, stoppingToken);
                    if (dimensions.HasValue)
                    {
                        logger.LogDebug("Cached dimensions for: {Url} ({Width}x{Height})",
                            url, dimensions.Value.Width, dimensions.Value.Height);
                        successCount++;
                    }
                    else
                    {
                        logger.LogDebug("Could not resolve dimensions for: {Url}", url);
                        failCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Error warming cache for {Url}", url);
                    failCount++;
                }

                await Task.Delay(100, stoppingToken);
            }

            logger.LogInformation("Image Cache Warmer completed. Cached: {Success}, Failed: {Failed}",
                successCount, failCount);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Image Cache Warmer was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Image Cache Warmer. This is non-fatal - the blog will continue to work without cached dimensions.");
        }
    }
}
```

```csharp src/MyBlog.Infrastructure/Services/ImageDimensionService.cs
using System.Buffers.Binary;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Services;

public sealed class ImageDimensionService : IImageDimensionService
{
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImageDimensionService> _logger;

    public ImageDimensionService(
        HttpClient httpClient,
        IServiceScopeFactory scopeFactory,
        ILogger<ImageDimensionService> logger)
    {
        _httpClient = httpClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
            var cached = await db.ImageDimensionCache.FindAsync([url], cancellationToken);
            if (cached != null)
            {
                return (cached.Width, cached.Height);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check image dimension cache for {Url}. Continuing without cache.", url);
        }

        try
        {
            var dimensions = await FetchDimensionsFromNetworkAsync(url, cancellationToken);

            if (dimensions.HasValue)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();

                    if (!await db.ImageDimensionCache.AnyAsync(x => x.Url == url, cancellationToken))
                    {
                        db.ImageDimensionCache.Add(new ImageDimensionCache
                        {
                            Url = url,
                            Width = dimensions.Value.Width,
                            Height = dimensions.Value.Height,
                            LastCheckedUtc = DateTime.UtcNow
                        });
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache image dimensions for {Url}. Dimensions were resolved but not cached.", url);
                }

                return dimensions;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve dimensions for {Url}", url);
        }

        return null;
    }

    private async Task<(int Width, int Height)?> FetchDimensionsFromNetworkAsync(string url, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        var buffer = new byte[32];
        var bytesRead = await ReadFullyAsync(stream, buffer, ct);

        if (bytesRead < 8)
        {
            return null;
        }

        if (IsPng(buffer))
        {
            if (bytesRead >= 24)
            {
                var width = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(16, 4));
                var height = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(20, 4));
                return (width, height);
            }
        }
        else if (IsGif(buffer))
        {
            if (bytesRead >= 10)
            {
                var width = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(6, 2));
                var height = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(8, 2));
                return (width, height);
            }
        }
        else if (IsJpeg(buffer))
        {
            return await ParseJpegDimensionsAsync(stream, buffer, bytesRead, ct);
        }
        else if (IsWebP(buffer))
        {
            return ParseWebPDimensions(buffer, bytesRead);
        }

        return null;
    }

    private static async Task<int> ReadFullyAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], ct);
            if (read == 0)
            {
                break;
            }
            totalRead += read;
        }
        return totalRead;
    }

    private static bool IsPng(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 8 &&
        buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47 &&
        buffer[4] == 0x0D && buffer[5] == 0x0A && buffer[6] == 0x1A && buffer[7] == 0x0A;

    private static bool IsGif(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 6 &&
        buffer[0] == 0x47 && buffer[1] == 0x49 && buffer[2] == 0x46 &&
        buffer[3] == 0x38 && (buffer[4] == 0x39 || buffer[4] == 0x37) && buffer[5] == 0x61;

    private static bool IsJpeg(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 2 &&
        buffer[0] == 0xFF && buffer[1] == 0xD8;

    private static bool IsWebP(ReadOnlySpan<byte> buffer) =>
        buffer.Length >= 12 &&
        buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
        buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50;

    private static async Task<(int Width, int Height)?> ParseJpegDimensionsAsync(
        Stream stream, byte[] initialBuffer, int initialBytesRead, CancellationToken ct)
    {
        var position = 2;
        var single = new byte[1];

        async Task<int> ReadByteAsync()
        {
            if (position < initialBytesRead)
            {
                return initialBuffer[position++];
            }

            var read = await stream.ReadAsync(single.AsMemory(0, 1), ct);
            if (read == 0)
            {
                return -1;
            }
            position++;
            return single[0];
        }

        async Task<byte[]?> ReadBytesAsync(int count)
        {
            var result = new byte[count];
            var resultPos = 0;

            while (resultPos < count && position < initialBytesRead)
            {
                result[resultPos++] = initialBuffer[position++];
            }

            if (resultPos < count)
            {
                var remaining = count - resultPos;
                var read = await ReadFullyAsync(stream, result.AsMemory(resultPos, remaining), ct);
                if (read < remaining)
                {
                    return null;
                }
                position += read;
            }

            return result;
        }

        while (true)
        {
            var ff = await ReadByteAsync();
            if (ff == -1)
            {
                return null;
            }

            if (ff != 0xFF)
            {
                continue;
            }

            int marker;
            do
            {
                marker = await ReadByteAsync();
                if (marker == -1)
                {
                    return null;
                }
            } while (marker == 0xFF);

            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                var sofData = await ReadBytesAsync(7);
                if (sofData == null)
                {
                    return null;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(sofData.AsSpan(3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(sofData.AsSpan(5, 2));
                return (width, height);
            }

            if (marker == 0xD9)
            {
                return null;
            }

            if (marker == 0xDA)
            {
                return null;
            }

            if ((marker >= 0xD0 && marker <= 0xD7) || marker == 0x00 || marker == 0x01)
            {
                continue;
            }

            var lengthBytes = await ReadBytesAsync(2);
            if (lengthBytes == null)
            {
                return null;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes) - 2;
            if (length > 0)
            {
                if (position < initialBytesRead)
                {
                    var skipFromBuffer = Math.Min(length, initialBytesRead - position);
                    position += skipFromBuffer;
                    length -= skipFromBuffer;
                }

                if (length > 0)
                {
                    var skipBuffer = new byte[Math.Min(length, 8192)];
                    var remaining = length;
                    while (remaining > 0)
                    {
                        var toRead = Math.Min(remaining, skipBuffer.Length);
                        var read = await ReadFullyAsync(stream, skipBuffer.AsMemory(0, toRead), ct);
                        if (read == 0)
                        {
                            return null;
                        }
                        remaining -= read;
                        position += read;
                    }
                }
            }
        }
    }

    private static (int Width, int Height)? ParseWebPDimensions(byte[] buffer, int bytesRead)
    {
        if (bytesRead < 30)
        {
            return null;
        }

        var chunkType = Encoding.ASCII.GetString(buffer, 12, 4);

        switch (chunkType)
        {
            case "VP8 ":
                {
                    if (buffer[23] == 0x9D && buffer[24] == 0x01 && buffer[25] == 0x2A)
                    {
                        var width = (buffer[26] | (buffer[27] << 8)) & 0x3FFF;
                        var height = (buffer[28] | (buffer[29] << 8)) & 0x3FFF;
                        return (width, height);
                    }
                    break;
                }
            case "VP8L":
                {
                    if (buffer[20] == 0x2F)
                    {
                        var b0 = buffer[21];
                        var b1 = buffer[22];
                        var b2 = buffer[23];
                        var b3 = buffer[24];

                        var width = ((b0 | (b1 << 8)) & 0x3FFF) + 1;
                        var height = (((b1 >> 6) | (b2 << 2) | (b3 << 10)) & 0x3FFF) + 1;
                        return (width, height);
                    }
                    break;
                }
            case "VP8X":
                {
                    var width = (buffer[24] | (buffer[25] << 8) | (buffer[26] << 16)) + 1;
                    var height = (buffer[27] | (buffer[28] << 8) | (buffer[29] << 16)) + 1;
                    return (width, height);
                }
        }

        return null;
    }
}
```

```csharp src/MyBlog.Infrastructure/Services/PasswordService.cs
using Microsoft.AspNetCore.Identity;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;

namespace MyBlog.Infrastructure.Services;

public sealed class PasswordService : IPasswordService
{
    private readonly IPasswordHasher<User> _hasher;

    public PasswordService(IPasswordHasher<User> hasher)
    {
        _hasher = hasher;
    }

    public string HashPassword(string password) => _hasher.HashPassword(null!, password);

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(null!, hashedPassword, providedPassword);
        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
```

```csharp src/MyBlog.Infrastructure/Services/ReaderTrackingService.cs
using System.Collections.Concurrent;
using MyBlog.Core.Interfaces;

namespace MyBlog.Infrastructure.Services;

public class ReaderTrackingService : IReaderTrackingService
{
    private readonly ConcurrentDictionary<string, int> _slugCounts = new();

    private readonly ConcurrentDictionary<string, string> _connectionMap = new();

    public int JoinPost(string slug, string connectionId)
    {
        _connectionMap.AddOrUpdate(connectionId, slug, (_, _) => slug);

        return _slugCounts.AddOrUpdate(slug, 1, (_, count) => count + 1);
    }

    public int LeavePost(string slug, string connectionId)
    {
        _connectionMap.TryRemove(connectionId, out _);

        return _slugCounts.AddOrUpdate(slug, 0, (_, count) => count > 0 ? count - 1 : 0);
    }

    public (string? Slug, int NewCount) Disconnect(string connectionId)
    {
        if (_connectionMap.TryRemove(connectionId, out var slug))
        {
            var newCount = _slugCounts.AddOrUpdate(slug, 0, (_, count) => count > 0 ? count - 1 : 0);
            return (slug, newCount);
        }

        return (null, 0);
    }

    public int GetReaderCount(string slug) => _slugCounts.TryGetValue(slug, out var count) ? count : 0;
}
```

```csharp src/MyBlog.Infrastructure/Services/TelemetryCleanupService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure.Telemetry;

namespace MyBlog.Infrastructure.Services;

public sealed class TelemetryCleanupService : BackgroundService
{
    internal const int MaxRetentionDays = 30;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryCleanupService> _logger;
    private readonly int _retentionDays;
    private readonly int _maxDatabaseRows;
    private readonly long _maxLogFileBytes;
    private readonly string? _logsDirectory;

    public TelemetryCleanupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<TelemetryCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retentionDays = Math.Clamp(configuration.GetValue("Telemetry:RetentionDays", MaxRetentionDays), 1, MaxRetentionDays);
        _maxDatabaseRows = Math.Max(1, configuration.GetValue("Telemetry:MaxDatabaseRows", 50_000));
        _maxLogFileBytes = Math.Max(1, configuration.GetValue("Telemetry:MaxLogFileMegabytes", 100)) * 1024L * 1024L;
        _logsDirectory = TelemetryPathResolver.GetLogsDirectory();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupAsync(stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITelemetryLogRepository>();

            var expired = await repository.DeleteOlderThanAsync(cutoff, cancellationToken);
            var trimmed = await repository.TrimToNewestAsync(_maxDatabaseRows, cancellationToken);

            if (expired + trimmed > 0)
            {
                _logger.LogInformation(
                    "Telemetry cleanup: deleted {Expired} database logs older than {Days} days and {Trimmed} beyond {MaxRows} rows",
                    expired, _retentionDays, trimmed, _maxDatabaseRows);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Telemetry cleanup failed for database logs");
        }

        if (_logsDirectory is null)
        {
            return;
        }

        try
        {
            var (deleted, failed) = LogFileRetention.Apply(_logsDirectory, cutoff, _maxLogFileBytes);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Telemetry cleanup: deleted {Count} log files from {Directory}", deleted, _logsDirectory);
            }

            if (failed > 0)
            {
                _logger.LogWarning(
                    "Telemetry cleanup: could not delete {Count} log files in {Directory}", failed, _logsDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry cleanup failed for log files in {Directory}", _logsDirectory);
        }
    }
}
```

```csharp src/MyBlog.Infrastructure/Telemetry/DatabaseLogExporter.cs
using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyBlog.Infrastructure.Telemetry;

public sealed class DatabaseLogExporter : BaseExporter<LogRecord>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseLogExporter(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();

            foreach (var record in batch)
            {
                var log = new TelemetryLog
                {
                    TimestampUtc = record.Timestamp.ToUniversalTime(),
                    Level = record.LogLevel.ToString(),
                    Category = record.CategoryName ?? "Unknown",
                    Message = record.FormattedMessage ?? record.Body ?? "",
                    Exception = record.Exception?.ToString(),
                    TraceId = record.TraceId.ToString(),
                    SpanId = record.SpanId.ToString(),
                    Properties = SerializeAttributes(record.Attributes)
                };

                context.TelemetryLogs.Add(log);
            }

            context.SaveChanges();
            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    internal static string? SerializeAttributes(IReadOnlyList<KeyValuePair<string, object?>>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return null;
        }

        var values = new Dictionary<string, object?>(attributes.Count);
        foreach (var attribute in attributes)
        {
            values[attribute.Key] = attribute.Value;
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in values)
            {
                writer.WritePropertyName(key);
                WriteValue(writer, value);
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case short sh:
                writer.WriteNumberValue(sh);
                break;
            case byte by:
                writer.WriteNumberValue(by);
                break;
            case sbyte sb:
                writer.WriteNumberValue(sb);
                break;
            case ushort us:
                writer.WriteNumberValue(us);
                break;
            case uint ui:
                writer.WriteNumberValue(ui);
                break;
            case ulong ul:
                writer.WriteNumberValue(ul);
                break;
            case decimal m:
                writer.WriteNumberValue(m);
                break;
            case double d when double.IsFinite(d):
                writer.WriteNumberValue(d);
                break;
            case float f when float.IsFinite(f):
                writer.WriteNumberValue(f);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt);
                break;
            case DateTimeOffset dto:
                writer.WriteStringValue(dto);
                break;
            case Guid g:
                writer.WriteStringValue(g);
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
```

```csharp src/MyBlog.Infrastructure/Telemetry/FileLogEntry.cs
namespace MyBlog.Infrastructure.Telemetry;

internal sealed record FileLogEntry(
    string Timestamp,
    string Level,
    string? Category,
    string? Message,
    string TraceId,
    string SpanId,
    string? Exception);
```

```csharp src/MyBlog.Infrastructure/Telemetry/FileLogExporter.cs
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyBlog.Infrastructure.Telemetry;

public sealed class FileLogExporter : BaseExporter<LogRecord>
{
    private readonly string _directory;
    private readonly string _runId;
    private readonly long _maxFileSizeBytes;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private long _currentFileSize;
    private int _fileNumber;
    private bool _isFirstRecord = true;

    public FileLogExporter(string directory, long maxFileSizeBytes = 25 * 1024 * 1024)
    {
        _directory = directory;
        _maxFileSizeBytes = maxFileSizeBytes;
        _runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(_directory);
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        try
        {
            lock (_lock)
            {
                EnsureWriter();

                foreach (var record in batch)
                {
                    var entry = new FileLogEntry(
                        record.Timestamp.ToString("O"),
                        record.LogLevel.ToString(),
                        record.CategoryName,
                        record.FormattedMessage ?? record.Body,
                        record.TraceId.ToString(),
                        record.SpanId.ToString(),
                        record.Exception?.ToString());

                    var json = JsonSerializer.Serialize(entry, TelemetryJsonContext.Default.FileLogEntry);
                    var bytes = Encoding.UTF8.GetByteCount(json) + 2;

                    if (_currentFileSize + bytes > _maxFileSizeBytes)
                    {
                        RotateFile();
                    }

                    if (!_isFirstRecord)
                    {
                        _writer!.WriteLine(",");
                    }
                    else
                    {
                        _isFirstRecord = false;
                    }

                    _writer!.Write(json);
                    _currentFileSize += bytes;
                }

                _writer!.Flush();
            }

            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    private void EnsureWriter()
    {
        if (_writer is null)
        {
            OpenNewFile();
        }
    }

    private void OpenNewFile()
    {
        var fileName = _fileNumber == 0
            ? $"logs_{_runId}.json"
            : $"logs_{_runId}_{_fileNumber:D3}.json";

        _writer = new StreamWriter(Path.Combine(_directory, fileName), false, Encoding.UTF8);
        _writer.WriteLine("[");
        _currentFileSize = 2;
        _isFirstRecord = true;
    }

    private void RotateFile()
    {
        CloseWriter();
        _fileNumber++;
        OpenNewFile();
    }

    private void CloseWriter()
    {
        if (_writer is not null)
        {
            _writer.WriteLine();
            _writer.WriteLine("]");
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        lock (_lock)
        {
            CloseWriter();
        }
        return true;
    }
}
```

```csharp src/MyBlog.Infrastructure/Telemetry/LogFileRetention.cs
namespace MyBlog.Infrastructure.Telemetry;

internal static class LogFileRetention
{
    internal const string FilePattern = "logs_*.json";

    internal static (int Deleted, int Failed) Apply(string directory, DateTime cutoffUtc, long maxTotalBytes)
    {
        if (!Directory.Exists(directory))
        {
            return (0, 0);
        }

        var files = new DirectoryInfo(directory)
            .GetFiles(FilePattern)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();

        var deleted = 0;
        var failed = 0;
        long keptBytes = 0;

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var expired = file.LastWriteTimeUtc < cutoffUtc;
            var overBudget = i > 0 && keptBytes + file.Length > maxTotalBytes;

            if (!expired && !overBudget)
            {
                keptBytes += file.Length;
                continue;
            }

            try
            {
                file.Delete();
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;
            }
        }

        return (deleted, failed);
    }
}
```

```csharp src/MyBlog.Infrastructure/Telemetry/TelemetryJsonContext.cs
using System.Text.Json.Serialization;

namespace MyBlog.Infrastructure.Telemetry;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(FileLogEntry))]
internal partial class TelemetryJsonContext : JsonSerializerContext
{
}
```

```csharp src/MyBlog.Infrastructure/Telemetry/TelemetryLoggingExtensions.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyBlog.Infrastructure.Telemetry;

public static class TelemetryLoggingExtensions
{
    public static OpenTelemetryLoggerOptions AddLocalExporters(
        this OpenTelemetryLoggerOptions options,
        IConfiguration configuration)
    {
        if (configuration.GetValue("Telemetry:EnableFileLogging", true))
        {
            var logsDirectory = TelemetryPathResolver.GetLogsDirectory();
            if (logsDirectory is not null)
            {
                options.AddProcessor(_ => new BatchLogRecordExportProcessor(new FileLogExporter(logsDirectory)));
            }
        }

        if (configuration.GetValue("Telemetry:EnableDatabaseLogging", true))
        {
            options.AddProcessor(sp => new BatchLogRecordExportProcessor(
                new DatabaseLogExporter(sp.GetRequiredService<IServiceScopeFactory>())));
        }

        return options;
    }
}
```

```csharp src/MyBlog.Infrastructure/Telemetry/TelemetryPathResolver.cs
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Telemetry;

public static class TelemetryPathResolver
{
    public static string? GetLogsDirectory()
    {
        var preferredDir = Path.Combine(DatabasePathResolver.GetPreferredDataDirectory(), "telemetry", "logs");
        if (TryCreateAndVerify(preferredDir))
        {
            return preferredDir;
        }

        var localDir = Path.Combine(AppContext.BaseDirectory, "telemetry", "logs");
        if (TryCreateAndVerify(localDir))
        {
            return localDir;
        }

        return null;
    }

    private static bool TryCreateAndVerify(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var testFile = Path.Combine(path, $".write-test-{Guid.NewGuid()}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

```xml src/MyBlog.slnx
<Solution>
  <Project Path="MyBlog.Core/MyBlog.Core.csproj" />
  <Project Path="MyBlog.Infrastructure/MyBlog.Infrastructure.csproj" />
  <Project Path="MyBlog.Web/MyBlog.Web.csproj" />
  <Project Path="MyBlog.Tests/MyBlog.Tests.csproj" />
  <Project Path="MyBlog.E2E/MyBlog.E2E.csproj" />
</Solution>
```

```csharp src/MyBlog.Tests/Integration/AuthServiceLongPasswordTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Integration;

public sealed class AuthServiceLongPasswordTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly AuthService _sut;
    private readonly PasswordService _passwordService;

    public AuthServiceLongPasswordTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _passwordService = new PasswordService(new PasswordHasher<User>());
        var userRepository = new UserRepository(_context);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DefaultAdminPassword"] = "TestAdmin123!"
            })
            .Build();

        _sut = new AuthService(userRepository, _passwordService, configuration);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task AuthenticateAsync_With128CharacterPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var longPassword = new string('a', 128);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "long-pass-user",
            PasswordHash = _passwordService.HashPassword(longPassword),
            Email = "longpass@example.com",
            DisplayName = "Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("long-pass-user", longPassword, ct);

        Assert.NotNull(result);
        Assert.Equal("long-pass-user", result.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_With256CharacterPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var longPassword = new string('x', 256);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "very-long-pass-user",
            PasswordHash = _passwordService.HashPassword(longPassword),
            Email = "very-long@example.com",
            DisplayName = "Very Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("very-long-pass-user", longPassword, ct);

        Assert.NotNull(result);
        Assert.Equal("very-long-pass-user", result.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_With512CharacterPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var longPassword = new string('P', 512);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "extralong-pass-user",
            PasswordHash = _passwordService.HashPassword(longPassword),
            Email = "extralong@example.com",
            DisplayName = "Extra Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("extralong-pass-user", longPassword, ct);

        Assert.NotNull(result);
        Assert.Equal("extralong-pass-user", result.Username);
    }

    [Fact]
    public async Task ChangePasswordAsync_With128CharacterNewPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalPassword = "ShortPass123!";
        var newLongPassword = new string('N', 128);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "change-pass-long-user",
            PasswordHash = _passwordService.HashPassword(originalPassword),
            Email = "changelong@example.com",
            DisplayName = "Change Long Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.ChangePasswordAsync(user.Id, originalPassword, newLongPassword, ct);

        Assert.True(result);

        var authenticated = await _sut.AuthenticateAsync("change-pass-long-user", newLongPassword, ct);
        Assert.NotNull(authenticated);
    }

    [Fact]
    public async Task AuthenticateAsync_WithComplexLongPassword_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var complexPassword = string.Concat(
            new string('A', 50),
            new string('1', 50),
            new string('!', 50),
            new string('z', 50)
        );

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "complex-pass-user",
            PasswordHash = _passwordService.HashPassword(complexPassword),
            Email = "complex@example.com",
            DisplayName = "Complex Password User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("complex-pass-user", complexPassword, ct);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task AuthenticateAsync_After100FailedAttempts_StillAllowsLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPassword123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "no-lockout-user",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "no-lockout@example.com",
            DisplayName = "No Lockout User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        for (var i = 0; i < 100; i++)
        {
            var failedResult = await _sut.AuthenticateAsync("no-lockout-user", "WrongPassword", ct);
            Assert.Null(failedResult);
        }

        var successResult = await _sut.AuthenticateAsync("no-lockout-user", correctPassword, ct);
        Assert.NotNull(successResult);
        Assert.Equal("no-lockout-user", successResult.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_After1000FailedAttempts_StillAllowsLogin()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPassword123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "no-lockout1000user",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "no-lockout1000@example.com",
            DisplayName = "No Lockout 1000 User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        for (var i = 0; i < 1000; i++)
        {
            var failedResult = await _sut.AuthenticateAsync("no-lockout1000user", $"WrongPassword{i}", ct);
            Assert.Null(failedResult);
        }

        var successResult = await _sut.AuthenticateAsync("no-lockout1000user", correctPassword, ct);
        Assert.NotNull(successResult);
        Assert.Equal("no-lockout1000user", successResult.Username);
    }

    [Fact]
    public async Task ChangePasswordAsync_AfterManyFailedAttempts_StillWorks()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalPassword = "OriginalPass123!";
        var newPassword = "NewPassword456!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "change-pass-no-lockout",
            PasswordHash = _passwordService.HashPassword(originalPassword),
            Email = "change-pass-no-lockout@example.com",
            DisplayName = "Change Password No Lockout",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        for (var i = 0; i < 50; i++)
        {
            var failedResult = await _sut.ChangePasswordAsync(user.Id, "WrongOldPassword", "NewPass", ct);
            Assert.False(failedResult);
        }

        var successResult = await _sut.ChangePasswordAsync(user.Id, originalPassword, newPassword, ct);
        Assert.True(successResult);

        var authenticated = await _sut.AuthenticateAsync("change-pass-no-lockout", newPassword, ct);
        Assert.NotNull(authenticated);
    }

    [Fact]
    public async Task AuthenticateAsync_InterleavedFailuresAndSuccesses_NeverLocks()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPassword123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "interleaved-user",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "interleaved@example.com",
            DisplayName = "Interleaved User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        for (var i = 0; i < 20; i++)
        {
            for (var j = 0; j < 5; j++)
            {
                var failedResult = await _sut.AuthenticateAsync("interleaved-user", "WrongPassword", ct);
                Assert.Null(failedResult);
            }

            var successResult = await _sut.AuthenticateAsync("interleaved-user", correctPassword, ct);
            Assert.NotNull(successResult);
        }

        var finalResult = await _sut.AuthenticateAsync("interleaved-user", correctPassword, ct);
        Assert.NotNull(finalResult);
        Assert.Equal("interleaved-user", finalResult.Username);
    }
}
```

```csharp src/MyBlog.Tests/Integration/AuthServiceTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Integration;

public class AuthServiceTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly AuthService _sut;
    private readonly PasswordService _passwordService = new(new PasswordHasher<User>());

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        var userRepository = new UserRepository(_context);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DefaultAdminPassword"] = "TestAdmin123!"
            })
            .Build();

        _sut = new AuthService(userRepository, _passwordService, configuration);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var password = "TestPassword123";
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(password),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("testuser", password, ct);

        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidPassword_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword("CorrectPassword"),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.AuthenticateAsync("testuser", "WrongPassword", ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_WithNonExistentUser_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.AuthenticateAsync("nonexistent", "password", ct);
        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureAdminUserAsync_WhenNoUsersExist_CreatesAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.EnsureAdminUserAsync(ct);

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin", ct);
        Assert.NotNull(admin);
    }

    [Fact]
    public async Task EnsureAdminUserAsync_WhenUsersExist_DoesNotCreateAnother()
    {
        var ct = TestContext.Current.CancellationToken;
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "existing",
            PasswordHash = "hash",
            Email = "existing@example.com",
            DisplayName = "Existing",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(existingUser);
        await _context.SaveChangesAsync(ct);

        await _sut.EnsureAdminUserAsync(ct);

        var userCount = await _context.Users.CountAsync(ct);
        Assert.Equal(1, userCount);
    }
}
```

```csharp src/MyBlog.Tests/Integration/DatabaseSchemaUpdaterTests.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using Xunit;

namespace MyBlog.Tests.Integration;

public class DatabaseSchemaUpdaterTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;

    public DatabaseSchemaUpdaterTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task ApplyUpdatesAsync_OnFreshDatabase_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        var count = await _context.ImageDimensionCache.CountAsync(ct);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_WhenImageDimensionCacheTableMissing_CreatesIt()
    {
        var ct = TestContext.Current.CancellationToken;

        await _context.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS \"ImageDimensionCache\"", ct);

        var tableExists = await TableExistsAsync("ImageDimensionCache", ct);
        Assert.False(tableExists);

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        tableExists = await TableExistsAsync("ImageDimensionCache", ct);
        Assert.True(tableExists);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_CalledMultipleTimes_IsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);
        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        var count = await _context.ImageDimensionCache.CountAsync(ct);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_AfterRecreation_TableIsUsable()
    {
        var ct = TestContext.Current.CancellationToken;

        await _context.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS \"ImageDimensionCache\"", ct);

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        _context.ImageDimensionCache.Add(new ImageDimensionCache
        {
            Url = "https://example.com/test.png",
            Width = 800,
            Height = 600,
            LastCheckedUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        var cached = await _context.ImageDimensionCache
            .FirstOrDefaultAsync(x => x.Url == "https://example.com/test.png", ct);
        Assert.NotNull(cached);
        Assert.Equal(800, cached.Width);
        Assert.Equal(600, cached.Height);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_PreservesExistingData()
    {
        var ct = TestContext.Current.CancellationToken;

        _context.ImageDimensionCache.Add(new ImageDimensionCache
        {
            Url = "https://example.com/existing.png",
            Width = 1024,
            Height = 768,
            LastCheckedUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        var cached = await _context.ImageDimensionCache
            .FirstOrDefaultAsync(x => x.Url == "https://example.com/existing.png", ct);
        Assert.NotNull(cached);
        Assert.Equal(1024, cached.Width);
        Assert.Equal(768, cached.Height);
    }

    [Fact]
    public async Task ApplyUpdatesAsync_DoesNotAffectOtherTables()
    {
        var ct = TestContext.Current.CancellationToken;

        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "schema-test-user",
            PasswordHash = "hash",
            Email = "schema@example.com",
            DisplayName = "Schema Test",
            CreatedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(ct);

        await DatabaseSchemaUpdater.ApplyUpdatesAsync(_context);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == "schema-test-user", ct);
        Assert.NotNull(user);
        Assert.Equal("Schema Test", user.DisplayName);
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result) > 0;
    }
}
```

```csharp src/MyBlog.Tests/Integration/ImageDimensionServiceTests.cs
using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Integration;

public sealed class ImageDimensionServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public ImageDimensionServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<BlogDbContext>(options => options.UseSqlite(_connection));
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<BlogDbContext>().Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetDimensionsAsync_Png_ReturnsDimensions()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync(CreatePng(640, 480), ct);

        Assert.Equal(640, result?.Width);
        Assert.Equal(480, result?.Height);
    }

    [Fact]
    public async Task GetDimensionsAsync_GifWiderThanInt16_ReturnsUnsignedDimensions()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync(CreateGif(40000, 300), ct);

        Assert.Equal(40000, result?.Width);
        Assert.Equal(300, result?.Height);
    }

    [Fact]
    public async Task GetDimensionsAsync_JpegLargerThanInt16_ReturnsUnsignedDimensions()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync(CreateJpeg(50000, 40000, appSegmentLength: 0), ct);

        Assert.Equal(50000, result?.Width);
        Assert.Equal(40000, result?.Height);
    }

    [Fact]
    public async Task GetDimensionsAsync_JpegWithLargeAppSegment_SkipsEmbeddedFrameMarker()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync(CreateJpeg(1600, 1200, appSegmentLength: 0x9000), ct);

        Assert.Equal(1600, result?.Width);
        Assert.Equal(1200, result?.Height);
    }

    [Fact]
    public async Task GetDimensionsAsync_SecondCall_UsesCacheWithoutNetwork()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new StubHandler(CreatePng(10, 20));
        using var client = new HttpClient(handler);
        var sut = CreateService(client);
        const string url = "https://example.com/cached.png?a=1&b=2";

        var first = await sut.GetDimensionsAsync(url, ct);
        var second = await sut.GetDimensionsAsync(url, ct);

        Assert.Equal(1, handler.Calls);
        Assert.Equal(first, second);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        Assert.True(await db.ImageDimensionCache.AnyAsync(x => x.Url == url, ct));
    }

    [Fact]
    public async Task GetDimensionsAsync_UnknownFormat_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await ResolveAsync([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09], ct);

        Assert.Null(result);
    }

    private async Task<(int Width, int Height)?> ResolveAsync(byte[] content, CancellationToken ct)
    {
        using var client = new HttpClient(new StubHandler(content));
        var sut = CreateService(client);
        return await sut.GetDimensionsAsync($"https://example.com/{Guid.NewGuid()}", ct);
    }

    private ImageDimensionService CreateService(HttpClient client) =>
        new(client, _provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ImageDimensionService>.Instance);

    private static byte[] CreatePng(int width, int height)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
        bytes.AddRange(BigEndian32(width));
        bytes.AddRange(BigEndian32(height));
        bytes.AddRange(new byte[8]);
        return [.. bytes];
    }

    private static byte[] CreateGif(int width, int height)
    {
        var bytes = new List<byte> { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };
        bytes.Add((byte)(width & 0xFF));
        bytes.Add((byte)(width >> 8));
        bytes.Add((byte)(height & 0xFF));
        bytes.Add((byte)(height >> 8));
        bytes.AddRange(new byte[8]);
        return [.. bytes];
    }

    private static byte[] CreateJpeg(int width, int height, int appSegmentLength)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        if (appSegmentLength > 0)
        {
            bytes.AddRange([0xFF, 0xE1, (byte)(appSegmentLength >> 8), (byte)(appSegmentLength & 0xFF)]);
            var payload = new byte[appSegmentLength - 2];
            byte[] embeddedFrame = [0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x10, 0x00, 0x10];
            embeddedFrame.CopyTo(payload, 0);
            bytes.AddRange(payload);
        }

        bytes.AddRange([0xFF, 0xC0, 0x00, 0x11, 0x08]);
        bytes.AddRange(BigEndian16(height));
        bytes.AddRange(BigEndian16(width));
        bytes.AddRange(new byte[12]);
        bytes.AddRange([0xFF, 0xD9]);
        return [.. bytes];
    }

    private static byte[] BigEndian32(int value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static byte[] BigEndian16(int value) =>
        [(byte)(value >> 8), (byte)value];

    private sealed class StubHandler(byte[] content) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}
```

```csharp src/MyBlog.Tests/Integration/ImageRepositoryTests.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

public class ImageRepositoryTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly ImageRepository _sut;
    private readonly User _testUser;

    public ImageRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hash",
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();

        _sut = new ImageRepository(_context);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task CreateAsync_AddsImageToDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("test-image.png");

        var result = await _sut.CreateAsync(image, ct);

        Assert.NotEqual(Guid.Empty, result.Id);
        var saved = await _context.Images.FindAsync([result.Id], ct);
        Assert.NotNull(saved);
        Assert.Equal("test-image.png", saved.FileName);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsImage()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("test-image.png");
        await _sut.CreateAsync(image, ct);

        var result = await _sut.GetByIdAsync(image.Id, ct);

        Assert.NotNull(result);
        Assert.Equal("test-image.png", result.FileName);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllImagesOrderedByUploadDate()
    {
        var ct = TestContext.Current.CancellationToken;

        var image1 = CreateTestImage("first.png");
        image1.UploadedAtUtc = DateTime.UtcNow.AddHours(-2);
        await _sut.CreateAsync(image1, ct);

        var image2 = CreateTestImage("second.png");
        image2.UploadedAtUtc = DateTime.UtcNow.AddHours(-1);
        await _sut.CreateAsync(image2, ct);

        var image3 = CreateTestImage("third.png");
        image3.UploadedAtUtc = DateTime.UtcNow;
        await _sut.CreateAsync(image3, ct);

        var result = await _sut.GetAllAsync(ct);

        Assert.Equal(3, result.Count);
        Assert.Equal("third.png", result[0].FileName);
        Assert.Equal("second.png", result[1].FileName);
        Assert.Equal("first.png", result[2].FileName);
    }

    [Fact]
    public async Task GetAllAsync_WithNoImages_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetAllAsync(ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesImageFromDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("to-delete.png");
        await _sut.CreateAsync(image, ct);

        await _sut.DeleteAsync(image.Id, ct);

        var deleted = await _context.Images.FindAsync([image.Id], ct);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.DeleteAsync(Guid.NewGuid(), ct);
    }

    [Fact]
    public async Task CreateAsync_StoresImageData()
    {
        var ct = TestContext.Current.CancellationToken;
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var image = CreateTestImage("data-test.png", imageData);

        await _sut.CreateAsync(image, ct);

        var saved = await _sut.GetByIdAsync(image.Id, ct);
        Assert.NotNull(saved);
        Assert.Equal(imageData, saved.Data);
    }

    [Fact]
    public async Task CreateAsync_PreservesContentType()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("test.jpg");
        image.ContentType = "image/jpeg";

        await _sut.CreateAsync(image, ct);

        var saved = await _sut.GetByIdAsync(image.Id, ct);
        Assert.NotNull(saved);
        Assert.Equal("image/jpeg", saved.ContentType);
    }

    [Fact]
    public async Task CreateAsync_PreservesUserAssociation()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("user-test.png");

        await _sut.CreateAsync(image, ct);

        var saved = await _sut.GetByIdAsync(image.Id, ct);
        Assert.NotNull(saved);
        Assert.Equal(_testUser.Id, saved.UploadedByUserId);
    }

    [Fact]
    public async Task GetAllAsync_DoesNotTrackEntities()
    {
        var ct = TestContext.Current.CancellationToken;
        var image = CreateTestImage("tracking-test.png");
        await _sut.CreateAsync(image, ct);

        var result = await _sut.GetAllAsync(ct);

        Assert.Single(result);
        var entry = _context.Entry(result[0]);
        Assert.Equal(EntityState.Detached, entry.State);
    }

    private Image CreateTestImage(string fileName, byte[]? data = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            ContentType = "image/png",
            Data = data ?? new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            UploadedByUserId = _testUser.Id,
            UploadedAtUtc = DateTime.UtcNow
        };
}
```

```csharp src/MyBlog.Tests/Integration/PasswordChangeTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Integration;

public class PasswordChangeTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly AuthService _sut;
    private readonly PasswordService _passwordService = new(new PasswordHasher<User>());

    public PasswordChangeTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        var userRepository = new UserRepository(_context);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:DefaultAdminPassword"] = "TestAdmin123!"
            })
            .Build();

        _sut = new AuthService(userRepository, _passwordService, configuration);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalPassword = "OriginalPass123!";
        var newPassword = "NewPassword456!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(originalPassword),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.ChangePasswordAsync(user.Id, originalPassword, newPassword, ct);

        Assert.True(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectPassword_AllowsLoginWithNewPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalPassword = "OriginalPass123!";
        var newPassword = "NewPassword456!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(originalPassword),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        await _sut.ChangePasswordAsync(user.Id, originalPassword, newPassword, ct);

        var authenticated = await _sut.AuthenticateAsync("testuser", newPassword, ct);
        Assert.NotNull(authenticated);

        var oldAuth = await _sut.AuthenticateAsync("testuser", originalPassword, ct);
        Assert.Null(oldAuth);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPass123!";
        var wrongPassword = "WrongPassword!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        var result = await _sut.ChangePasswordAsync(user.Id, wrongPassword, "NewPass123!", ct);

        Assert.False(result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongPassword_DoesNotChangePassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var correctPassword = "CorrectPass123!";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = _passwordService.HashPassword(correctPassword),
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        await _sut.ChangePasswordAsync(user.Id, "WrongPassword!", "NewPass123!", ct);

        var authenticated = await _sut.AuthenticateAsync("testuser", correctPassword, ct);
        Assert.NotNull(authenticated);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithNonExistentUser_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.ChangePasswordAsync(Guid.NewGuid(), "any", "password", ct);
        Assert.False(result);
    }
}
```

```csharp src/MyBlog.Tests/Integration/PostRepositoryExtendedTests.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

public class PostRepositoryExtendedTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly PostRepository _sut;
    private readonly User _testUser;

    public PostRepositoryExtendedTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hash",
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();

        _sut = new PostRepository(_context);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task GetPublishedPostsAsync_ReturnsTotalCount()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 5; i++)
        {
            await _sut.CreateAsync(CreateTestPost($"Post {i}", isPublished: true), ct);
        }

        var (_, totalCount) = await _sut.GetPublishedPostsAsync(1, 2, ct);

        Assert.Equal(5, totalCount);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_RespectsPageSize()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 5; i++)
        {
            await _sut.CreateAsync(CreateTestPost($"Post {i}", isPublished: true), ct);
        }

        var (posts, _) = await _sut.GetPublishedPostsAsync(1, 2, ct);

        Assert.Equal(2, posts.Count);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_RespectsPageNumber()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 5; i++)
        {
            var post = CreateTestPost($"Post {i}", isPublished: true);
            post.PublishedAtUtc = DateTime.UtcNow.AddDays(-i);
            await _sut.CreateAsync(post, ct);
        }

        var (page1Posts, _) = await _sut.GetPublishedPostsAsync(1, 2, ct);
        var (page2Posts, _) = await _sut.GetPublishedPostsAsync(2, 2, ct);

        Assert.NotEqual(page1Posts[0].Id, page2Posts[0].Id);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_ExcludesDrafts()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.CreateAsync(CreateTestPost("Published", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var (posts, totalCount) = await _sut.GetPublishedPostsAsync(1, 10, ct);

        Assert.Equal(1, totalCount);
        Assert.Single(posts);
        Assert.Equal("Published", posts[0].Title);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_OrdersByPublishedDateDescending()
    {
        var ct = TestContext.Current.CancellationToken;

        var oldPost = CreateTestPost("Old", isPublished: true);
        oldPost.PublishedAtUtc = DateTime.UtcNow.AddDays(-5);
        await _sut.CreateAsync(oldPost, ct);

        var newPost = CreateTestPost("New", isPublished: true);
        newPost.PublishedAtUtc = DateTime.UtcNow;
        await _sut.CreateAsync(newPost, ct);

        var (posts, _) = await _sut.GetPublishedPostsAsync(1, 10, ct);

        Assert.Equal("New", posts[0].Title);
        Assert.Equal("Old", posts[1].Title);
    }

    [Fact]
    public async Task GetAllPostsAsync_IncludesBothPublishedAndDrafts()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.CreateAsync(CreateTestPost("Published", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var posts = await _sut.GetAllPostsAsync(ct);

        Assert.Equal(2, posts.Count);
    }

    [Fact]
    public async Task GetAllPostsAsync_OrdersByUpdatedDateDescending()
    {
        var ct = TestContext.Current.CancellationToken;

        var oldPost = CreateTestPost("Old", isPublished: true);
        oldPost.UpdatedAtUtc = DateTime.UtcNow.AddDays(-5);
        await _sut.CreateAsync(oldPost, ct);

        var newPost = CreateTestPost("New", isPublished: true);
        newPost.UpdatedAtUtc = DateTime.UtcNow;
        await _sut.CreateAsync(newPost, ct);

        var posts = await _sut.GetAllPostsAsync(ct);

        Assert.Equal("New", posts[0].Title);
        Assert.Equal("Old", posts[1].Title);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.CreateAsync(CreateTestPost("Post 1"), ct);
        await _sut.CreateAsync(CreateTestPost("Post 2"), ct);
        await _sut.CreateAsync(CreateTestPost("Post 3"), ct);

        var count = await _sut.GetCountAsync(ct);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetCountAsync_IncludesBothPublishedAndDrafts()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.CreateAsync(CreateTestPost("Published", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var count = await _sut.GetCountAsync(ct);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetRecentPostsAsync_ReturnsSpecifiedCount()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 10; i++)
        {
            await _sut.CreateAsync(CreateTestPost($"Post {i}"), ct);
        }

        var posts = await _sut.GetRecentPostsAsync(5, ct);

        Assert.Equal(5, posts.Count);
    }

    [Fact]
    public async Task GetRecentPostsAsync_OrdersByUpdatedDateDescending()
    {
        var ct = TestContext.Current.CancellationToken;

        var oldPost = CreateTestPost("Old");
        oldPost.UpdatedAtUtc = DateTime.UtcNow.AddDays(-5);
        await _sut.CreateAsync(oldPost, ct);

        var newPost = CreateTestPost("New");
        newPost.UpdatedAtUtc = DateTime.UtcNow;
        await _sut.CreateAsync(newPost, ct);

        var posts = await _sut.GetRecentPostsAsync(2, ct);

        Assert.Equal("New", posts[0].Title);
    }

    [Fact]
    public async Task IsSlugTakenAsync_WithExistingSlug_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test", slug: "test-slug");
        await _sut.CreateAsync(post, ct);

        var isTaken = await _sut.IsSlugTakenAsync("test-slug", cancellationToken: ct);

        Assert.True(isTaken);
    }

    [Fact]
    public async Task IsSlugTakenAsync_WithNonExistentSlug_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;

        var isTaken = await _sut.IsSlugTakenAsync("non-existent-slug", cancellationToken: ct);

        Assert.False(isTaken);
    }

    [Fact]
    public async Task IsSlugTakenAsync_WithExcludedPostId_ExcludesThatPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test", slug: "test-slug");
        await _sut.CreateAsync(post, ct);

        var isTaken = await _sut.IsSlugTakenAsync("test-slug", post.Id, ct);

        Assert.False(isTaken);
    }

    [Fact]
    public async Task IsSlugTakenAsync_WithDifferentExcludedPostId_StillReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test", slug: "test-slug");
        await _sut.CreateAsync(post, ct);

        var isTaken = await _sut.IsSlugTakenAsync("test-slug", Guid.NewGuid(), ct);

        Assert.True(isTaken);
    }

    [Fact]
    public async Task GetBySlugAsync_IncludesAuthorDisplayName()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test", slug: "test-slug");
        await _sut.CreateAsync(post, ct);

        var result = await _sut.GetBySlugAsync("test-slug", ct);

        Assert.NotNull(result);
        Assert.Equal("Test User", result.AuthorDisplayName);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesAuthorNavigation()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test");
        await _sut.CreateAsync(post, ct);

        var result = await _sut.GetByIdAsync(post.Id, ct);

        Assert.NotNull(result);
        Assert.NotNull(result.Author);
        Assert.Equal("testuser", result.Author.Username);
    }

    [Fact]
    public async Task UpdateAsync_ChangesModifiedFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Original Title");
        await _sut.CreateAsync(post, ct);

        post.Title = "Updated Title";
        post.Content = "Updated Content";
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _sut.UpdateAsync(post, ct);

        var updated = await _sut.GetByIdAsync(post.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Content", updated.Content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("To Delete");
        await _sut.CreateAsync(post, ct);

        await _sut.DeleteAsync(post.Id, ct);

        var deleted = await _sut.GetByIdAsync(post.Id, ct);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.DeleteAsync(Guid.NewGuid(), ct);
    }

    private Post CreateTestPost(string title, string? slug = null, bool isPublished = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Slug = slug ?? title.ToLower().Replace(" ", "-"),
            Content = "Test content",
            Summary = "Test summary",
            AuthorId = _testUser.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsPublished = isPublished,
            PublishedAtUtc = isPublished ? DateTime.UtcNow : null
        };
}
```

```csharp src/MyBlog.Tests/Integration/PostRepositoryTests.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

public class PostRepositoryTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly PostRepository _sut;
    private readonly User _testUser;

    public PostRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hash",
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();

        _sut = new PostRepository(_context);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task CreateAsync_AddsPostToDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test Post");

        var result = await _sut.CreateAsync(post, ct);

        Assert.NotEqual(Guid.Empty, result.Id);
        var saved = await _context.Posts.FindAsync([result.Id], ct);
        Assert.NotNull(saved);
        Assert.Equal("Test Post", saved.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test Post");
        await _sut.CreateAsync(post, ct);

        var result = await _sut.GetByIdAsync(post.Id, ct);

        Assert.NotNull(result);
        Assert.Equal("Test Post", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), ct);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySlugAsync_WithExistingSlug_ReturnsPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Test Post", "test-post");
        await _sut.CreateAsync(post, ct);

        var result = await _sut.GetBySlugAsync("test-post", ct);

        Assert.NotNull(result);
        Assert.Equal("Test Post", result.Title);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_ReturnsOnlyPublishedPosts()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestPost("Published", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var (posts, totalCount) = await _sut.GetPublishedPostsAsync(1, 10, ct);

        Assert.Single(posts);
        Assert.Equal("Published", posts.First().Title);
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Original Title");
        await _sut.CreateAsync(post, ct);

        post.Title = "Updated Title";
        await _sut.UpdateAsync(post, ct);

        var result = await _sut.GetByIdAsync(post.Id, ct);
        Assert.Equal("Updated Title", result!.Title);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPost()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("To Delete");
        await _sut.CreateAsync(post, ct);

        await _sut.DeleteAsync(post.Id, ct);

        var result = await _sut.GetByIdAsync(post.Id, ct);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPublishedPostsAsync_ReturnsCorrectCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestPost("Post 1", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Post 2", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var (_, totalCount) = await _sut.GetPublishedPostsAsync(1, 10, ct);

        Assert.Equal(2, totalCount);
    }

    private Post CreateTestPost(string title, string? slug = null, bool isPublished = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Slug = slug ?? title.ToLower().Replace(" ", "-"),
            Content = "Test content",
            Summary = "Test summary",
            AuthorId = _testUser.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsPublished = isPublished,
            PublishedAtUtc = isPublished ? DateTime.UtcNow : null
        };
}
```

```csharp src/MyBlog.Tests/Integration/PostRepositoryUpdateTests.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

public sealed class PostRepositoryUpdateTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly PostRepository _sut;
    private readonly User _testUser;

    public PostRepositoryUpdateTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hash",
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();

        _sut = new PostRepository(_context);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task UpdateAsync_WithDetachedPostAndStaleAuthor_DoesNotOverwriteAuthor()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Original", isPublished: true);
        await _sut.CreateAsync(post, ct);

        await _context.Users
            .Where(u => u.Id == _testUser.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, "changed-elsewhere"), ct);

        var otherOptions = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite(_context.Database.GetDbConnection())
            .Options;
        await using var otherContext = new BlogDbContext(otherOptions);

        var detached = new Post
        {
            Id = post.Id,
            Title = "Updated",
            Slug = post.Slug,
            Content = post.Content,
            Summary = post.Summary,
            AuthorId = _testUser.Id,
            CreatedAtUtc = post.CreatedAtUtc,
            UpdatedAtUtc = DateTime.UtcNow,
            PublishedAtUtc = post.PublishedAtUtc,
            IsPublished = post.IsPublished,
            Author = new User
            {
                Id = _testUser.Id,
                Username = _testUser.Username,
                PasswordHash = "stale",
                Email = _testUser.Email,
                DisplayName = _testUser.DisplayName,
                CreatedAtUtc = _testUser.CreatedAtUtc
            }
        };

        await new PostRepository(otherContext).UpdateAsync(detached, ct);

        var hash = await _context.Users.AsNoTracking()
            .Where(u => u.Id == _testUser.Id)
            .Select(u => u.PasswordHash)
            .SingleAsync(ct);
        var title = await _context.Posts.AsNoTracking()
            .Where(p => p.Id == post.Id)
            .Select(p => p.Title)
            .SingleAsync(ct);

        Assert.Equal("changed-elsewhere", hash);
        Assert.Equal("Updated", title);
    }

    [Fact]
    public async Task UpdateAsync_WithTrackedPost_PersistsChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        var post = CreateTestPost("Tracked", isPublished: true);
        await _sut.CreateAsync(post, ct);

        post.Title = "Tracked Updated";
        await _sut.UpdateAsync(post, ct);

        var title = await _context.Posts.AsNoTracking()
            .Where(p => p.Id == post.Id)
            .Select(p => p.Title)
            .SingleAsync(ct);
        Assert.Equal("Tracked Updated", title);
    }

    [Fact]
    public async Task GetPublishedCountAsync_CountsOnlyPublishedPosts()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestPost("Published One", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Published Two", isPublished: true), ct);
        await _sut.CreateAsync(CreateTestPost("Draft", isPublished: false), ct);

        var count = await _sut.GetPublishedCountAsync(ct);

        Assert.Equal(2, count);
    }

    private Post CreateTestPost(string title, bool isPublished) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Slug = title.ToLowerInvariant().Replace(" ", "-"),
            Content = "Test content",
            Summary = "Test summary",
            AuthorId = _testUser.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsPublished = isPublished,
            PublishedAtUtc = isPublished ? DateTime.UtcNow : null
        };
}
```

```csharp src/MyBlog.Tests/Integration/RssFeedTests.cs
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Web.Endpoints;
using Xunit;

namespace MyBlog.Tests.Integration;

public class RssFeedTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly PostRepository _postRepository;
    private readonly User _testUser;

    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace ContentNs = "http://purl.org/rss/1.0/modules/content/";

    public RssFeedTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _testUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            PasswordHash = "hash",
            Email = "test@example.com",
            DisplayName = "Test User",
            CreatedAtUtc = DateTime.UtcNow
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();

        _postRepository = new PostRepository(_context);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task RssFeed_GeneratesValidXml()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Test Post 1", isPublished: true, ct: ct);

        var bytes = await GenerateFeedBytes(ct);
        var xml = Encoding.UTF8.GetString(bytes);

        var doc = XDocument.Parse(xml);
        Assert.NotNull(doc.Root);
        Assert.Equal("rss", doc.Root.Name.LocalName);
    }

    [Fact]
    public async Task RssFeed_HasCorrectRssVersion()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Test Post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);

        Assert.Equal("2.0", doc.Root!.Attribute("version")?.Value);
    }

    [Fact]
    public async Task RssFeed_ContainsOnlyPublishedPosts()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Published Post 1", isPublished: true, ct: ct);
        await CreateTestPost("Published Post 2", isPublished: true, ct: ct);
        await CreateTestPost("Draft Post", isPublished: false, ct: ct);

        var doc = await GenerateFeedXDocument(ct);

        var items = doc.Descendants("item").ToList();
        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, i => i.Element("title")?.Value == "Draft Post");
    }

    [Fact]
    public async Task RssFeed_HasChannelMetadata()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Test Post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var channel = doc.Root!.Element("channel")!;

        Assert.Equal("TestBlog", channel.Element("title")?.Value);
        Assert.NotNull(channel.Element("link")?.Value);
        Assert.NotNull(channel.Element("description")?.Value);
        Assert.NotNull(channel.Element("language")?.Value);
    }

    [Fact]
    public async Task RssFeed_HasAtomSelfLink()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Test Post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var channel = doc.Root!.Element("channel")!;

        var atomLink = channel.Element(AtomNs + "link");
        Assert.NotNull(atomLink);
        Assert.Equal("self", atomLink.Attribute("rel")?.Value);
        Assert.Equal("application/rss+xml", atomLink.Attribute("type")?.Value);
        Assert.Contains("/feed.xml", atomLink.Attribute("href")?.Value);
    }

    [Fact]
    public async Task RssFeed_ItemHasGuidWithPermalink()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Guid Test Post", slug: "guid-test-post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();
        var guid = item.Element("guid");

        Assert.NotNull(guid);
        Assert.Equal("true", guid.Attribute("isPermaLink")?.Value);
        Assert.Contains("/post/guid-test-post", guid.Value);
    }

    [Fact]
    public async Task RssFeed_ItemHasRfc822PubDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var publishedDate = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        await CreateTestPost("Date Test Post", isPublished: true, publishedAt: publishedDate, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();
        var pubDate = item.Element("pubDate")?.Value;

        Assert.NotNull(pubDate);
        Assert.True(DateTimeOffset.TryParse(pubDate, out _), $"pubDate '{pubDate}' should be a valid RFC 822 date");
        Assert.Contains("GMT", pubDate);
    }

    [Fact]
    public async Task RssFeed_ItemHasRequiredElements()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Complete Post", slug: "complete-post", isPublished: true, ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();

        Assert.NotNull(item.Element("title"));
        Assert.Equal("Complete Post", item.Element("title")!.Value);
        Assert.NotNull(item.Element("link"));
        Assert.Contains("/post/complete-post", item.Element("link")!.Value);
        Assert.NotNull(item.Element("description"));
        Assert.NotNull(item.Element("author"));
        Assert.Equal("Test User", item.Element("author")!.Value);
    }

    [Fact]
    public async Task RssFeed_WithNoPosts_GeneratesEmptyFeed()
    {
        var ct = TestContext.Current.CancellationToken;

        var doc = await GenerateFeedXDocument(ct);
        var items = doc.Descendants("item").ToList();

        Assert.Empty(items);
        Assert.NotNull(doc.Root!.Element("channel")!.Element("title"));
    }

    [Fact]
    public async Task RssFeed_LimitsTo20Posts()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 25; i++)
        {
            await CreateTestPost($"Post {i}", isPublished: true, ct: ct);
        }

        var doc = await GenerateFeedXDocument(ct);
        var items = doc.Descendants("item").ToList();

        Assert.Equal(RssEndpoints.FeedItemLimit, items.Count);
    }

    [Fact]
    public async Task RssFeed_HasNoBom()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("BOM Test Post", isPublished: true, ct: ct);

        var bytes = await GenerateFeedBytes(ct);

        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "RSS feed should not contain a UTF-8 BOM");
    }

    [Fact]
    public async Task RssFeed_ItemHasContentEncoded_WithFullHtml()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Full Content Post", slug: "full-content-post", isPublished: true,
            content: "# Hello World\n\nThis is the **full** post content.", ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();
        var contentEncoded = item.Element(ContentNs + "encoded");

        Assert.NotNull(contentEncoded);
        var html = contentEncoded.Value;
        Assert.Contains("Hello World", html);
        Assert.Contains("full", html);
    }

    [Fact]
    public async Task RssFeed_ContentEncoded_IsCData()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("CData Test", slug: "cdata-test", isPublished: true,
            content: "Some <em>content</em> here", ct: ct);

        var bytes = await GenerateFeedBytes(ct);
        var xml = Encoding.UTF8.GetString(bytes);

        Assert.Contains("<![CDATA[", xml);
    }

    [Fact]
    public async Task RssFeed_DescriptionIsSummary_NotFullContent()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Summary Test", slug: "summary-test", isPublished: true,
            content: "This is a very long post body that should not appear in description",
            summary: "Short summary", ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();

        Assert.Equal("Short summary", item.Element("description")?.Value);
    }

    [Fact]
    public async Task RssFeed_ContentEncoded_ContainsRenderedMarkdown()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTestPost("Markdown Test", slug: "markdown-test", isPublished: true,
            content: "**bold text** and *italic text*", ct: ct);

        var doc = await GenerateFeedXDocument(ct);
        var item = doc.Descendants("item").First();
        var contentEncoded = item.Element(ContentNs + "encoded");

        Assert.NotNull(contentEncoded);
        var html = contentEncoded.Value;
        Assert.Contains("<p>", html);
    }

    private Task<byte[]> GenerateFeedBytes(CancellationToken ct) =>
        RssEndpoints.BuildFeedAsync("https://example.com", "TestBlog", _postRepository, new StubMarkdownService(), ct);

    private async Task<XDocument> GenerateFeedXDocument(CancellationToken ct)
    {
        var bytes = await GenerateFeedBytes(ct);
        var xml = Encoding.UTF8.GetString(bytes);
        return XDocument.Parse(xml);
    }

    private async Task CreateTestPost(
        string title,
        string? slug = null,
        bool isPublished = true,
        DateTime? publishedAt = null,
        string? content = null,
        string? summary = null,
        CancellationToken ct = default)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            Title = title,
            Slug = slug ?? title.ToLower().Replace(" ", "-"),
            Content = content ?? $"Content for {title}",
            Summary = summary ?? $"Summary for {title}",
            AuthorId = _testUser.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsPublished = isPublished,
            PublishedAtUtc = isPublished ? (publishedAt ?? DateTime.UtcNow) : null
        };

        _context.Posts.Add(post);
        await _context.SaveChangesAsync(ct);
    }

    private sealed class StubMarkdownService : IMarkdownService
    {
        public Task<string> ToHtmlAsync(string markdown) =>
            Task.FromResult($"<p>{markdown}</p>");
    }
}
```

```csharp src/MyBlog.Tests/Integration/TelemetryCleanupTests.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using MyBlog.Infrastructure.Telemetry;
using Xunit;

namespace MyBlog.Tests.Integration;

public sealed class TelemetryCleanupTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly TelemetryLogRepository _sut;
    private readonly string _logsDirectory;

    public TelemetryCleanupTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _sut = new TelemetryLogRepository(_context);

        _logsDirectory = Path.Combine(Path.GetTempPath(), $"myblog-logs-{Guid.NewGuid()}");
        Directory.CreateDirectory(_logsDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        Directory.Delete(_logsDirectory, recursive: true);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddLogAsync(DateTime.UtcNow.AddDays(-60), ct);
        await AddLogAsync(DateTime.UtcNow.AddDays(-5), ct);

        var deleted = await _sut.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30), ct);

        Assert.Equal(1, deleted);
        Assert.Equal(1, await _context.TelemetryLogs.CountAsync(ct));
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithNoOldLogs_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddLogAsync(DateTime.UtcNow.AddDays(-5), ct);

        var deleted = await _sut.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30), ct);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithEmptyTable_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;

        var deleted = await _sut.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30), ct);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task TrimToNewestAsync_KeepsOnlyNewestRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await AddLogAsync(now.AddMinutes(-i), ct);
        }

        var deleted = await _sut.TrimToNewestAsync(3, ct);

        Assert.Equal(2, deleted);
        var remaining = await _context.TelemetryLogs.AsNoTracking()
            .OrderByDescending(l => l.TimestampUtc)
            .Select(l => l.TimestampUtc)
            .ToListAsync(ct);
        Assert.Equal(new[] { now, now.AddMinutes(-1), now.AddMinutes(-2) }, remaining);
    }

    [Fact]
    public async Task TrimToNewestAsync_UnderLimit_DeletesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await AddLogAsync(DateTime.UtcNow, ct);

        var deleted = await _sut.TrimToNewestAsync(10, ct);

        Assert.Equal(0, deleted);
        Assert.Equal(1, await _context.TelemetryLogs.CountAsync(ct));
    }

    [Fact]
    public void LogFileRetention_DeletesFilesOlderThanCutoff()
    {
        var old = CreateLogFile("logs_old.json", 10, DateTime.UtcNow.AddDays(-31));
        var recent = CreateLogFile("logs_recent.json", 10, DateTime.UtcNow.AddDays(-1));

        var (deleted, failed) = LogFileRetention.Apply(_logsDirectory, DateTime.UtcNow.AddDays(-30), long.MaxValue);

        Assert.Equal(1, deleted);
        Assert.Equal(0, failed);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void LogFileRetention_OverBudget_DeletesOldestFirstAndKeepsNewest()
    {
        var oldest = CreateLogFile("logs_a.json", 100, DateTime.UtcNow.AddHours(-3));
        var middle = CreateLogFile("logs_b.json", 100, DateTime.UtcNow.AddHours(-2));
        var newest = CreateLogFile("logs_c.json", 100, DateTime.UtcNow.AddHours(-1));

        var (deleted, _) = LogFileRetention.Apply(_logsDirectory, DateTime.UtcNow.AddDays(-30), 250);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(middle));
        Assert.True(File.Exists(newest));
    }

    [Fact]
    public void LogFileRetention_NewestFileLargerThanBudget_IsKept()
    {
        var newest = CreateLogFile("logs_big.json", 500, DateTime.UtcNow);

        var (deleted, _) = LogFileRetention.Apply(_logsDirectory, DateTime.UtcNow.AddDays(-30), 100);

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(newest));
    }

    [Fact]
    public void LogFileRetention_IgnoresFilesNotMatchingPattern()
    {
        var other = CreateLogFile("notes.txt", 10, DateTime.UtcNow.AddDays(-90));

        var (deleted, _) = LogFileRetention.Apply(_logsDirectory, DateTime.UtcNow.AddDays(-30), long.MaxValue);

        Assert.Equal(0, deleted);
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void LogFileRetention_MissingDirectory_ReturnsZero()
    {
        var (deleted, failed) = LogFileRetention.Apply(
            Path.Combine(_logsDirectory, "missing"), DateTime.UtcNow, long.MaxValue);

        Assert.Equal(0, deleted);
        Assert.Equal(0, failed);
    }

    private async Task AddLogAsync(DateTime timestampUtc, CancellationToken ct)
    {
        _context.TelemetryLogs.Add(new TelemetryLog
        {
            TimestampUtc = timestampUtc,
            Level = "Information",
            Category = "Test",
            Message = "Log"
        });
        await _context.SaveChangesAsync(ct);
    }

    private string CreateLogFile(string name, int bytes, DateTime lastWriteUtc)
    {
        var path = Path.Combine(_logsDirectory, name);
        File.WriteAllBytes(path, new byte[bytes]);
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
        return path;
    }
}
```

```csharp src/MyBlog.Tests/Integration/TelemetryExportTests.cs
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Xunit;

namespace MyBlog.Tests.Integration;

public sealed class TelemetryExportTests
{
    [Fact]
    public async Task AddLocalExporters_WithDatabaseLogging_PersistsLogRecords()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Telemetry:EnableFileLogging"] = "false",
                ["Telemetry:EnableDatabaseLogging"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDbContext<BlogDbContext>(options => options.UseSqlite(connection));
        services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Warning)
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.AddLocalExporters(configuration);
            }));

        await using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BlogDbContext>().Database.EnsureCreatedAsync(ct);
        }

        provider.GetRequiredService<ILogger<TelemetryExportTests>>().LogWarning("Exported {Value}", 42);
        Assert.True(provider.GetRequiredService<LoggerProvider>().ForceFlush(10_000));

        using var verifyScope = provider.CreateScope();
        var log = await verifyScope.ServiceProvider.GetRequiredService<BlogDbContext>()
            .TelemetryLogs.AsNoTracking()
            .Where(l => l.Category == typeof(TelemetryExportTests).FullName)
            .SingleAsync(ct);
        Assert.Equal("Warning", log.Level);
        Assert.Equal("Exported 42", log.Message);
    }

    [Fact]
    public void FileLogExporter_OnShutdown_WritesValidJsonArray()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"myblog-file-logs-{Guid.NewGuid()}");
        try
        {
            using (var factory = LoggerFactory.Create(logging => logging
                .SetMinimumLevel(LogLevel.Information)
                .AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.AddProcessor(new BatchLogRecordExportProcessor(new FileLogExporter(directory)));
                })))
            {
                factory.CreateLogger("FileTest").LogInformation("First {Number}", 1);
                factory.CreateLogger("FileTest").LogInformation("Second {Number}", 2);
            }

            var file = Assert.Single(Directory.GetFiles(directory, "logs_*.json"));
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var messages = document.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("Message").GetString())
                .ToList();
            Assert.Equal(new string?[] { "First 1", "Second 2" }, messages);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
```

```csharp src/MyBlog.Tests/Integration/UserRepositoryTests.cs
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Repositories;
using Xunit;

namespace MyBlog.Tests.Integration;

public class UserRepositoryTests : IAsyncDisposable
{
    private readonly BlogDbContext _context;
    private readonly UserRepository _sut;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BlogDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _context = new BlogDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _sut = new UserRepository(_context);
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task CreateAsync_AddsUserToDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("newuser");

        await _sut.CreateAsync(user, ct);

        var saved = await _context.Users.FindAsync([user.Id], ct);
        Assert.NotNull(saved);
        Assert.Equal("newuser", saved.Username);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("testuser");
        await _sut.CreateAsync(user, ct);

        var result = await _sut.GetByIdAsync(user.Id, ct);

        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUsernameAsync_WithExistingUsername_ReturnsUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("findme");
        await _sut.CreateAsync(user, ct);

        var result = await _sut.GetByUsernameAsync("findme", ct);

        Assert.NotNull(result);
        Assert.Equal("findme", result.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_IsCaseInsensitive()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("CaseSensitive");
        await _sut.CreateAsync(user, ct);

        var result = await _sut.GetByUsernameAsync("casesensitive", ct);

        Assert.NotNull(result);
        Assert.Equal("CaseSensitive", result.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_WithNonExistingUsername_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetByUsernameAsync("nonexistent", ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsersOrderedByUsername()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestUser("charlie"), ct);
        await _sut.CreateAsync(CreateTestUser("alice"), ct);
        await _sut.CreateAsync(CreateTestUser("bob"), ct);

        var result = await _sut.GetAllAsync(ct);

        Assert.Equal(3, result.Count);
        Assert.Equal("alice", result[0].Username);
        Assert.Equal("bob", result[1].Username);
        Assert.Equal("charlie", result[2].Username);
    }

    [Fact]
    public async Task GetAllAsync_WithNoUsers_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.GetAllAsync(ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AnyUsersExistAsync_WithNoUsers_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _sut.AnyUsersExistAsync(ct);

        Assert.False(result);
    }

    [Fact]
    public async Task AnyUsersExistAsync_WithUsers_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        await _sut.CreateAsync(CreateTestUser("anyuser"), ct);

        var result = await _sut.AnyUsersExistAsync(ct);

        Assert.True(result);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesExistingUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("updateme");
        await _sut.CreateAsync(user, ct);

        user.DisplayName = "Updated Display Name";
        user.Email = "updated@example.com";
        await _sut.UpdateAsync(user, ct);

        var updated = await _sut.GetByIdAsync(user.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("Updated Display Name", updated.DisplayName);
        Assert.Equal("updated@example.com", updated.Email);
    }

    [Fact]
    public async Task UpdateAsync_CanChangePasswordHash()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("passwordchange");
        await _sut.CreateAsync(user, ct);

        user.PasswordHash = "newhash";
        await _sut.UpdateAsync(user, ct);

        var updated = await _sut.GetByIdAsync(user.Id, ct);
        Assert.NotNull(updated);
        Assert.Equal("newhash", updated.PasswordHash);
    }

    [Fact]
    public async Task DeleteAsync_RemovesUserFromDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser("deleteme");
        await _sut.CreateAsync(user, ct);

        await _sut.DeleteAsync(user.Id, ct);

        var deleted = await _context.Users.FindAsync([user.Id], ct);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.DeleteAsync(Guid.NewGuid(), ct);
    }

    [Fact]
    public async Task DeleteAsync_DecreasesUserCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var user1 = CreateTestUser("user1");
        var user2 = CreateTestUser("user2");
        await _sut.CreateAsync(user1, ct);
        await _sut.CreateAsync(user2, ct);

        await _sut.DeleteAsync(user1.Id, ct);

        var allUsers = await _sut.GetAllAsync(ct);
        Assert.Single(allUsers);
        Assert.Equal("user2", allUsers[0].Username);
    }

    [Fact]
    public async Task CreateAsync_PreservesAllFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "fulluser",
            PasswordHash = "hash123",
            Email = "full@example.com",
            DisplayName = "Full User",
            CreatedAtUtc = createdAt
        };

        await _sut.CreateAsync(user, ct);

        var saved = await _sut.GetByIdAsync(user.Id, ct);
        Assert.NotNull(saved);
        Assert.Equal("fulluser", saved.Username);
        Assert.Equal("hash123", saved.PasswordHash);
        Assert.Equal("full@example.com", saved.Email);
        Assert.Equal("Full User", saved.DisplayName);
        Assert.Equal(createdAt, saved.CreatedAtUtc);
    }

    private static User CreateTestUser(string username) =>
        new()
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = "hash",
            Email = $"{username}@example.com",
            DisplayName = $"Display {username}",
            CreatedAtUtc = DateTime.UtcNow
        };
}
```

```xml src/MyBlog.Tests/MyBlog.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.v3" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyBlog.Core\MyBlog.Core.csproj" />
    <ProjectReference Include="..\MyBlog.Web\MyBlog.Web.csproj" />
    <ProjectReference Include="..\MyBlog.Infrastructure\MyBlog.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

```csharp src/MyBlog.Tests/Unit/DatabasePathResolverTests.cs
using MyBlog.Infrastructure.Data;
using Xunit;

namespace MyBlog.Tests.Unit;

public class DatabasePathResolverTests
{
    [Fact]
    public void GetDatabasePath_ReturnsNonEmptyPath()
    {
        var path = DatabasePathResolver.GetDatabasePath();

        Assert.False(string.IsNullOrEmpty(path));
    }

    [Fact]
    public void GetDatabasePath_EndsWithMyblogDb()
    {
        var path = DatabasePathResolver.GetDatabasePath();

        Assert.EndsWith("myblog.db", path);
    }

    [Fact]
    public void GetDatabasePath_ContainsMyBlogDirectory()
    {
        var path = DatabasePathResolver.GetDatabasePath();

        Assert.Contains("MyBlog", path);
    }

    [Fact]
    public void GetDataDirectory_ReturnsNonEmptyPath()
    {
        var path = DatabasePathResolver.GetDataDirectory();

        Assert.False(string.IsNullOrEmpty(path));
    }

    [Fact]
    public void GetDataDirectory_ContainsMyBlogDirectory()
    {
        var path = DatabasePathResolver.GetDataDirectory();

        Assert.EndsWith("MyBlog", path);
    }

    [Fact]
    public void GetDatabasePath_CreatesDirectoryIfNotExists()
    {
        var path = DatabasePathResolver.GetDatabasePath();
        var directory = Path.GetDirectoryName(path);

        Assert.NotNull(directory);
        Assert.True(Directory.Exists(directory));
    }

    [Fact]
    public void GetDatabasePath_ReturnsAbsolutePath()
    {
        var path = DatabasePathResolver.GetDatabasePath();

        Assert.True(Path.IsPathRooted(path));
    }

    [Fact]
    public void GetDataDirectory_ReturnsAbsolutePath()
    {
        var path = DatabasePathResolver.GetDataDirectory();

        Assert.True(Path.IsPathRooted(path));
    }

    [Fact]
    public void GetDatabasePath_ConsistentAcrossMultipleCalls()
    {
        var path1 = DatabasePathResolver.GetDatabasePath();
        var path2 = DatabasePathResolver.GetDatabasePath();

        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetDataDirectory_ConsistentAcrossMultipleCalls()
    {
        var path1 = DatabasePathResolver.GetDataDirectory();
        var path2 = DatabasePathResolver.GetDataDirectory();

        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetDatabasePath_IsInDataDirectory()
    {
        var dbPath = DatabasePathResolver.GetDatabasePath();
        var dataDir = DatabasePathResolver.GetDataDirectory();

        var dbDirectory = Path.GetDirectoryName(dbPath);
        Assert.Equal(dataDir, dbDirectory);
    }
}
```

```csharp src/MyBlog.Tests/Unit/LoginRateLimitMiddlewareTests.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MyBlog.Web.Middleware;
using Xunit;

namespace MyBlog.Tests.Unit;

public sealed class LoginRateLimitMiddlewareTests : IDisposable
{
    private readonly LoginRateLimitMiddleware _sut;
    private int _nextCallCount;
    private readonly List<TimeSpan> _recordedDelays = [];

    public LoginRateLimitMiddlewareTests()
    {
        LoginRateLimitMiddleware.ClearAttempts();
        _nextCallCount = 0;
        RequestDelegate next = _ =>
        {
            _nextCallCount++;
            return Task.CompletedTask;
        };

        Task NoOpDelay(TimeSpan delay, CancellationToken ct)
        {
            _recordedDelays.Add(delay);
            return Task.CompletedTask;
        }

        _sut = new LoginRateLimitMiddleware(
            next,
            NullLogger<LoginRateLimitMiddleware>.Instance,
            NoOpDelay);
    }

    public void Dispose() =>
        LoginRateLimitMiddleware.ClearAttempts();

    [Fact]
    public async Task InvokeAsync_NonLoginRequest_PassesThroughImmediately()
    {
        var context = CreateHttpContext("/api/posts", "GET");
        await _sut.InvokeAsync(context);

        Assert.Equal(1, _nextCallCount);
        Assert.Empty(_recordedDelays);
    }

    [Fact]
    public async Task InvokeAsync_GetLoginRequest_PassesThroughImmediately()
    {
        var context = CreateHttpContext("/login", "GET");
        await _sut.InvokeAsync(context);

        Assert.Equal(1, _nextCallCount);
        Assert.Empty(_recordedDelays);
    }

    [Fact]
    public async Task InvokeAsync_FirstFiveAttempts_NoDelay()
    {
        var uniqueIp = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 5; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(5, _nextCallCount);
        Assert.Empty(_recordedDelays);
    }

    [Fact]
    public async Task InvokeAsync_SixthAttempt_HasOneSecondDelay()
    {
        var uniqueIp = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 6; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(6, _nextCallCount);
        Assert.Single(_recordedDelays);
        Assert.Equal(TimeSpan.FromSeconds(1), _recordedDelays[0]);
    }

    [Fact]
    public async Task InvokeAsync_ProgressiveDelays_IncreaseExponentially()
    {
        var uniqueIp = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 10; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(10, _nextCallCount);
        Assert.Equal(5, _recordedDelays.Count);

        Assert.Equal(TimeSpan.FromSeconds(1), _recordedDelays[0]);
        Assert.Equal(TimeSpan.FromSeconds(2), _recordedDelays[1]);
        Assert.Equal(TimeSpan.FromSeconds(4), _recordedDelays[2]);
        Assert.Equal(TimeSpan.FromSeconds(8), _recordedDelays[3]);
        Assert.Equal(TimeSpan.FromSeconds(16), _recordedDelays[4]);
    }

    [Fact]
    public async Task InvokeAsync_DelayCappedAt30Seconds()
    {
        var uniqueIp = $"192.168.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 15; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(15, _nextCallCount);
        var maxDelays = _recordedDelays.Where(d => d == TimeSpan.FromSeconds(30)).ToList();
        Assert.True(maxDelays.Count >= 4, "Should have multiple 30-second delays");
        Assert.True(_recordedDelays.All(d => d <= TimeSpan.FromSeconds(30)), "No delay should exceed 30 seconds");
    }

    [Fact]
    public async Task InvokeAsync_AfterManyAttempts_NeverBlocks()
    {
        var uniqueIp = $"10.0.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        for (var i = 0; i < 100; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", uniqueIp);
            await _sut.InvokeAsync(context);
        }

        Assert.Equal(100, _nextCallCount);
    }

    [Fact]
    public async Task InvokeAsync_DifferentIPs_IndependentTracking()
    {
        var ip1 = $"10.1.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        var ip2 = $"10.2.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";

        for (var i = 0; i < 6; i++)
        {
            var context = CreateHttpContext("/account/login", "POST", ip1);
            await _sut.InvokeAsync(context);
        }

        var ip1Delays = _recordedDelays.Count;
        Assert.Equal(1, ip1Delays);

        var context2 = CreateHttpContext("/account/login", "POST", ip2);
        await _sut.InvokeAsync(context2);

        Assert.Equal(ip1Delays, _recordedDelays.Count);
    }

    [Fact]
    public void CalculateDelay_UnknownIP_ReturnsZero()
    {
        var delay = LoginRateLimitMiddleware.CalculateDelay("unknown-ip-never-seen");
        Assert.Equal(TimeSpan.Zero, delay);
    }

    private static DefaultHttpContext CreateHttpContext(string path, string method, string? remoteIp = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;

        if (remoteIp != null)
        {
            context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(remoteIp);
        }

        return context;
    }
}
```

````csharp src/MyBlog.Tests/Unit/MarkdownServiceEdgeCaseTests.cs
using MyBlog.Core.Interfaces;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

internal sealed class NullImageDimensionService : IImageDimensionService
{
    public Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult<(int, int)?>(null);
}

public class MarkdownServiceEdgeCaseTests
{
    private readonly MarkdownService _sut = new(new NullImageDimensionService());

    private static string NormalizeNewlines(string s) => s.Replace("\r\n", "\n");

    [Fact]
    public async Task ToHtml_WithNestedBoldAndItalic_ProcessesBothCorrectly()
    {
        var result = await _sut.ToHtmlAsync("This is **bold with *nested italic* inside**");

        Assert.Contains("<strong>bold with <em>nested italic</em> inside</strong>", result);
    }

    [Fact]
    public async Task ToHtml_WithCodeBlockContainingSpecialCharacters_PreservesContent()
    {
        var markdown = "```\n<script>alert('xss')</script>\n```";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<pre><code>", result);
        Assert.Contains("&lt;script&gt;", result);
        Assert.DoesNotContain("<script>", result);
    }

    [Fact]
    public async Task ToHtml_WithHtmlInParagraph_EscapesHtml()
    {
        var result = await _sut.ToHtmlAsync("<div>Not allowed</div>");

        Assert.Contains("&lt;div&gt;", result);
        Assert.DoesNotContain("<div>", result);
    }

    [Fact]
    public async Task ToHtml_WithMixedListTypes_ClosesListsProperly()
    {
        var markdown = "- Unordered item\n\n1. Ordered item";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        Assert.Contains("</ul>", result);
        Assert.Contains("<ol>", result);
    }

    [Fact]
    public async Task ToHtml_WithMultipleCodeBlocks_HandlesEachCorrectly()
    {
        var markdown = "```\nfirst block\n```\n\nSome text\n\n```\nsecond block\n```";
        var result = await _sut.ToHtmlAsync(markdown);

        var codeBlockCount = result.Split("<pre><code>").Length - 1;
        Assert.Equal(2, codeBlockCount);
    }

    [Fact]
    public async Task ToHtml_WithUnclosedCodeBlock_ClosesAutomatically()
    {
        var markdown = "```\nunclosed code block";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("</code></pre>", result);
    }

    [Fact]
    public async Task ToHtml_WithLinkContainingSpecialCharacters_EncodesCorrectly()
    {
        var result = await _sut.ToHtmlAsync("[Link](https://example.com/path?a=1&b=2)");

        Assert.Contains("href=\"https://example.com/path?a=1&amp;b=2\"", result);
    }

    [Fact]
    public async Task ToHtml_WithConsecutiveHeadings_ProcessesAll()
    {
        var markdown = "# H1\n## H2\n### H3";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<h1>H1</h1>", result);
        Assert.Contains("<h2>H2</h2>", result);
        Assert.Contains("<h3>H3</h3>", result);
    }

    [Fact]
    public async Task ToHtml_WithMultipleBlockquotes_ProcessesEach()
    {
        var markdown = "> Quote 1\n\n> Quote 2";
        var result = await _sut.ToHtmlAsync(markdown);

        var quoteCount = result.Split("<blockquote>").Length - 1;
        Assert.Equal(2, quoteCount);
    }

    [Fact]
    public async Task ToHtml_WithHorizontalRuleVariations_AllWork()
    {
        var markdown1 = await _sut.ToHtmlAsync("---");
        var markdown2 = await _sut.ToHtmlAsync("***");
        var markdown3 = await _sut.ToHtmlAsync("___");
        var markdown4 = await _sut.ToHtmlAsync("----");

        Assert.Contains("<hr />", markdown1);
        Assert.Contains("<hr />", markdown2);
        Assert.Contains("<hr />", markdown3);
        Assert.Contains("<hr />", markdown4);
    }

    [Fact]
    public async Task ToHtml_WithUnorderedListVariations_BothWork()
    {
        var dashList = await _sut.ToHtmlAsync("- Item 1\n- Item 2");
        var asteriskList = await _sut.ToHtmlAsync("* Item 1\n* Item 2");

        Assert.Contains("<ul>", dashList);
        Assert.Contains("<ul>", asteriskList);
    }

    [Fact]
    public async Task ToHtml_WithLongOrderedList_MaintainsStructure()
    {
        var markdown = "1. First\n2. Second\n3. Third\n4. Fourth\n5. Fifth";
        var result = await _sut.ToHtmlAsync(markdown);

        var liCount = result.Split("<li>").Length - 1;
        Assert.Equal(5, liCount);
    }

    [Fact]
    public async Task ToHtml_WithInlineCodeInHeading_ProcessesBoth()
    {
        var result = await _sut.ToHtmlAsync("# Heading with `code`");

        Assert.Contains("<h1>Heading with <code>code</code></h1>", result);
    }

    [Fact]
    public async Task ToHtml_WithBoldInListItem_ProcessesBoth()
    {
        var result = await _sut.ToHtmlAsync("- Item with **bold**");

        Assert.Contains("<li>Item with <strong>bold</strong></li>", result);
    }

    [Fact]
    public async Task ToHtml_WithLinkInListItem_ProcessesBoth()
    {
        var result = await _sut.ToHtmlAsync("1. Check [this](https://example.com)");

        Assert.Contains("<li>Check <a href=\"https://example.com\">this</a></li>", result);
    }

    [Fact]
    public async Task ToHtml_WithEmptyLines_HandlesGracefully()
    {
        var markdown = "Paragraph 1\n\n\n\nParagraph 2";
        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Contains("<p>Paragraph 1</p>", result);
        Assert.Contains("<p>Paragraph 2</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithOnlyWhitespace_ReturnsEmpty()
    {
        var result = await _sut.ToHtmlAsync("   \n\n   \t   ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ToHtml_WithImageAltTextContainingSpecialChars_EncodesAlt()
    {
        var result = await _sut.ToHtmlAsync("![Image with \"quotes\"](https://example.com/img.png)");

        Assert.Contains("alt=\"Image with &quot;quotes&quot;\"", result);
    }

    [Fact]
    public async Task ToHtml_WithEmojiInContent_PreservesEmoji()
    {
        var result = await _sut.ToHtmlAsync("Hello 👋 World 🌍");

        Assert.Contains("👋", result);
        Assert.Contains("🌍", result);
    }

    [Fact]
    public async Task ToHtml_WithVeryLongLine_ProcessesCorrectly()
    {
        var longText = new string('a', 10000);
        var result = await _sut.ToHtmlAsync(longText);

        Assert.Contains($"<p>{longText}</p>", result);
    }

    [Fact]
    public async Task ToHtml_WithHeadingAfterList_ClosesListFirst()
    {
        var markdown = "- Item 1\n- Item 2\n# Heading";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        var ulCloseIndex = result.IndexOf("</ul>", StringComparison.Ordinal);
        var h1Index = result.IndexOf("<h1>", StringComparison.Ordinal);
        Assert.True(ulCloseIndex < h1Index);
    }

    [Fact]
    public async Task ToHtml_WithCodeBlockAfterList_ClosesListFirst()
    {
        var markdown = "- Item 1\n- Item 2\n```\ncode\n```";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        var ulCloseIndex = result.IndexOf("</ul>", StringComparison.Ordinal);
        var preIndex = result.IndexOf("<pre>", StringComparison.Ordinal);
        Assert.True(ulCloseIndex < preIndex);
    }
}
````

```csharp src/MyBlog.Tests/Unit/MarkdownServiceImageUrlTests.cs
using MyBlog.Core.Interfaces;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class MarkdownServiceImageUrlTests
{
    [Fact]
    public async Task ToHtml_WithImageQueryString_LooksUpRawUrlAndRendersEncodedSrc()
    {
        var recorder = new RecordingImageDimensionService();
        var sut = new MarkdownService(recorder);

        var result = await sut.ToHtmlAsync("![alt](https://example.com/img.png?w=1&h=2)");

        var url = Assert.Single(recorder.Urls);
        Assert.Equal("https://example.com/img.png?w=1&h=2", url);
        Assert.Contains("<img src=\"https://example.com/img.png?w=1&amp;h=2\" alt=\"alt\" width=\"10\" height=\"20\" />", result);
    }

    private sealed class RecordingImageDimensionService : IImageDimensionService
    {
        public List<string> Urls { get; } = [];

        public Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default)
        {
            Urls.Add(url);
            return Task.FromResult<(int, int)?>((10, 20));
        }
    }
}
```

```csharp src/MyBlog.Tests/Unit/MarkdownServiceLongDocumentTests.cs
using System.Text;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class MarkdownServiceLongDocumentTests
{
    private readonly MarkdownService _sut = new(new MockImageDimensionService());

    private static string BuildArticle()
    {
        var lines = new[]
        {
            "# Kids' Lungs Recovered Faster Than Anyone Expected",
            "",
            "A five-year London study found children's lung growth caught up \u2014 and the reason it worked so fast matters.",
            "",
            "---",
            "",
            "## The study",
            "",
            "The findings were published in *The Lancet Public Health*.",
            "",
            "An expert said \"carefully conducted\" and moved on.",
            "",
            "### Sourcing note",
            "",
            "Core findings are drawn from the university press release."
        };

        return string.Join("\n", lines);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }
        return count;
    }

    [Fact]
    public async Task ToHtml_WithRealWorldArticle_ProducesExpectedBlockStructure()
    {
        var result = await _sut.ToHtmlAsync(BuildArticle());

        Assert.Contains("<h1>Kids' Lungs Recovered Faster Than Anyone Expected</h1>", result);
        Assert.Contains("<h2>The study</h2>", result);
        Assert.Contains("<h3>Sourcing note</h3>", result);
        Assert.Contains("<hr />", result);
        Assert.Equal(4, CountOccurrences(result, "<p>"));
        Assert.Equal(4, CountOccurrences(result, "</p>"));
    }

    [Fact]
    public async Task ToHtml_WithRealWorldArticle_ProducesBalancedInlineEmphasis()
    {
        var result = await _sut.ToHtmlAsync(BuildArticle());

        Assert.Contains("<em>The Lancet Public Health</em>", result);
        Assert.Equal(
            CountOccurrences(result, "<em>"),
            CountOccurrences(result, "</em>"));

        Assert.DoesNotContain("*", result);
    }

    [Fact]
    public async Task ToHtml_WithRealWorldArticle_EncodesQuotesAndPreservesEmDash()
    {
        var result = await _sut.ToHtmlAsync(BuildArticle());

        Assert.Contains("&quot;carefully conducted&quot;", result);
        Assert.Contains("\u2014", result);

        Assert.Contains("children's", result);
    }

    [Fact]
    public async Task ToHtml_WithDocumentLargerThanDefaultSignalRLimit_ParsesEveryBlock()
    {
        const int sectionCount = 250;
        const string paragraph =
            "Air pollution concentration falls off sharply with distance from its source, and the " +
            "gradient shows up at scales most people never think about, right down to a few meters " +
            "of sidewalk on one side of a street rather than the other.";

        var builder = new StringBuilder();
        for (var i = 1; i <= sectionCount; i++)
        {
            builder.Append("## Section ").Append(i).Append('\n');
            builder.Append('\n');
            builder.Append(paragraph).Append('\n');
            builder.Append('\n');
        }
        builder.Append("The final paragraph must survive intact.");

        var markdown = builder.ToString();

        Assert.True(
            markdown.Length > 32 * 1024,
            $"Test input should exceed the 32 KB default message size, but was {markdown.Length} characters.");

        var result = await _sut.ToHtmlAsync(markdown);

        Assert.Equal(sectionCount, CountOccurrences(result, "<h2>"));
        Assert.Equal(sectionCount, CountOccurrences(result, "</h2>"));
        Assert.Equal(sectionCount + 1, CountOccurrences(result, "<p>"));
        Assert.Contains("<h2>Section 1</h2>", result);
        Assert.Contains($"<h2>Section {sectionCount}</h2>", result);
        Assert.Contains("<p>The final paragraph must survive intact.</p>", result);
    }
}
```

````csharp src/MyBlog.Tests/Unit/MarkdownServiceTests.cs
using MyBlog.Core.Interfaces;
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

internal sealed class MockImageDimensionService : IImageDimensionService
{
    public Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default)
    {
        if (url.Contains("image.png"))
        {
            return Task.FromResult<(int, int)?>((100, 200));
        }

        return Task.FromResult<(int, int)?>(null);
    }
}

internal sealed class ThrowingImageDimensionService : IImageDimensionService
{
    public Task<(int Width, int Height)?> GetDimensionsAsync(string url, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated database error");
}

public class MarkdownServiceTests
{
    private readonly MarkdownService _sut = new(new MockImageDimensionService());

    private static string NormalizeNewlines(string s) => s.Replace("\r\n", "\n");

    [Fact]
    public async Task ToHtml_WithHeading1_ReturnsH1Tag()
    {
        var result = await _sut.ToHtmlAsync("# Hello");
        Assert.Contains("<h1>Hello</h1>", result);
    }

    [Fact]
    public async Task ToHtml_WithHeading2_ReturnsH2Tag()
    {
        var result = await _sut.ToHtmlAsync("## Hello");
        Assert.Contains("<h2>Hello</h2>", result);
    }

    [Fact]
    public async Task ToHtml_WithHeading6_ReturnsH6Tag()
    {
        var result = await _sut.ToHtmlAsync("###### Hello");
        Assert.Contains("<h6>Hello</h6>", result);
    }

    [Fact]
    public async Task ToHtml_WithBoldText_ReturnsStrongTag()
    {
        var result = await _sut.ToHtmlAsync("This is **bold** text");
        Assert.Contains("<strong>bold</strong>", result);
    }

    [Fact]
    public async Task ToHtml_WithItalicText_ReturnsEmTag()
    {
        var result = await _sut.ToHtmlAsync("This is *italic* text");
        Assert.Contains("<em>italic</em>", result);
    }

    [Fact]
    public async Task ToHtml_WithLink_ReturnsAnchorTag()
    {
        var result = await _sut.ToHtmlAsync("Check [this link](https://example.com)");
        Assert.Contains("<p>Check <a href=\"https://example.com\">this link</a></p>", result);
    }

    [Fact]
    public async Task ToHtml_WithImage_InjectsDimensions_IfResolvable()
    {
        var result = NormalizeNewlines(await _sut.ToHtmlAsync("![alt text](https://example.com/image.png)"));
        Assert.Contains("<p><img src=\"https://example.com/image.png\" alt=\"alt text\" width=\"100\" height=\"200\" /></p>\n", result);
    }

    [Fact]
    public async Task ToHtml_WithImage_InjectsParagraphs_IfResolvable()
    {
        var result = NormalizeNewlines(await _sut.ToHtmlAsync("Check out this photo of when I was younger. ![high school graduation photo](https://example.com/image.png)"));
        Assert.Contains("<p>Check out this photo of when I was younger. <img src=\"https://example.com/image.png\" alt=\"high school graduation photo\" width=\"100\" height=\"200\" /></p>\n", result);
    }

    [Fact]
    public async Task ToHtml_WithImage_NoDimensions_IfUnresolvable()
    {
        var result = NormalizeNewlines(await _sut.ToHtmlAsync("![alt text](https://example.com/unknown.jpg)"));
        Assert.Contains("<p><img src=\"https://example.com/unknown.jpg\" alt=\"alt text\" /></p>\n", result);
    }

    [Fact]
    public async Task ToHtml_WithImage_WhenServiceThrows_StillRendersImage()
    {
        var throwingService = new ThrowingImageDimensionService();
        var sut = new MarkdownService(throwingService);

        var result = NormalizeNewlines(await sut.ToHtmlAsync("![alt text](https://example.com/image.png)"));
        Assert.Contains("<p><img src=\"https://example.com/image.png\" alt=\"alt text\" /></p>\n", result);
    }

    [Fact]
    public async Task ToHtml_WithInlineCode_ReturnsCodeTag()
    {
        var result = await _sut.ToHtmlAsync("Use `code` here");
        Assert.Contains("<code>code</code>", result);
    }

    [Fact]
    public async Task ToHtml_WithCodeBlock_ReturnsPreCodeTags()
    {
        var markdown = "```\nvar x = 1;\n```";
        var result = await _sut.ToHtmlAsync(markdown);
        Assert.Contains("<pre><code>", result);
        Assert.Contains("var x = 1;", result);
        Assert.Contains("</code></pre>", result);
    }

    [Fact]
    public async Task ToHtml_WithBlockquote_ReturnsBlockquoteTag()
    {
        var result = await _sut.ToHtmlAsync("> This is a quote");
        Assert.Contains("<blockquote><p>This is a quote</p></blockquote>", result);
    }

    [Fact]
    public async Task ToHtml_WithUnorderedList_ReturnsUlLiTags()
    {
        var markdown = "- Item 1\n- Item 2";
        var result = await _sut.ToHtmlAsync(markdown);
        Assert.Contains("<ul>", result);
        Assert.Contains("<li>Item 1</li>", result);
        Assert.Contains("<li>Item 2</li>", result);
        Assert.Contains("</ul>", result);
    }

    [Fact]
    public async Task ToHtml_WithHorizontalRule_ReturnsHrTag()
    {
        var result = await _sut.ToHtmlAsync("---");
        Assert.Contains("<hr />", result);
    }

    [Fact]
    public async Task ToHtml_WithOrderedList_ReturnsOlLiTags()
    {
        var markdown = "1. First\n2. Second";
        var result = await _sut.ToHtmlAsync(markdown);
        Assert.Contains("<ol>", result);
        Assert.Contains("<li>First</li>", result);
        Assert.Contains("<li>Second</li>", result);
        Assert.Contains("</ol>", result);
    }

    [Fact]
    public async Task ToHtml_WithEmptyString_ReturnsEmpty()
    {
        var result = await _sut.ToHtmlAsync("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ToHtml_WithNull_ReturnsEmpty()
    {
        var result = await _sut.ToHtmlAsync(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ToHtml_WithWhitespace_ReturnsEmpty()
    {
        var result = await _sut.ToHtmlAsync("   ");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ToHtml_WithMultipleImages_ProcessesAll()
    {
        var markdown = "![img1](https://example.com/image.png) and ![img2](https://example.com/other.jpg)";
        var result = NormalizeNewlines(await _sut.ToHtmlAsync(markdown));

        Assert.Contains("width=\"100\" height=\"200\"", result);
        Assert.Contains("<img src=\"https://example.com/other.jpg\" alt=\"img2\" />", result);
    }
}
````

```csharp src/MyBlog.Tests/Unit/PasswordServiceTests.cs
using Microsoft.AspNetCore.Identity;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class PasswordServiceTests
{
    private readonly PasswordService _sut = new(new PasswordHasher<User>());

    [Fact]
    public void HashPassword_ReturnsNonEmptyHash()
    {
        var hash = _sut.HashPassword("TestPassword123");
        Assert.False(string.IsNullOrEmpty(hash));
    }

    [Fact]
    public void HashPassword_ReturnsDifferentHashForSamePassword()
    {
        var hash1 = _sut.HashPassword("TestPassword123");
        var hash2 = _sut.HashPassword("TestPassword123");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var password = "TestPassword123";
        var hash = _sut.HashPassword(password);

        var result = _sut.VerifyPassword(hash, password);

        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var hash = _sut.HashPassword("TestPassword123");

        var result = _sut.VerifyPassword(hash, "WrongPassword");

        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_WithEmptyPassword_ReturnsFalse()
    {
        var hash = _sut.HashPassword("TestPassword123");

        var result = _sut.VerifyPassword(hash, "");

        Assert.False(result);
    }
}
```

```csharp src/MyBlog.Tests/Unit/ReaderTrackingServiceTests.cs
using MyBlog.Infrastructure.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class ReaderTrackingServiceTests
{
    private readonly ReaderTrackingService _sut = new();

    [Fact]
    public void JoinPost_WithNewSlug_ReturnsOne()
    {
        var count = _sut.JoinPost("test-slug", "connection-1");

        Assert.Equal(1, count);
    }

    [Fact]
    public void JoinPost_WithMultipleConnections_ReturnsIncrementingCount()
    {
        var count1 = _sut.JoinPost("test-slug", "connection-1");
        var count2 = _sut.JoinPost("test-slug", "connection-2");
        var count3 = _sut.JoinPost("test-slug", "connection-3");

        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
        Assert.Equal(3, count3);
    }

    [Fact]
    public void JoinPost_WithDifferentSlugs_TracksIndependently()
    {
        var count1 = _sut.JoinPost("slug-a", "connection-1");
        var count2 = _sut.JoinPost("slug-b", "connection-2");
        var count3 = _sut.JoinPost("slug-a", "connection-3");

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
        Assert.Equal(2, count3);
    }

    [Fact]
    public void LeavePost_WithExistingConnection_DecrementsCount()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.JoinPost("test-slug", "connection-2");

        var count = _sut.LeavePost("test-slug", "connection-2");

        Assert.Equal(1, count);
    }

    [Fact]
    public void LeavePost_WhenCountIsZero_ReturnsZero()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.LeavePost("test-slug", "connection-1");

        var count = _sut.LeavePost("test-slug", "connection-1");

        Assert.Equal(0, count);
    }

    [Fact]
    public void LeavePost_WithNonExistentSlug_ReturnsZero()
    {
        var count = _sut.LeavePost("non-existent", "connection-1");

        Assert.Equal(0, count);
    }

    [Fact]
    public void Disconnect_WithKnownConnection_ReturnsSlugAndDecrementedCount()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.JoinPost("test-slug", "connection-2");

        var (slug, newCount) = _sut.Disconnect("connection-1");

        Assert.Equal("test-slug", slug);
        Assert.Equal(1, newCount);
    }

    [Fact]
    public void Disconnect_WithUnknownConnection_ReturnsNullSlug()
    {
        var (slug, newCount) = _sut.Disconnect("unknown-connection");

        Assert.Null(slug);
        Assert.Equal(0, newCount);
    }

    [Fact]
    public void Disconnect_RemovesConnectionMapping()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.Disconnect("connection-1");

        var (slug, _) = _sut.Disconnect("connection-1");

        Assert.Null(slug);
    }

    [Fact]
    public void GetReaderCount_WithExistingSlug_ReturnsCorrectCount()
    {
        _sut.JoinPost("test-slug", "connection-1");
        _sut.JoinPost("test-slug", "connection-2");

        var count = _sut.GetReaderCount("test-slug");

        Assert.Equal(2, count);
    }

    [Fact]
    public void GetReaderCount_WithNonExistentSlug_ReturnsZero()
    {
        var count = _sut.GetReaderCount("non-existent");

        Assert.Equal(0, count);
    }

    [Fact]
    public void JoinPost_SameConnectionSwitchingPages_UpdatesMapping()
    {
        _sut.JoinPost("slug-a", "connection-1");

        _sut.JoinPost("slug-b", "connection-1");

        var (slug, _) = _sut.Disconnect("connection-1");

        Assert.Equal("slug-b", slug);
    }

    [Fact]
    public async Task ConcurrentJoins_MaintainsAccurateCount()
    {
        var ct = TestContext.Current.CancellationToken;
        const int connectionCount = 100;
        var tasks = new List<Task>();

        for (var i = 0; i < connectionCount; i++)
        {
            var connectionId = $"connection-{i}";
            tasks.Add(Task.Run(() => _sut.JoinPost("test-slug", connectionId), ct));
        }

        await Task.WhenAll(tasks);

        var finalCount = _sut.GetReaderCount("test-slug");
        Assert.Equal(connectionCount, finalCount);
    }

    [Fact]
    public async Task ConcurrentJoinsAndLeaves_MaintainsAccurateCount()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 50; i++)
        {
            _sut.JoinPost("test-slug", $"connection-{i}");
        }

        var tasks = new List<Task>();

        for (var i = 50; i < 75; i++)
        {
            var connectionId = $"connection-{i}";
            tasks.Add(Task.Run(() => _sut.JoinPost("test-slug", connectionId), ct));
        }

        for (var i = 0; i < 25; i++)
        {
            var connectionId = $"connection-{i}";
            tasks.Add(Task.Run(() => _sut.LeavePost("test-slug", connectionId), ct));
        }

        await Task.WhenAll(tasks);

        var finalCount = _sut.GetReaderCount("test-slug");
        Assert.Equal(50, finalCount);
    }

    [Fact]
    public async Task ConcurrentDisconnects_MaintainsAccurateCount()
    {
        var ct = TestContext.Current.CancellationToken;
        const int connectionCount = 50;

        for (var i = 0; i < connectionCount; i++)
        {
            _sut.JoinPost("test-slug", $"connection-{i}");
        }

        var tasks = new List<Task>();
        for (var i = 0; i < connectionCount; i++)
        {
            var connectionId = $"connection-{i}";
            tasks.Add(Task.Run(() => _sut.Disconnect(connectionId), ct));
        }

        await Task.WhenAll(tasks);

        var finalCount = _sut.GetReaderCount("test-slug");
        Assert.Equal(0, finalCount);
    }
}
```

```csharp src/MyBlog.Tests/Unit/SlugServiceTests.cs
using MyBlog.Core.Services;
using Xunit;

namespace MyBlog.Tests.Unit;

public class SlugServiceTests
{
    private readonly SlugService _sut = new();

    [Fact]
    public void GenerateSlug_WithSimpleTitle_ReturnsLowercaseWithHyphens()
    {
        var result = _sut.GenerateSlugOrUuid("Hello World");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void GenerateSlug_WithSpecialCharacters_RemovesThem()
    {
        var result = _sut.GenerateSlugOrUuid("Hello, World! How's it going?");
        Assert.Equal("hello-world-hows-it-going", result);
    }

    [Fact]
    public void GenerateSlug_WithMultipleSpaces_CollapsesToSingleHyphen()
    {
        var result = _sut.GenerateSlugOrUuid("Hello    World");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void GenerateSlug_WithUnicode_RemovesDiacritics()
    {
        var result = _sut.GenerateSlugOrUuid("Café résumé");
        Assert.Equal("cafe-resume", result);
    }

    [Fact]
    public void GenerateSlug_WithLeadingTrailingSpaces_TrimsHyphens()
    {
        var result = _sut.GenerateSlugOrUuid("  Hello World  ");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void GenerateSlug_WithNumbers_PreservesNumbers()
    {
        var result = _sut.GenerateSlugOrUuid("Top 10 Tips for 2024");
        Assert.Equal("top-10-tips-for-2024", result);
    }

    [Fact]
    public void GenerateSlug_WithUnderscores_ConvertsToHyphens()
    {
        var result = _sut.GenerateSlugOrUuid("hello_world_test");
        Assert.Equal("hello-world-test", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("       ")]
    public void GenerateSlug_WithEmptyStringOrWhitespace_ReturnsGuidWithPrefix(string input)
    {
        var result = _sut.GenerateSlugOrUuid(input);

        Assert.StartsWith("post-", result);

        var guidPart = result.Replace("post-", "");

        var isValidGuid = Guid.TryParse(guidPart, out _);
        Assert.True(isValidGuid, $"Expected a valid GUID but got {guidPart}");
    }

    [Fact]
    public void GenerateUuidSlug_ReturnsSlugWithPostPrefix()
    {
        var result = _sut.GenerateUuidSlug();

        Assert.StartsWith("post-", result);
    }

    [Fact]
    public void GenerateUuidSlug_ReturnsValidUuidPart()
    {
        var result = _sut.GenerateUuidSlug();

        var guidPart = result.Replace("post-", "");

        var isValidGuid = Guid.TryParse(guidPart, out _);
        Assert.True(isValidGuid, $"Expected a valid GUID but got {guidPart}");
    }

    [Fact]
    public void GenerateUuidSlug_ReturnsUniqueValues()
    {
        var slug1 = _sut.GenerateUuidSlug();
        var slug2 = _sut.GenerateUuidSlug();

        Assert.NotEqual(slug1, slug2);
    }

    [Fact]
    public void GenerateUuidSlug_ReturnsVersion7Guid()
    {
        var result = _sut.GenerateUuidSlug();

        var guidPart = result.Replace("post-", "");
        var guid = Guid.Parse(guidPart);

        var guidString = guid.ToString();
        var versionChar = guidString[14];

        Assert.Equal('7', versionChar);
    }
}
```

```csharp src/MyBlog.Tests/Unit/TelemetrySerializationTests.cs
using System.Text.Json;
using MyBlog.Infrastructure.Telemetry;
using Xunit;

namespace MyBlog.Tests.Unit;

public class TelemetrySerializationTests
{
    [Fact]
    public void FileLogEntry_SourceGeneratedJson_MatchesReflectionOutput()
    {
        var entry = new FileLogEntry(
            "2026-10-02T12:00:00.0000000+00:00",
            "Information",
            "MyBlog.Category",
            "Message with <html> & \"quotes\" 👋",
            "0123456789abcdef0123456789abcdef",
            "0123456789abcdef",
            null);

        var expected = JsonSerializer.Serialize(
            new
            {
                entry.Timestamp,
                entry.Level,
                entry.Category,
                entry.Message,
                entry.TraceId,
                entry.SpanId,
                entry.Exception
            },
            new JsonSerializerOptions { WriteIndented = true });

        var actual = JsonSerializer.Serialize(entry, TelemetryJsonContext.Default.FileLogEntry);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SerializeAttributes_WithNull_ReturnsNull() =>
        Assert.Null(DatabaseLogExporter.SerializeAttributes(null));

    [Fact]
    public void SerializeAttributes_WithEmpty_ReturnsNull() =>
        Assert.Null(DatabaseLogExporter.SerializeAttributes([]));

    [Fact]
    public void SerializeAttributes_WithPrimitives_MatchesReflectionOutput()
    {
        var timestamp = new DateTime(2026, 10, 2, 12, 30, 45, DateTimeKind.Utc);
        var id = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        List<KeyValuePair<string, object?>> attributes =
        [
            new("text", "value <b>"),
            new("int", 42),
            new("long", 9_000_000_000L),
            new("bool", true),
            new("double", 1.5),
            new("none", null),
            new("when", timestamp),
            new("id", id),
            new("{OriginalFormat}", "Value {Int}")
        ];
        var dictionary = new Dictionary<string, object?>();
        foreach (var attribute in attributes)
        {
            dictionary[attribute.Key] = attribute.Value;
        }

        var expected = JsonSerializer.Serialize(dictionary);
        var actual = DatabaseLogExporter.SerializeAttributes(attributes);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SerializeAttributes_WithDuplicateKeys_LastValueWins()
    {
        List<KeyValuePair<string, object?>> attributes =
        [
            new("key", "first"),
            new("key", "second")
        ];

        Assert.Equal("{\"key\":\"second\"}", DatabaseLogExporter.SerializeAttributes(attributes));
    }

    [Fact]
    public void SerializeAttributes_WithComplexValue_WritesInvariantString()
    {
        List<KeyValuePair<string, object?>> attributes =
        [
            new("version", new Version(1, 2, 3))
        ];

        Assert.Equal("{\"version\":\"1.2.3\"}", DatabaseLogExporter.SerializeAttributes(attributes));
    }
}
```

```json src/MyBlog.Web/appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "Otlp": {
    "Endpoint": "https://api.honeycomb.io",
    "ApiKey": "",
    "Protocol": "HttpProtobuf"
  }
}
```

```json src/MyBlog.Web/appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=myblog.db"
  },
  "Authentication": {
    "SessionTimeoutMinutes": 30,
    "DefaultAdminPassword": "ChangeMe123!"
  },
  "Blazor": {
    "MaximumReceiveMessageSizeBytes": 1048576
  },
  "Telemetry": {
    "RetentionDays": 30,
    "MaxDatabaseRows": 50000,
    "MaxLogFileMegabytes": 100,
    "EnableFileLogging": true,
    "EnableDatabaseLogging": true
  },
  "Otlp": {
    "Endpoint": "",
    "ApiKey": "",
    "Protocol": "HttpProtobuf"
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

```razor src/MyBlog.Web/Components/App.razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="theme-color" content="#f8f9fa" />
    <base href="/" />
    <link rel="stylesheet" href="@Assets["css/site.css"]" />
    <link rel="stylesheet" href="@Assets["MyBlog.Web.styles.css"]" />
    <link rel="alternate" type="application/rss+xml" title="RSS Feed" href="/feed.xml" />
    <HeadOutlet @rendermode="InteractiveServer" />
    <script src="@Assets["js/site.js"]"></script>
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="@Assets["_framework/blazor.web.js"]"></script>
</body>
</html>
```

```razor src/MyBlog.Web/Components/_Imports.razor
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.Extensions.Configuration
@using Microsoft.JSInterop
@using MyBlog.Web
@using MyBlog.Web.Components
@using MyBlog.Web.Components.Layout
@using MyBlog.Web.Components.Pages
@using MyBlog.Web.Components.Shared
@using MyBlog.Core.Constants
@using MyBlog.Core.Interfaces
@using MyBlog.Core.Models
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using Microsoft.AspNetCore.SignalR.Client
```

```razor src/MyBlog.Web/Components/Layout/MainLayout.razor
@inherits LayoutComponentBase
@inject IConfiguration Configuration

<div class="layout" data-blazor-interactive="@(RendererInfo.IsInteractive ? "true" : "false")">
    <a href="#main-content" class="skip-link">Skip to main content</a>

    <header class="header">
        <div class="container">
            <a href="/" class="logo">@(Configuration["Application:Title"] ?? "MyBlog")</a>
            <nav class="nav" aria-label="Main navigation">
                <a href="/">Home</a>
                <a href="/about">About</a>

                <AuthorizeView>
                    <Authorized>
                        <a href="/admin">Admin</a>
                        <form class="logout-form" method="post" action="/logout">
                            <AntiforgeryToken />
                            <button type="submit">Logout</button>
                        </form>
                    </Authorized>
                    <NotAuthorized>
                        <a href="/login">Login</a>
                    </NotAuthorized>
                </AuthorizeView>

                <ThemeSwitcher />
            </nav>
        </div>
    </header>

    <main id="main-content" class="main" role="main">
        <div class="container">
            @Body
        </div>
    </main>

    <Footer />
</div>
```

```razor src/MyBlog.Web/Components/NotFound.razor
@page "/not-found"
@using MyBlog.Web.Components.Layout

<LayoutView Layout="typeof(MainLayout)">
    <PageTitle>Page Not Found</PageTitle>
    <h1>Page Not Found</h1>
    <p>Sorry, the page you requested could not be found.</p>
    <a href="/">← Back to Home</a>
</LayoutView>
```

````razor src/MyBlog.Web/Components/Pages/About.razor
@page "/about"
@inject IConfiguration Configuration
@inject IPostRepository PostRepository

<PageTitle>About - @(Configuration["Application:Title"] ?? "MyBlog")</PageTitle>

<article class="about-page">
    <header class="about-header">
        <h1>About @(Configuration["Application:Title"] ?? "MyBlog")</h1>
        <p class="about-tagline">A lightweight, self-hosted blogging platform built with modern .NET technologies</p>
        <div class="reader-info">
            <ReaderBadge Slug="@TrackingSlug" />
        </div>
    </header>

    <section class="about-section">
        <h2>Overview</h2>
        <p>
            Welcome to <strong>@(Configuration["Application:Title"] ?? "MyBlog")</strong>, a powerful yet lightweight
            content management system and blogging platform built entirely with <strong>.NET 10</strong> and
            <strong>Blazor Server</strong>. This application represents a modern approach to self-hosted blogging,
            combining the performance and type-safety of C# with the interactive capabilities of Blazor's
            component-based architecture.
        </p>
        <p>
            MyBlog follows <strong>Clean Architecture</strong> principles, ensuring a clear separation of concerns
            between the domain logic, infrastructure, and presentation layers. This architectural approach makes
            the codebase maintainable, testable, and extensible for future enhancements.
        </p>
    </section>

    <section class="about-section">
        <h2>Architecture</h2>
        <p>
            The application is organized into a multi-project solution structure that follows industry best practices
            for enterprise-grade .NET applications:
        </p>

        <div class="architecture-grid">
            <div class="arch-card">
                <h3>MyBlog.Core</h3>
                <p class="arch-subtitle">Domain Layer</p>
                <p>
                    Contains the heart of the application: domain models, interfaces, and business logic services.
                    This layer has zero dependencies on external frameworks or infrastructure concerns, ensuring
                    that the core business rules remain portable and testable in isolation.
                </p>
                <div class="arch-contents">
                    <strong>Contains:</strong>
                    <ul>
                        <li><code>Models/</code> — Post, User, Image, TelemetryLog, DTOs</li>
                        <li><code>Interfaces/</code> — Repository and service contracts</li>
                        <li><code>Services/</code> — MarkdownService, SlugService</li>
                        <li><code>Constants/</code> — AppConstants (auth cookie name, roles, limits)</li>
                    </ul>
                </div>
            </div>

            <div class="arch-card">
                <h3>MyBlog.Infrastructure</h3>
                <p class="arch-subtitle">Data Access Layer</p>
                <p>
                    Implements the repository interfaces defined in Core using Entity Framework Core with SQLite.
                    This layer handles all database operations, authentication services, and external integrations.
                </p>
                <div class="arch-contents">
                    <strong>Contains:</strong>
                    <ul>
                        <li><code>Data/</code> — BlogDbContext, DatabasePathResolver, DatabaseSchemaUpdater</li>
                        <li><code>Repositories/</code> — EF Core implementations</li>
                        <li><code>Services/</code> — AuthService, PasswordService, ReaderTrackingService, ImageDimensionService</li>
                        <li><code>Telemetry/</code> — OpenTelemetry exporters</li>
                    </ul>
                </div>
            </div>

            <div class="arch-card">
                <h3>MyBlog.Web</h3>
                <p class="arch-subtitle">Presentation Layer</p>
                <p>
                    The Blazor Server application containing all UI components, pages, layouts, and middleware.
                    Routes render in Interactive Server mode after a prerendered first response; login is a
                    static form that posts to a minimal API endpoint.
                </p>
                <div class="arch-contents">
                    <strong>Contains:</strong>
                    <ul>
                        <li><code>Components/Pages/</code> — Razor pages (Home, PostDetail, About, Login)</li>
                        <li><code>Components/Pages/Admin/</code> — Dashboard, PostEditor, UserEditor, ImageManager</li>
                        <li><code>Components/Shared/</code> — Reusable components (PostCard, Pagination, ReaderBadge)</li>
                        <li><code>Middleware/</code> — LoginRateLimitMiddleware</li>
                        <li><code>Endpoints/</code> — RSS feed at <code>/feed.xml</code></li>
                    </ul>
                </div>
            </div>

            <div class="arch-card">
                <h3>MyBlog.Tests</h3>
                <p class="arch-subtitle">Testing Layer</p>
                <p>
                    Comprehensive test suite using <strong>xUnit v3</strong> with both unit and integration tests.
                    The tests cover core services, repositories, and middleware functionality with high code coverage.
                </p>
                <div class="arch-contents">
                    <strong>Contains:</strong>
                    <ul>
                        <li><code>Unit/</code> — MarkdownServiceTests, SlugServiceTests, PasswordServiceTests</li>
                        <li><code>Integration/</code> — AuthServiceTests, PostRepositoryTests, TelemetryCleanupTests</li>
                        <li>LoginRateLimitMiddlewareTests with injectable delay functions</li>
                    </ul>
                </div>
            </div>
        </div>
    </section>

    <section class="about-section">
        <h2>Technology Stack</h2>
        <div class="tech-grid">
            <div class="tech-category">
                <h3>Backend Framework</h3>
                <ul>
                    <li><strong>.NET 10</strong> — Current runtime with latest features and performance improvements</li>
                    <li><strong>Blazor Server</strong> — Component-based UI with real-time SignalR connectivity</li>
                    <li><strong>ASP.NET Core</strong> — Web framework with middleware pipeline</li>
                    <li><strong>Entity Framework Core</strong> — ORM; schema via EnsureCreated plus an idempotent updater, no migrations</li>
                </ul>
            </div>

            <div class="tech-category">
                <h3>Database</h3>
                <ul>
                    <li><strong>SQLite</strong> — Lightweight, serverless database engine</li>
                    <li><strong>XDG-Compliant Paths</strong> — Platform-specific data directories</li>
                    <li><strong>Image Storage</strong> — Binary data stored directly in database</li>
                    <li><strong>Telemetry Logs</strong> — Structured logging with retention policies</li>
                </ul>
            </div>

            <div class="tech-category">
                <h3>Authentication & Security</h3>
                <ul>
                    <li><strong>Cookie Authentication</strong> — Secure session management</li>
                    <li><strong>ASP.NET Identity PasswordHasher</strong> — Industry-standard hashing</li>
                    <li><strong>Rate Limiting Middleware</strong> — Progressive delays, never blocks</li>
                    <li><strong>Slug Collision Prevention</strong> — Automatic unique slug generation</li>
                </ul>
            </div>

            <div class="tech-category">
                <h3>Observability</h3>
                <ul>
                    <li><strong>OpenTelemetry</strong> — Distributed tracing and metrics</li>
                    <li><strong>File Log Exporter</strong> — JSON-formatted log files with rotation</li>
                    <li><strong>Database Log Exporter</strong> — Queryable telemetry storage</li>
                    <li><strong>Console Exporter</strong> — Development-time debugging</li>
                </ul>
            </div>

            <div class="tech-category">
                <h3>Testing</h3>
                <ul>
                    <li><strong>xUnit v3</strong> — Modern testing framework</li>
                    <li><strong>In-Memory Database</strong> — Fast integration tests</li>
                    <li><strong>Dependency Injection</strong> — Testable delay functions</li>
                    <li><strong>Cross-Platform CI</strong> — Windows, Linux, macOS matrix</li>
                </ul>
            </div>

            <div class="tech-category">
                <h3>DevOps & Deployment</h3>
                <ul>
                    <li><strong>GitHub Actions</strong> — Automated CI/CD pipeline</li>
                    <li><strong>WebDeploy</strong> — IIS deployment with AppOffline rule</li>
                    <li><strong>PowerShell Scripts</strong> — No third-party deployment actions</li>
                    <li><strong>Two Deploy Targets</strong> — Same package deployed to two IIS sites</li>
                </ul>
            </div>
        </div>
    </section>

    <section class="about-section">
        <h2>Core Features</h2>

        <div class="feature-grid">
            <div class="feature-card">
                <div class="feature-icon">📝</div>
                <h3>Markdown-Based Content</h3>
                <p>
                    Write posts in Markdown with a custom-built renderer that supports headings (h1-h6),
                    <strong>bold</strong> and <em>italic</em> text, links, images, fenced code blocks with
                    syntax preservation, blockquotes, horizontal rules, and both unordered and ordered lists.
                </p>
                <div class="feature-details">
                    <strong>Supported Syntax:</strong>
                    <code># Headings</code>, <code>**bold**</code>, <code>*italic*</code>,
                    <code>[links](url)</code>, <code>![images](url)</code>, <code>`inline code`</code>,
                    <code>```code blocks```</code>, <code>> blockquotes</code>, <code>- unordered lists</code>,
                    <code>1. ordered lists</code>, <code>---</code> horizontal rules
                </div>
            </div>

            <div class="feature-card">
                <div class="feature-icon">🖼️</div>
                <h3>Image Management</h3>
                <p>
                    Upload and manage images directly through the admin interface. Images are stored as binary
                    data in the SQLite database, eliminating file system dependencies and simplifying backups.
                </p>
                <div class="feature-details">
                    <strong>Specifications:</strong>
                    Max size: 5MB per image. Supported formats: JPEG, PNG, GIF, WebP.
                    Reference in Markdown: <code>/api/images/{'{'}id{'}'}</code>
                </div>
            </div>

            <div class="feature-card">
                <div class="feature-icon">👥</div>
                <h3>Multi-User Support</h3>
                <p>
                    Create, edit, and manage multiple user accounts with display names and email addresses.
                    Each post tracks its author, and administrators can reset passwords for other users.
                </p>
                <div class="feature-details">
                    <strong>User Management:</strong>
                    User list at <code>/admin/users</code>, create new users, edit profiles, reset passwords,
                    change your own password at <code>/admin/change-password</code>
                </div>
            </div>

            <div class="feature-card">
                <div class="feature-icon">📊</div>
                <h3>Real-Time Reader Tracking</h3>
                <p>
                    See how many people are currently reading each post with live-updating reader counts.
                    Uses a thread-safe <code>ConcurrentDictionary</code> and event-driven updates via
                    SignalR for instant UI refresh.
                </p>
                <div class="feature-details">
                    <strong>Implementation:</strong>
                    <code>IReaderTrackingService</code> with JoinPost/LeavePost lifecycle;
                    <code>ReaderHub</code> broadcasts <code>UpdateCount</code> to each post's group
                </div>
            </div>

            <div class="feature-card">
                <div class="feature-icon">🔒</div>
                <h3>Security Features</h3>
                <p>
                    Enterprise-grade security with rate limiting that progressively delays login attempts
                    (1s, 2s, 4s... up to 30s) but never completely blocks users. Passwords are hashed using
                    ASP.NET Core Identity's PasswordHasher (PBKDF2-HMAC-SHA512, 100,000 iterations).
                </p>
                <div class="feature-details">
                    <strong>Rate Limiting:</strong>
                    15-minute window, delays after 5 attempts, exponential backoff formula:
                    <code>2^(attempts-5)</code> seconds, capped at 30s
                </div>
            </div>

            <div class="feature-card">
                <div class="feature-icon">🔗</div>
                <h3>Smart Slug Generation</h3>
                <p>
                    Automatically generates URL-friendly slugs from post titles with Unicode normalization,
                    special character removal, and a UUIDv7 fallback (<code>post-&lt;uuid&gt;</code>) when the
                    title slug is empty or already taken.
                </p>
                <div class="feature-details">
                    <strong>Algorithm:</strong>
                    Normalize Unicode → lowercase → replace spaces/underscores with hyphens →
                    remove non-alphanumeric → collapse multiple hyphens → trim edges
                </div>
            </div>
        </div>
    </section>

    <section class="about-section">
        <h2>Admin Dashboard</h2>
        <p>
            The administrative interface provides comprehensive content management capabilities:
        </p>

        <div class="admin-features">
            <div class="admin-feature">
                <h3>📋 Dashboard (<code>/admin</code>)</h3>
                <p>
                    Overview of your blog with total post count and the 5 most recently updated posts.
                    Quick links to all management areas with status indicators.
                </p>
            </div>

            <div class="admin-feature">
                <h3>📝 Post Management (<code>/admin/posts</code>)</h3>
                <p>
                    View all posts with title, author, and publish status. Create new posts with
                    a live Markdown preview, edit existing content, toggle publish/draft status,
                    and delete posts.
                </p>
            </div>

            <div class="admin-feature">
                <h3>👤 User Management (<code>/admin/users</code>)</h3>
                <p>
                    List all registered users, create new accounts with usernames, emails, and display names.
                    Edit user profiles and reset passwords for other administrators.
                </p>
            </div>

            <div class="admin-feature">
                <h3>🖼️ Image Library (<code>/admin/images</code>)</h3>
                <p>
                    Upload images with a file picker, browse thumbnails alongside the image URL to
                    paste into Markdown, and delete unused images.
                </p>
            </div>

            <div class="admin-feature">
                <h3>🔐 Password Change (<code>/admin/change-password</code>)</h3>
                <p>
                    Securely update your password with current password verification.
                    Minimum 8 characters required, with confirmation field to prevent typos.
                </p>
            </div>
        </div>
    </section>

    <section class="about-section">
        <h2>Configuration</h2>
        <p>
            MyBlog is highly configurable through <code>appsettings.json</code> and environment variables:
        </p>

        <div class="config-section">
            <h3>Application Settings</h3>
            <div class="config-table">
                <div class="config-row">
                    <code>Application:Title</code>
                    <span>Blog title displayed in header and page titles</span>
                    <em>Default: "MyBlog"</em>
                </div>
                <div class="config-row">
                    <code>Application:PostsPerPage</code>
                    <span>Number of posts per page on the homepage</span>
                    <em>Default: 10</em>
                </div>
                <div class="config-row">
                    <code>Application:RequireHttps</code>
                    <span>Force HTTPS for authentication cookies</span>
                    <em>Default: false</em>
                </div>
                <div class="config-row">
                    <code>Application:GitForgeUrl</code>
                    <span>Link to source code repository in footer</span>
                    <em>Default: GitHub URL</em>
                </div>
            </div>
        </div>

        <div class="config-section">
            <h3>Authentication Settings</h3>
            <div class="config-table">
                <div class="config-row">
                    <code>Authentication:SessionTimeoutMinutes</code>
                    <span>How long before the session expires</span>
                    <em>Default: 30</em>
                </div>
                <div class="config-row">
                    <code>Authentication:DefaultAdminPassword</code>
                    <span>Initial password for first admin user</span>
                    <em>Default: "ChangeMe123!"</em>
                </div>
                <div class="config-row">
                    <code>MYBLOG_ADMIN_PASSWORD</code>
                    <span>Environment variable override (only on first run)</span>
                    <em>Takes precedence over config file</em>
                </div>
            </div>
        </div>

        <div class="config-section">
            <h3>Telemetry Settings</h3>
            <div class="config-table">
                <div class="config-row">
                    <code>Telemetry:RetentionDays</code>
                    <span>Days to keep database and file logs; cleanup runs hourly</span>
                    <em>Default and maximum: 30</em>
                </div>
                <div class="config-row">
                    <code>Telemetry:MaxDatabaseRows</code>
                    <span>Newest database log rows kept regardless of age</span>
                    <em>Default: 50000</em>
                </div>
                <div class="config-row">
                    <code>Telemetry:MaxLogFileMegabytes</code>
                    <span>Total size budget for JSON log files</span>
                    <em>Default: 100</em>
                </div>
                <div class="config-row">
                    <code>Telemetry:EnableFileLogging</code>
                    <span>Write logs to JSON files</span>
                    <em>Default: true</em>
                </div>
                <div class="config-row">
                    <code>Telemetry:EnableDatabaseLogging</code>
                    <span>Store logs in SQLite database</span>
                    <em>Default: true</em>
                </div>
            </div>
        </div>

        <div class="config-section">
            <h3>Database Locations (XDG-Compliant)</h3>
            <div class="config-table">
                <div class="config-row">
                    <code>Linux</code>
                    <span><code>~/.local/share/MyBlog/myblog.db</code></span>
                    <em>Follows XDG_DATA_HOME</em>
                </div>
                <div class="config-row">
                    <code>macOS</code>
                    <span><code>~/Library/Application Support/MyBlog/myblog.db</code></span>
                    <em>Standard macOS location</em>
                </div>
                <div class="config-row">
                    <code>Windows</code>
                    <span><code>%LOCALAPPDATA%\MyBlog\myblog.db</code></span>
                    <em>Per-user application data</em>
                </div>
            </div>
        </div>
    </section>

    <section class="about-section">
        <h2>Deployment</h2>
        <p>
            MyBlog includes a complete CI/CD pipeline with GitHub Actions for automated testing and deployment:
        </p>

        <div class="deployment-section">
            <h3>GitHub Actions Workflow</h3>
            <ol>
                <li><strong>Build & Test</strong> — Runs on Windows, Linux, and macOS simultaneously</li>
                <li><strong>Restore</strong> — <code>dotnet restore src/MyBlog.slnx</code></li>
                <li><strong>Build</strong> — <code>dotnet build -c Release</code></li>
                <li><strong>Test</strong> — <code>dotnet run --project src/MyBlog.Tests/MyBlog.Tests.csproj</code> (Microsoft.Testing.Platform)</li>
                <li><strong>Publish</strong> — Framework-dependent publish for win-x86</li>
                <li><strong>Deploy</strong> — WebDeploy to IIS with AppOffline rule</li>
            </ol>
        </div>

        <div class="deployment-section">
            <h3>Required GitHub Secrets</h3>
            <div class="config-table">
                <div class="config-row">
                    <code>WEBSITE_NAME</code>
                    <span>IIS site name for WebDeploy</span>
                    <em>e.g., "MyBlog"</em>
                </div>
                <div class="config-row">
                    <code>SERVER_COMPUTER_NAME</code>
                    <span>Server hostname</span>
                    <em>e.g., "myserver.example.com"</em>
                </div>
                <div class="config-row">
                    <code>SERVER_USERNAME</code>
                    <span>WebDeploy authentication user</span>
                    <em>Deploy account username</em>
                </div>
                <div class="config-row">
                    <code>SERVER_PASSWORD</code>
                    <span>WebDeploy authentication password</span>
                    <em>Stored as secret</em>
                </div>
            </div>
        </div>

        <div class="deployment-section">
            <h3>WebDeploy Configuration</h3>
            <p>
                The deployment uses key features to ensure zero-downtime deployments:
            </p>
            <ul>
                <li><strong>AppOffline Rule</strong> — Creates <code>app_offline.htm</code> to gracefully stop the application</li>
                <li><strong>DoNotDeleteRule</strong> — Preserves existing files not in the deployment package</li>
                <li><strong>Retry Logic</strong> — 3 retry attempts with 3-second intervals for transient failures</li>
                <li><strong>File Locking Prevention</strong> — Solves ERROR_FILE_IN_USE during active deployments</li>
            </ul>
        </div>
    </section>

    <section class="about-section">
        <h2>Design Principles</h2>
        <div class="principles-grid">
            <div class="principle">
                <h3>🎯 Zero External Dependencies</h3>
                <p>
                    No npm, no Node.js, no CSS frameworks. All styling is custom CSS using CSS variables
                    for theming. Besides Blazor's <code>blazor.web.js</code> runtime, the only JavaScript is a
                    small vanilla <code>site.js</code> for theming and sharing.
                </p>
            </div>

            <div class="principle">
                <h3>📦 Self-Contained</h3>
                <p>
                    Everything needed runs from a single deployable unit. SQLite eliminates the need
                    for external database servers. Images are stored in the database, not the filesystem.
                </p>
            </div>

            <div class="principle">
                <h3>🔧 Centralized Package Management</h3>
                <p>
                    Uses <code>Directory.Packages.props</code> for consistent NuGet package versions
                    across all projects. The modern <code>.slnx</code> solution format keeps things clean.
                </p>
            </div>

            <div class="principle">
                <h3>⚡ Performance First</h3>
                <p>
                    Pages are prerendered for a fast first paint, static assets are served precompressed
                    and fingerprinted via <code>MapStaticAssets</code>, and AsNoTracking queries
                    minimize EF Core overhead for read operations.
                </p>
            </div>

            <div class="principle">
                <h3>🧪 Testability</h3>
                <p>
                    All services use interface-based dependency injection. The rate limiting middleware
                    accepts an injectable delay function for instant test execution instead of real waits.
                </p>
            </div>

            <div class="principle">
                <h3>🌍 Cross-Platform</h3>
                <p>
                    Runs identically on Windows, Linux, and macOS. XDG-compliant paths ensure data
                    is stored in appropriate locations for each operating system.
                </p>
            </div>
        </div>
    </section>

    <section class="about-section">
        <h2>Data Models</h2>
        <p>
            The core domain models represent the fundamental entities in the blogging system:
        </p>

        <div class="model-grid">
            <div class="model-card">
                <h3>Post</h3>
                <p>A blog post with full content management capabilities.</p>
                <div class="model-fields">
                    <code>Id</code> (Guid), <code>Title</code>, <code>Slug</code>, <code>Content</code>,
                    <code>Summary</code>, <code>AuthorId</code>, <code>CreatedAtUtc</code>,
                    <code>UpdatedAtUtc</code>, <code>PublishedAtUtc</code>, <code>IsPublished</code>
                </div>
            </div>

            <div class="model-card">
                <h3>User</h3>
                <p>An authenticated user who can create and manage content.</p>
                <div class="model-fields">
                    <code>Id</code> (Guid), <code>Username</code>, <code>PasswordHash</code>,
                    <code>Email</code>, <code>DisplayName</code>, <code>CreatedAtUtc</code>
                </div>
            </div>

            <div class="model-card">
                <h3>Image</h3>
                <p>An uploaded image stored as binary data in the database.</p>
                <div class="model-fields">
                    <code>Id</code> (Guid), <code>FileName</code>, <code>ContentType</code>,
                    <code>Data</code> (byte[]), <code>UploadedByUserId</code>, <code>UploadedAtUtc</code>,
                    <code>PostId</code> (optional)
                </div>
            </div>

            <div class="model-card">
                <h3>TelemetryLog</h3>
                <p>Structured log entries for observability and debugging.</p>
                <div class="model-fields">
                    <code>Id</code> (int), <code>TimestampUtc</code>, <code>Level</code>,
                    <code>Category</code>, <code>Message</code>, <code>Exception</code>,
                    <code>TraceId</code>, <code>SpanId</code>, <code>Properties</code> (JSON)
                </div>
            </div>
        </div>
    </section>

    <section class="about-section">
        <h2>API Endpoints</h2>
        <p>
            While primarily a Blazor Server application, MyBlog exposes a few HTTP endpoints:
        </p>

        <div class="api-table">
            <div class="api-row">
                <code>GET /api/images/{'{'}id{'}'}</code>
                <span>Retrieve an image by its GUID for embedding in posts</span>
            </div>
            <div class="api-row">
                <code>POST /logout</code>
                <span>Sign out the current user (requires authorization)</span>
            </div>
            <div class="api-row">
                <code>POST /account/login</code>
                <span>Form-based authentication with rate limiting</span>
            </div>
            <div class="api-row">
                <code>GET /feed.xml</code>
                <span>RSS 2.0 feed of the 20 most recent published posts</span>
            </div>
        </div>
    </section>

    <section class="about-section">
        <h2>Quick Start</h2>
        <div class="quickstart">
            <h3>Prerequisites</h3>
            <p><a href="https://dotnet.microsoft.com/download/dotnet/10.0" target="_blank">.NET 10 SDK</a> or later</p>

            <h3>Running Locally</h3>
            <pre><code># Clone the repository
git clone https://github.com/kusl/dotnetcms.git
cd dotnetcms/src

# Restore and run
dotnet restore MyBlog.slnx
cd MyBlog.Web
dotnet run</code></pre>

            <h3>Default Credentials</h3>
            <p>
                <strong>Username:</strong> <code>admin</code><br />
                <strong>Password:</strong> <code>ChangeMe123!</code> (or set <code>MYBLOG_ADMIN_PASSWORD</code> environment variable before first run)
            </p>

            <div class="warning-box">
                <strong>⚠️ Important:</strong> The default password is only used when creating the initial admin user.
                Once the user exists, you must change the password through the website at <code>/admin/change-password</code>.
            </div>
        </div>
    </section>

    <section class="about-section stats-section">
        <h2>Current Statistics</h2>
        <div class="stats-grid">
            <div class="stat-card">
                <div class="stat-value">@_postCount</div>
                <div class="stat-label">Total Posts</div>
            </div>
            <div class="stat-card">
                <div class="stat-value">@_publishedCount</div>
                <div class="stat-label">Published</div>
            </div>
            <div class="stat-card">
                <div class="stat-value">@(_postCount - _publishedCount)</div>
                <div class="stat-label">Drafts</div>
            </div>
        </div>
    </section>

    <section class="about-section">
        <h2>Open Source</h2>
        <p>
            MyBlog is open source software. The complete source code, including this page, is available
            on GitHub. Contributions, bug reports, and feature requests are welcome!
        </p>
        <p>
            <a href="@(Configuration["Application:GitForgeUrl"] ?? "https://github.com/kusl/dotnetcms")"
               target="_blank"
               class="github-link">
                View on GitHub →
            </a>
        </p>
    </section>

    <section class="about-footer">
        <p>
            Built with ❤️ using .NET 10 and Blazor Server
        </p>
        <p class="version-info">
            <small>
                Runtime: @System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription |
                OS: @System.Runtime.InteropServices.RuntimeInformation.OSDescription
            </small>
        </p>
    </section>
</article>

@code {
    private const string TrackingSlug = "__about_page__";
    private int _postCount;
    private int _publishedCount;

    protected override async Task OnInitializedAsync()
    {
        _publishedCount = await PostRepository.GetPublishedCountAsync();
        _postCount = await PostRepository.GetCountAsync();
    }
}
````

```css src/MyBlog.Web/Components/Pages/About.razor.css
.about-page {
    max-width: 1200px;
    margin: 0 auto;
    padding: 0 var(--spacing);
}

.about-header {
    text-align: center;
    padding: calc(var(--spacing) * 2) 0;
    border-bottom: 1px solid var(--color-border);
    margin-bottom: calc(var(--spacing) * 2);
}

.about-header h1 {
    font-size: 2.5rem;
    margin-bottom: 0.5rem;
    color: var(--color-primary);
}

.about-tagline {
    font-size: 1.25rem;
    color: var(--color-text-muted);
    margin-bottom: 1rem;
}

.reader-info {
    display: flex;
    justify-content: center;
    margin-top: 1rem;
}

.about-section {
    margin-bottom: calc(var(--spacing) * 3);
    padding-bottom: calc(var(--spacing) * 2);
    border-bottom: 1px solid var(--color-border);
}

.about-section:last-of-type {
    border-bottom: none;
}

.about-section h2 {
    font-size: 1.75rem;
    margin-bottom: 1rem;
    color: var(--color-text);
    position: relative;
    padding-bottom: 0.5rem;
}

.about-section h2::after {
    content: '';
    position: absolute;
    bottom: 0;
    left: 0;
    width: 60px;
    height: 3px;
    background: var(--color-primary);
    border-radius: 2px;
}

.about-section p {
    line-height: 1.8;
    margin-bottom: 1rem;
}

.architecture-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: 1.5rem;
    margin-top: 1.5rem;
}

.arch-card {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: 1.5rem;
    transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.arch-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.arch-card h3 {
    color: var(--color-primary);
    margin-bottom: 0.25rem;
    font-size: 1.25rem;
}

.arch-subtitle {
    font-size: 0.875rem;
    color: var(--color-text-muted);
    margin-bottom: 1rem;
    font-weight: 500;
}

.arch-contents {
    margin-top: 1rem;
    padding-top: 1rem;
    border-top: 1px solid var(--color-border);
    font-size: 0.9rem;
}

.arch-contents ul {
    margin: 0.5rem 0 0 1.25rem;
    padding: 0;
}

.arch-contents li {
    margin-bottom: 0.25rem;
}

.arch-contents code {
    background: var(--color-bg);
    padding: 0.125rem 0.375rem;
    border-radius: 3px;
    font-size: 0.85em;
}

.tech-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: 1.5rem;
    margin-top: 1.5rem;
}

.tech-category {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: 1.25rem;
}

.tech-category h3 {
    font-size: 1.1rem;
    margin-bottom: 0.75rem;
    color: var(--color-primary);
    border-bottom: 2px solid var(--color-primary);
    padding-bottom: 0.5rem;
}

.tech-category ul {
    margin: 0;
    padding: 0;
    list-style: none;
}

.tech-category li {
    padding: 0.5rem 0;
    border-bottom: 1px solid var(--color-border);
    font-size: 0.95rem;
}

.tech-category li:last-child {
    border-bottom: none;
}

.feature-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    gap: 1.5rem;
    margin-top: 1.5rem;
}

.feature-card {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: 1.5rem;
    transition: transform 0.2s ease;
}

.feature-card:hover {
    transform: translateY(-2px);
}

.feature-icon {
    font-size: 2.5rem;
    margin-bottom: 0.75rem;
}

.feature-card h3 {
    font-size: 1.15rem;
    margin-bottom: 0.75rem;
    color: var(--color-text);
}

.feature-details {
    margin-top: 1rem;
    padding-top: 1rem;
    border-top: 1px solid var(--color-border);
    font-size: 0.875rem;
    color: var(--color-text-muted);
}

.feature-details code {
    background: var(--color-bg);
    padding: 0.125rem 0.25rem;
    border-radius: 3px;
    font-size: 0.9em;
}

.admin-features {
    display: grid;
    gap: 1rem;
    margin-top: 1.5rem;
}

.admin-feature {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: 1.25rem;
}

.admin-feature h3 {
    font-size: 1.1rem;
    margin-bottom: 0.5rem;
    color: var(--color-text);
}

.admin-feature code {
    background: var(--color-bg);
    padding: 0.125rem 0.375rem;
    border-radius: 3px;
    font-size: 0.85em;
    color: var(--color-primary);
}

.config-section {
    margin-top: 1.5rem;
}

.config-section h3 {
    font-size: 1.15rem;
    margin-bottom: 1rem;
    color: var(--color-text);
}

.config-table {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    overflow: hidden;
}

.config-row {
    display: grid;
    grid-template-columns: minmax(200px, 1fr) 2fr auto;
    gap: 1rem;
    padding: 1rem;
    border-bottom: 1px solid var(--color-border);
    align-items: center;
}

.config-row:last-child {
    border-bottom: none;
}

.config-row code {
    background: var(--color-bg);
    padding: 0.25rem 0.5rem;
    border-radius: 3px;
    font-size: 0.85rem;
    color: var(--color-primary);
    word-break: break-all;
}

.config-row em {
    font-size: 0.85rem;
    color: var(--color-text-muted);
    white-space: nowrap;
}

.deployment-section {
    margin-top: 1.5rem;
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: 1.5rem;
}

.deployment-section h3 {
    font-size: 1.15rem;
    margin-bottom: 1rem;
    color: var(--color-primary);
}

.deployment-section ol {
    margin: 0;
    padding-left: 1.5rem;
}

.deployment-section li {
    margin-bottom: 0.5rem;
}

.deployment-section ul {
    margin: 0.5rem 0 0 0;
    padding-left: 1.5rem;
}

.principles-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: 1.5rem;
    margin-top: 1.5rem;
}

.principle {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: 1.5rem;
}

.principle h3 {
    font-size: 1.1rem;
    margin-bottom: 0.75rem;
    color: var(--color-text);
}

.principle p {
    font-size: 0.95rem;
    margin: 0;
}

.model-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: 1.5rem;
    margin-top: 1.5rem;
}

.model-card {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: 1.5rem;
}

.model-card h3 {
    color: var(--color-primary);
    margin-bottom: 0.5rem;
    font-size: 1.2rem;
}

.model-fields {
    margin-top: 1rem;
    padding-top: 1rem;
    border-top: 1px solid var(--color-border);
    font-size: 0.875rem;
    color: var(--color-text-muted);
}

.model-fields code {
    background: var(--color-bg);
    padding: 0.125rem 0.25rem;
    border-radius: 3px;
    margin-right: 0.25rem;
}

.api-table {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    overflow: hidden;
    margin-top: 1rem;
}

.api-row {
    display: grid;
    grid-template-columns: minmax(200px, auto) 1fr;
    gap: 1rem;
    padding: 1rem;
    border-bottom: 1px solid var(--color-border);
    align-items: center;
}

.api-row:last-child {
    border-bottom: none;
}

.api-row code {
    background: var(--color-bg);
    padding: 0.375rem 0.625rem;
    border-radius: 3px;
    font-size: 0.9rem;
    color: var(--color-primary);
    font-weight: 500;
}

.quickstart {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: 1.5rem;
    margin-top: 1rem;
}

.quickstart h3 {
    font-size: 1.1rem;
    margin-top: 1.5rem;
    margin-bottom: 0.75rem;
    color: var(--color-primary);
}

.quickstart h3:first-child {
    margin-top: 0;
}

.quickstart pre {
    background: var(--color-bg);
    padding: 1rem;
    border-radius: var(--radius);
    overflow-x: auto;
    margin: 0.5rem 0;
}

.quickstart code {
    font-size: 0.9rem;
    line-height: 1.6;
}

.warning-box {
    background: #fef3cd;
    border: 1px solid #ffc107;
    border-radius: var(--radius);
    padding: 1rem;
    margin-top: 1rem;
    color: #856404;
}

.stats-section {
    text-align: center;
}

.stats-grid {
    display: flex;
    justify-content: center;
    gap: 2rem;
    flex-wrap: wrap;
    margin-top: 1.5rem;
}

.stat-card {
    background: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: 1.5rem 2.5rem;
    min-width: 140px;
}

.stat-value {
    font-size: 2.5rem;
    font-weight: bold;
    color: var(--color-primary);
}

.stat-label {
    font-size: 0.95rem;
    color: var(--color-text-muted);
    margin-top: 0.25rem;
}

.github-link {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    background: var(--color-primary);
    color: white;
    padding: 0.75rem 1.5rem;
    border-radius: var(--radius);
    text-decoration: none;
    font-weight: 500;
    transition: background 0.2s ease, transform 0.2s ease;
}

.github-link:hover {
    background: var(--color-primary-dark, #1a5490);
    transform: translateY(-1px);
    color: white;
    text-decoration: none;
}

.about-footer {
    text-align: center;
    padding: calc(var(--spacing) * 2) 0;
    margin-top: calc(var(--spacing) * 2);
    border-top: 1px solid var(--color-border);
}

.about-footer p {
    margin: 0.5rem 0;
}

.version-info {
    color: var(--color-text-muted);
}

@media (max-width: 768px) {
    .about-header h1 {
        font-size: 2rem;
    }

    .about-tagline {
        font-size: 1.1rem;
    }

    .about-section h2 {
        font-size: 1.5rem;
    }

    .config-row {
        grid-template-columns: 1fr;
        gap: 0.5rem;
    }

    .config-row em {
        white-space: normal;
    }

    .api-row {
        grid-template-columns: 1fr;
        gap: 0.5rem;
    }

    .stats-grid {
        gap: 1rem;
    }

    .stat-card {
        padding: 1rem 1.5rem;
        min-width: 100px;
    }

    .stat-value {
        font-size: 2rem;
    }
}

@media (max-width: 480px) {
    .about-page {
        padding: 0 0.75rem;
    }

    .arch-card,
    .feature-card,
    .principle,
    .model-card {
        padding: 1rem;
    }

    .quickstart {
        padding: 1rem;
    }

    .quickstart pre {
        padding: 0.75rem;
        font-size: 0.8rem;
    }
}
```

```razor src/MyBlog.Web/Components/Pages/AccessDenied.razor
@page "/access-denied"

<PageTitle>Access Denied</PageTitle>

<div class="container" style="max-width: 600px; margin-top: 50px; text-align: center;">
    <h1 style="color: var(--color-danger);">Access Denied</h1>
    <p>You do not have permission to view this resource.</p>
    <div style="margin-top: 20px;">
        <a href="/" class="btn btn-primary">Return Home</a>
        <form action="/logout" method="post" style="display: inline;">
            <AntiforgeryToken />
            <button type="submit" class="btn btn-link">Logout</button>
        </form>
    </div>
</div>
```

```razor src/MyBlog.Web/Components/Pages/Admin/ChangePassword.razor
@page "/admin/change-password"
@attribute [Authorize]
@inject IAuthService AuthService
@inject AuthenticationStateProvider AuthStateProvider
@inject NavigationManager Navigation
@using System.Security.Claims

<PageTitle>Change Password</PageTitle>

<h1>Change Password</h1>

<div class="change-password-form">
    @if (!string.IsNullOrEmpty(_successMessage))
    {
        <div class="success-message">@_successMessage</div>
    }

    @if (!string.IsNullOrEmpty(_errorMessage))
    {
        <div class="error-message">@_errorMessage</div>
    }

    <form @onsubmit="HandleSubmit">
        <div class="form-group">
            <label for="currentPassword">Current Password</label>
            <input type="password" id="currentPassword" name="currentPassword" @bind="_currentPassword" required />
        </div>

        <div class="form-group">
            <label for="newPassword">New Password</label>
            <input type="password" id="newPassword" name="newPassword" @bind="_newPassword" required minlength="8" />
            <small>Minimum 8 characters</small>
        </div>

        <div class="form-group">
            <label for="confirmPassword">Confirm New Password</label>
            <input type="password" id="confirmPassword" name="confirmPassword" @bind="_confirmPassword" required />
        </div>

        <div class="form-actions">
            <button type="submit" class="btn btn-primary">Change Password</button>
            <a href="/admin" class="btn btn-secondary">Cancel</a>
        </div>
    </form>
</div>

@code {
    private string _currentPassword = "";
    private string _newPassword = "";
    private string _confirmPassword = "";
    private string? _successMessage;
    private string? _errorMessage;

    private async Task HandleSubmit()
    {
        _successMessage = null;
        _errorMessage = null;

        if (string.IsNullOrWhiteSpace(_newPassword) || _newPassword.Length < 8)
        {
            _errorMessage = "New password must be at least 8 characters.";
            return;
        }

        if (_newPassword != _confirmPassword)
        {
            _errorMessage = "New password and confirmation do not match.";
            return;
        }

        if (_currentPassword == _newPassword)
        {
            _errorMessage = "New password must be different from current password.";
            return;
        }

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _errorMessage = "Unable to identify current user. Please log in again.";
            return;
        }

        var success = await AuthService.ChangePasswordAsync(userId, _currentPassword, _newPassword);

        if (success)
        {
            _successMessage = "Password changed successfully!";
            _currentPassword = "";
            _newPassword = "";
            _confirmPassword = "";
        }
        else
        {
            _errorMessage = "Current password is incorrect.";
        }
    }
}
```

```css src/MyBlog.Web/Components/Pages/Admin/ChangePassword.razor.css

.change-password-form {
    max-width: 500px;
}

.change-password-form small {
    display: block;
    margin-top: 0.25rem;
    color: var(--color-text-muted);
    font-size: 0.875rem;
}
```

```razor src/MyBlog.Web/Components/Pages/Admin/Dashboard.razor
@page "/admin"
@attribute [Authorize]
@inject IPostRepository PostRepository

<PageTitle>Admin Dashboard</PageTitle>

<h1>Admin Dashboard</h1>

<div class="dashboard-stats">
    <div class="stat-card">
        <h3>Total Posts</h3>
        <p class="stat-value">@_postCount</p>
    </div>
</div>

<div class="admin-nav">
    <a href="/admin/posts" class="btn">Manage Posts</a>
    <a href="/admin/posts/new" class="btn">New Post</a>
    <a href="/admin/images" class="btn">Manage Images</a>
</div>

<h2>Recent Posts</h2>
@if (_recentPosts is null)
{
    <p>Loading...</p>
}
else if (_recentPosts.Count == 0)
{
    <p>No posts yet.</p>
}
else
{
    <table class="admin-table">
        <thead>
            <tr>
                <th>Title</th>
                <th>Status</th>
                <th>Updated</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var post in _recentPosts)
            {
                <tr>
                    <td><a href="/admin/posts/edit/@post.Id">@post.Title</a></td>
                    <td>@(post.IsPublished ? "Published" : "Draft")</td>
                    <td>@(post.PublishedAtUtc?.ToString("MMM d, yyyy") ?? "—")</td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private int _postCount;
    private IReadOnlyList<PostListItemDto>? _recentPosts;

    protected override async Task OnInitializedAsync()
    {
        _postCount = await PostRepository.GetCountAsync();
        _recentPosts = await PostRepository.GetRecentPostsAsync(5);
    }
}
```

```css src/MyBlog.Web/Components/Pages/Admin/Dashboard.razor.css

.dashboard-stats {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
    gap: var(--spacing);
    margin-bottom: calc(var(--spacing) * 2);
}

.stat-card {
    padding: calc(var(--spacing) * 1.25);
    background-color: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-lg);
    text-align: center;
    transition: border-color var(--transition-fast);
}

.stat-card:hover {
    border-color: var(--color-primary-muted);
}

.stat-value {
    font-size: 2rem;
    font-weight: 700;
    margin: 0;
    color: var(--color-primary);
}

.stat-label {
    color: var(--color-text-muted);
    font-size: 0.875rem;
}

.admin-nav {
    display: flex;
    gap: 0.5rem;
    margin-bottom: calc(var(--spacing) * 2);
    flex-wrap: wrap;
}

@media (max-width: 480px) {
    .dashboard-stats {
        grid-template-columns: 1fr;
    }
}
```

```razor src/MyBlog.Web/Components/Pages/Admin/ImageManager.razor
@page "/admin/images"
@attribute [Authorize]
@inject IImageRepository ImageRepository
@inject AuthenticationStateProvider AuthStateProvider
@using System.Security.Claims

<PageTitle>Manage Images</PageTitle>

<h1>Manage Images</h1>

<div class="image-upload">
    <h3>Upload New Image</h3>
    <InputFile OnChange="HandleFileUpload" accept="image/*" disabled="@_uploading" />
    @if (_uploading)
    {
        <p>Uploading...</p>
    }
    @if (!string.IsNullOrEmpty(_uploadStatus))
    {
        <p class="@(_uploadError ? "error" : "success")">@_uploadStatus</p>
    }
</div>

<h3>Uploaded Images</h3>
@if (_images is null)
{
    <p>Loading...</p>
}
else if (_images.Count == 0)
{
    <p>No images uploaded yet.</p>
}
else
{
    <div class="image-grid">
        @foreach (var image in _images)
        {
            <div class="image-card">
                <img src="/api/images/@image.Id" alt="@image.FileName" />
                <div class="image-info">
                    <p>@image.FileName</p>
                    <code>/api/images/@image.Id</code>
                    <button @onclick="() => DeleteImage(image.Id)" class="btn-link danger" disabled="@_deleting">Delete</button>
                </div>
            </div>
        }
    </div>
}

@code {
    private IReadOnlyList<Image>? _images;
    private string? _uploadStatus;
    private bool _uploadError;
    private bool _uploading;
    private bool _deleting;

    protected override async Task OnInitializedAsync() => await LoadImages();

    private async Task LoadImages() => _images = await ImageRepository.GetAllAsync();

    private async Task HandleFileUpload(InputFileChangeEventArgs e)
    {
        _uploadStatus = null;
        _uploadError = false;
        _uploading = true;

        try
        {
            var file = e.File;
            if (file.Size > AppConstants.MaxImageSizeBytes)
            {
                _uploadStatus = "File too large. Maximum size is 5MB.";
                _uploadError = true;
                return;
            }

            if (!AppConstants.AllowedImageTypes.Contains(file.ContentType))
            {
                _uploadStatus = "Invalid file type. Only JPEG, PNG, GIF, and WebP are allowed.";
                _uploadError = true;
                return;
            }

            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier);
            
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _uploadStatus = "Unable to identify current user. Please log in again.";
                _uploadError = true;
                return;
            }

            using var stream = file.OpenReadStream(AppConstants.MaxImageSizeBytes);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            var image = new Image
            {
                Id = Guid.NewGuid(),
                FileName = file.Name,
                ContentType = file.ContentType,
                Data = ms.ToArray(),
                UploadedAtUtc = DateTime.UtcNow,
                UploadedByUserId = userId
            };

            await ImageRepository.CreateAsync(image);
            _uploadStatus = "Image uploaded successfully!";
            await LoadImages();
        }
        catch (Exception ex)
        {
            _uploadStatus = $"Upload failed: {ex.Message}";
            _uploadError = true;
        }
        finally
        {
            _uploading = false;
        }
    }

    private async Task DeleteImage(Guid id)
    {
        _deleting = true;
        try
        {
            await ImageRepository.DeleteAsync(id);
            await LoadImages();
        }
        finally
        {
            _deleting = false;
        }
    }
}
```

```css src/MyBlog.Web/Components/Pages/Admin/ImageManager.razor.css

.image-upload {
    margin-bottom: calc(var(--spacing) * 2);
    padding: var(--spacing);
    background-color: var(--color-bg-alt);
    border: 2px dashed var(--color-border);
    border-radius: var(--radius-lg);
    transition: border-color var(--transition-fast);
}

.image-upload:hover {
    border-color: var(--color-primary-muted);
}

.image-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
    gap: var(--spacing);
}

.image-card {
    border: 1px solid var(--color-border);
    border-radius: var(--radius-lg);
    overflow: hidden;
    background-color: var(--color-bg);
    transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}

.image-card:hover {
    border-color: var(--color-primary-muted);
    box-shadow: 0 4px 12px var(--color-shadow);
}

.image-card img {
    width: 100%;
    height: 150px;
    object-fit: cover;
}

.image-info {
    padding: 0.75rem;
}

.image-info p {
    margin: 0 0 0.25rem;
    font-size: 0.875rem;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.image-info code {
    display: block;
    font-size: 0.75rem;
    color: var(--color-text-muted);
    margin-bottom: 0.5rem;
    padding: 0.25rem;
    background-color: var(--color-code-bg);
    border-radius: 0.25rem;
}
```

```razor src/MyBlog.Web/Components/Pages/Admin/PostEditor.razor
@page "/admin/posts/new"
@page "/admin/posts/edit/{Id:guid}"
@attribute [Authorize]
@implements IDisposable
@inject IPostRepository PostRepository
@inject ISlugService SlugService
@inject NavigationManager Navigation
@inject AuthenticationStateProvider AuthStateProvider
@inject ILogger<PostEditor> Logger
@using System.Security.Claims

<PageTitle>@(IsEdit ? "Edit Post" : "New Post")</PageTitle>

<h1>@(IsEdit ? "Edit Post" : "New Post")</h1>

@if (_loading)
{
    <p>Loading...</p>
}
else if (IsEdit && _existingPost is null)
{
    <p>Post not found.</p>
    <a href="/admin/posts">Back to Posts</a>
}
else
{
    <div class="post-editor">
        <div class="editor-form">
            @if (!_interactive)
            {
                <div class="editor-status" role="status">
                    Connecting to the server. Please wait before typing or pasting.
                </div>
            }

            <div class="form-group">
                <label for="title">Title</label>
                <input type="text" id="title" @bind="_title" @bind:event="oninput" readonly="@(!_interactive)" placeholder="Enter post title..." />
                <small class="char-count">@_title.Length characters received by the server</small>
            </div>

            <div class="form-group">
                <label for="summary">Summary</label>
                <textarea id="summary" @bind="_summary" @bind:event="oninput" readonly="@(!_interactive)" rows="2" placeholder="Brief description for listings..."></textarea>
                <small class="char-count">@_summary.Length characters received by the server</small>
            </div>

            <div class="form-group">
                <label for="content">Content (Markdown)</label>
                <textarea id="content" @bind="_content" @bind:event="oninput" @bind:after="SchedulePreviewRefresh" readonly="@(!_interactive)" rows="15" placeholder="Write your post content in Markdown..."></textarea>
                <small class="char-count">@_content.Length characters received by the server</small>
            </div>

            <div class="form-group checkbox">
                <label>
                    <input type="checkbox" @bind="_isPublished" />
                    Published
                </label>
            </div>

            @if (!string.IsNullOrEmpty(_error))
            {
                <div class="error-message">@_error</div>
            }

            <div class="form-actions">
                <button @onclick="Save" class="btn btn-primary" disabled="@(_saving || !_interactive)">
                    @(_saving ? "Saving..." : "Save")
                </button>
                <a href="/admin/posts" class="btn">Cancel</a>
            </div>
        </div>

        <div class="editor-preview">
            <h3>Preview</h3>
            <div class="preview-content">
                <MarkdownRenderer Content="@_previewContent" />
            </div>
        </div>
    </div>
}

@code {
    private const int PreviewDebounceMilliseconds = 500;

    [Parameter]
    public Guid? Id { get; set; }

    private bool IsEdit => Id.HasValue;
    private bool _loading = true;
    private bool _saving;

    private bool _interactive;

    private string _title = "";
    private string _summary = "";
    private string _content = "";
    private string _previewContent = "";
    private bool _isPublished = true;
    private string? _error;
    private Post? _existingPost;
    private System.Threading.Timer? _previewTimer;

    protected override async Task OnInitializedAsync()
    {
        if (IsEdit)
        {
            _existingPost = await PostRepository.GetByIdAsync(Id!.Value);
            if (_existingPost is not null)
            {
                _title = _existingPost.Title;
                _summary = _existingPost.Summary;
                _content = _existingPost.Content;
                _previewContent = _existingPost.Content;
                _isPublished = _existingPost.IsPublished;
            }
        }
        _loading = false;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && !_interactive)
        {
            _interactive = true;
            StateHasChanged();
        }
    }

    private void SchedulePreviewRefresh()
    {
        _previewTimer ??= new System.Threading.Timer(_ => RefreshPreview(), null, Timeout.Infinite, Timeout.Infinite);
        _previewTimer.Change(PreviewDebounceMilliseconds, Timeout.Infinite);
    }

    private void RefreshPreview()
    {
        try
        {
            _ = InvokeAsync(() =>
            {
                _previewContent = _content;
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task Save()
    {
        if (!_interactive)
        {
            _error = "The editor is still connecting. Please wait a moment and try again.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_title))
        {
            _error = "Title is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_content))
        {
            _error = "Content is required.";
            return;
        }

        _saving = true;
        _error = null;

        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _error = "User not authenticated. Please log in again.";
                return;
            }

            if (!Guid.TryParse(userId, out var authorId))
            {
                _error = "Invalid user ID. Please log in again.";
                Logger.LogError("Failed to parse user ID from claims: {UserId}", userId);
                return;
            }

            if (IsEdit && _existingPost is not null)
            {
                var newSlug = SlugService.GenerateSlugOrUuid(_title);

                if (newSlug != _existingPost.Slug)
                {
                    var isSlugTaken = await PostRepository.IsSlugTakenAsync(newSlug, _existingPost.Id);
                    if (isSlugTaken)
                    {
                        newSlug = SlugService.GenerateUuidSlug();
                        Logger.LogInformation(
                            "Slug collision detected for title '{Title}', using UUID slug: {Slug}",
                            _title, newSlug);
                    }
                }

                _existingPost.Title = _title;
                _existingPost.Slug = newSlug;
                _existingPost.Summary = _summary;
                _existingPost.Content = _content;
                _existingPost.IsPublished = _isPublished;
                _existingPost.UpdatedAtUtc = DateTime.UtcNow;

                if (_isPublished && !_existingPost.PublishedAtUtc.HasValue)
                {
                    _existingPost.PublishedAtUtc = DateTime.UtcNow;
                }
                else if (!_isPublished)
                {
                    _existingPost.PublishedAtUtc = null;
                }

                await PostRepository.UpdateAsync(_existingPost);
                Logger.LogInformation(
                    "Post updated: {PostId} by user {UserId} ({ContentLength} content characters)",
                    _existingPost.Id, authorId, _content.Length);
            }
            else
            {
                var slug = SlugService.GenerateSlugOrUuid(_title);

                var isSlugTaken = await PostRepository.IsSlugTakenAsync(slug);
                if (isSlugTaken)
                {
                    slug = SlugService.GenerateUuidSlug();
                    Logger.LogInformation(
                        "Slug collision detected for title '{Title}', using UUID slug: {Slug}",
                        _title, slug);
                }

                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    Title = _title,
                    Slug = slug,
                    Summary = _summary,
                    Content = _content,
                    IsPublished = _isPublished,
                    AuthorId = authorId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    PublishedAtUtc = _isPublished ? DateTime.UtcNow : null
                };
                await PostRepository.CreateAsync(post);
                Logger.LogInformation(
                    "Post created: {PostId} by user {UserId} ({ContentLength} content characters)",
                    post.Id, authorId, _content.Length);
            }

            Navigation.NavigateTo("/admin/posts");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save post. Title: {Title}, IsEdit: {IsEdit}", _title, IsEdit);

            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            _error = $"Failed to save: {innerMessage}";
        }
        finally
        {
            _saving = false;
        }
    }

    public void Dispose() => _previewTimer?.Dispose();
}
```

```css src/MyBlog.Web/Components/Pages/Admin/PostEditor.razor.css

.post-editor {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: calc(var(--spacing) * 2);
}

.editor-form {
    display: flex;
    flex-direction: column;
}

.editor-form .form-group input[type="text"],
.editor-form .form-group textarea {
    font-size: 16px;
}

.editor-form .form-group textarea#content {
    font-family: ui-monospace, 'SF Mono', SFMono-Regular, Menlo, Monaco, Consolas, monospace;
    line-height: 1.5;
}

.editor-status {
    margin-bottom: var(--spacing);
    padding: 0.5rem 0.75rem;
    border: 1px solid var(--color-warning);
    border-radius: var(--radius);
    background-color: var(--color-bg-alt);
    color: var(--color-warning);
    font-size: 0.875rem;
}

.char-count {
    display: block;
    margin-top: 0.25rem;
    color: var(--color-text-muted);
    font-size: 0.75rem;
    font-variant-numeric: tabular-nums;
}

.editor-preview {
    border: 1px solid var(--color-border);
    border-radius: var(--radius-lg);
    padding: var(--spacing);
    max-height: 80vh;
    overflow-y: auto;
    background-color: var(--color-bg);
}

.editor-preview h3 {
    margin-bottom: var(--spacing);
    padding-bottom: 0.5rem;
    border-bottom: 1px solid var(--color-border);
    color: var(--color-text-muted);
    font-size: 0.875rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

@media (max-width: 1024px) {
    .post-editor {
        grid-template-columns: 1fr 1fr;
        gap: var(--spacing);
    }
}

@media (max-width: 768px) {
    .post-editor {
        grid-template-columns: 1fr;
        gap: var(--spacing);
    }

    .editor-preview {
        order: -1;
        max-height: 200px;
        position: sticky;
        top: 0;
        z-index: 10;
        margin-bottom: var(--spacing);
    }

    .editor-form .form-group textarea#content {
        min-height: 300px;
        -webkit-overflow-scrolling: touch;
    }

    .editor-form .form-group textarea#summary {
        min-height: 80px;
    }

    .editor-form .form-group.checkbox label {
        padding: 0.75rem 0;
        min-height: 44px;
    }

    .editor-form .form-group.checkbox input[type="checkbox"] {
        width: 20px;
        height: 20px;
        min-width: 20px;
    }

    .editor-form .form-actions {
        flex-direction: column;
    }

    .editor-form .form-actions .btn {
        width: 100%;
        justify-content: center;
        min-height: 44px;
    }
}

@media (max-width: 480px) {
    .editor-preview {
        max-height: 150px;
        padding: calc(var(--spacing) * 0.75);
    }

    .editor-preview h3 {
        font-size: 0.75rem;
        margin-bottom: calc(var(--spacing) * 0.5);
        padding-bottom: 0.25rem;
    }
}
```

```razor src/MyBlog.Web/Components/Pages/Admin/PostList.razor
@page "/admin/posts"
@attribute [Authorize]
@inject IPostRepository PostRepository
@inject NavigationManager Navigation

<PageTitle>Manage Posts</PageTitle>

<h1>Manage Posts</h1>

<p><a href="/admin/posts/new" class="btn btn-primary">Create New Post</a></p>

@if (_posts is null)
{
    <p>Loading...</p>
}
else if (_posts.Count == 0)
{
    <p>No posts yet.</p>
}
else
{
    <table class="admin-table">
        <thead>
            <tr>
                <th>Title</th>
                <th>Author</th>
                <th>Status</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var post in _posts)
            {
                <tr>
                    <td>@post.Title</td>
                    <td>@post.AuthorDisplayName</td>
                    <td>@(post.IsPublished ? "Published" : "Draft")</td>
                    <td>
                        <a href="/admin/posts/edit/@post.Id">Edit</a>
                        <button @onclick="() => DeletePost(post.Id)" class="btn-link danger" disabled="@_deleting">Delete</button>
                    </td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private IReadOnlyList<PostListItemDto>? _posts;
    private bool _deleting;

    protected override async Task OnInitializedAsync() => await LoadPosts();

    private async Task LoadPosts() => _posts = await PostRepository.GetAllPostsAsync();

    private async Task DeletePost(Guid id)
    {
        _deleting = true;
        try
        {
            await PostRepository.DeleteAsync(id);
            await LoadPosts();
        }
        finally
        {
            _deleting = false;
        }
    }
}
```

```razor src/MyBlog.Web/Components/Pages/Admin/UserEditor.razor
@page "/admin/users/new"
@page "/admin/users/edit/{Id:guid}"
@attribute [Authorize(Roles = AppConstants.AdminRole)]
@inject IUserRepository UserRepository
@inject IPasswordService PasswordService
@inject NavigationManager Navigation

<PageTitle>@(IsEdit ? "Edit User" : "New User")</PageTitle>

<h1>@(IsEdit ? "Edit User" : "New User")</h1>

<div class="user-editor" style="max-width: 600px;">
    @if (!string.IsNullOrEmpty(_error))
    {
        <div class="error-message">@_error</div>
    }

    <div class="form-group">
        <label for="username">Username</label>
        <input type="text" id="username" @bind="_username" required />
    </div>

    <div class="form-group">
        <label for="displayName">Display Name</label>
        <input type="text" id="displayName" @bind="_displayName" required />
    </div>

    <div class="form-group">
        <label for="email">Email</label>
        <input type="email" id="email" @bind="_email" required />
    </div>

    <div class="form-group">
        <label for="password">Password @(IsEdit ? "(Leave blank to keep current)" : "")</label>
        <input type="password" id="password" @bind="_password" required="@(!IsEdit)" />
        @if (!IsEdit) 
        {
            <small>Required for new users.</small>
        }
    </div>

    <div class="form-actions">
        <button @onclick="Save" class="btn btn-primary" disabled="@_saving">
            @(_saving ? "Saving..." : "Save User")
        </button>
        <a href="/admin/users" class="btn btn-secondary">Cancel</a>
    </div>
</div>

@code {
    [Parameter]
    public Guid? Id { get; set; }

    private bool IsEdit => Id.HasValue;
    private bool _saving;
    private string? _error;

    private string _username = "";
    private string _displayName = "";
    private string _email = "";
    private string _password = "";

    private User? _existingUser;

    protected override async Task OnParametersSetAsync()
    {
        if (IsEdit)
        {
            _existingUser = await UserRepository.GetByIdAsync(Id!.Value);
            if (_existingUser is null)
            {
                Navigation.NavigateTo("/admin/users");
                return;
            }

            _username = _existingUser.Username;
            _displayName = _existingUser.DisplayName;
            _email = _existingUser.Email;
        }
    }

    private async Task Save()
    {
        _error = null;
        _saving = true;

        try
        {
            if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_displayName) || string.IsNullOrWhiteSpace(_email))
            {
                _error = "All fields are required.";
                return;
            }

            var duplicate = await UserRepository.GetByUsernameAsync(_username);
            if (duplicate is not null && duplicate.Id != Id)
            {
                _error = "Username is already taken.";
                return;
            }

            if (IsEdit && _existingUser is not null)
            {
                _existingUser.Username = _username;
                _existingUser.DisplayName = _displayName;
                _existingUser.Email = _email;

                if (!string.IsNullOrWhiteSpace(_password))
                {
                     if (_password.Length < 8)
                     {
                         _error = "Password must be at least 8 characters.";
                         return;
                     }
                    _existingUser.PasswordHash = PasswordService.HashPassword(_password);
                }

                await UserRepository.UpdateAsync(_existingUser);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_password) || _password.Length < 8)
                {
                    _error = "Password is required (min 8 chars) for new users.";
                    return;
                }

                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    Username = _username,
                    DisplayName = _displayName,
                    Email = _email,
                    PasswordHash = PasswordService.HashPassword(_password),
                    CreatedAtUtc = DateTime.UtcNow
                };

                await UserRepository.CreateAsync(newUser);
            }

            Navigation.NavigateTo("/admin/users");
        }
        catch (Exception ex)
        {
            _error = $"Error saving user: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }
}
```

```css src/MyBlog.Web/Components/Pages/Admin/UserEditor.razor.css

.user-editor {
    width: 100%;
    padding: 1rem;
    box-sizing: border-box;
}

@media (min-width: 768px) {
    .user-editor .form-group {
        flex-direction: row;
        align-items: center;
        display: flex;
    }

    .user-editor .form-group label {
        flex: 0 0 150px;
        margin-bottom: 0;
    }

    .user-editor .form-group input {
        flex: 1;
    }
}
```

```razor src/MyBlog.Web/Components/Pages/Admin/UserList.razor
@page "/admin/users"
@attribute [Authorize(Roles = AppConstants.AdminRole)]
@inject IUserRepository UserRepository
@inject AuthenticationStateProvider AuthStateProvider
@inject IJSRuntime JsRuntime
@using System.Security.Claims

<PageTitle>Manage Users</PageTitle>

<div class="admin-container">
    <div class="admin-header">
        <h1>Manage Users</h1>
        <a href="/admin/users/new" class="btn btn-primary">Create New User</a>
    </div>

    @if (_users is null)
    {
        <p>Loading...</p>
    }
    else
    {
        <div class="responsive-user-list">
            <div class="list-header d-none-mobile">
                <div class="col">Username</div>
                <div class="col">Display Name</div>
                <div class="col">Email</div>
                <div class="col actions">Actions</div>
            </div>

            @foreach (var user in _users)
            {
                <div class="list-row">
                    <div class="list-item">
                        <span class="mobile-label">Username:</span>
                        <a href="/admin/users/edit/@user.Id">@user.Username</a>
                    </div>
                    <div class="list-item">
                        <span class="mobile-label">Name:</span>
                        @user.DisplayName
                    </div>
                    <div class="list-item email-cell">
                        <span class="mobile-label">Email:</span>
                        @user.Email
                    </div>
                    <div class="list-item actions">
                        <a href="/admin/users/edit/@user.Id" class="btn btn-sm">Edit</a>
                        @if (user.Id != _currentUserId)
                        {
                            <button @onclick="() => DeleteUser(user.Id)" class="btn btn-sm btn-danger">Delete</button>
                        }
                        else
                        {
                            <span class="current-user-badge">(Current)</span>
                        }
                    </div>
                </div>
            }
        </div>
    }
</div>

@code {
    private IReadOnlyList<User>? _users;
    private Guid _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var idClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim != null && Guid.TryParse(idClaim.Value, out var id))
        {
            _currentUserId = id;
        }

        await LoadUsers();
    }

    private async Task LoadUsers() => _users = await UserRepository.GetAllAsync();

    private async Task DeleteUser(Guid id)
    {
        if (id == _currentUserId) return;

        var confirm = await JsRuntime.InvokeAsync<bool>("confirm", "Are you sure you want to delete this user?");
        
        if (confirm)
        {
            await UserRepository.DeleteAsync(id);
            await LoadUsers();
        }
    }
}
```

```css src/MyBlog.Web/Components/Pages/Admin/UserList.razor.css
.responsive-user-list {
    display: flex;
    flex-direction: column;
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    overflow: hidden;
}

.list-header {
    display: flex;
    background-color: var(--color-bg-alt);
    font-weight: bold;
    padding: 1rem;
    border-bottom: 2px solid var(--color-border);
}

.list-row {
    display: flex;
    padding: 1rem;
    border-bottom: 1px solid var(--color-border);
    background-color: var(--color-bg);
}

.list-row:last-child { border-bottom: none; }
.col, .list-item { flex: 1; min-width: 0; }
.actions { text-align: right; flex: 0 0 180px; }

.mobile-label { display: none; font-weight: bold; margin-right: 0.5rem; }
.current-user-badge { color: var(--color-text-muted); font-size: 0.85rem; }

@media (max-width: 768px) {
    .d-none-mobile { display: none; }
    
    .list-row {
        flex-direction: column;
        gap: 0.5rem;
        padding: 1.5rem 1rem;
    }

    .list-item {
        display: flex;
        justify-content: space-between;
        align-items: center;
        text-align: left;
    }

    .mobile-label { display: inline-block; }
    .actions { 
        flex: 1; 
        margin-top: 1rem; 
        padding-top: 1rem; 
        border-top: 1px dashed var(--color-border);
        text-align: left;
        justify-content: flex-start;
        gap: 10px;
    }
}
```

```razor src/MyBlog.Web/Components/Pages/Home.razor
@page "/"
@inject IPostRepository PostRepository
@inject IConfiguration Configuration

<PageTitle>@(Configuration["Application:Title"] ?? "MyBlog")</PageTitle>

<h1>Welcome to @(Configuration["Application:Title"] ?? "MyBlog")</h1>

@if (_posts is null)
{
    <p>Loading...</p>
}
else if (_posts.Count == 0)
{
    <p>No posts yet. Check back soon!</p>
}
else
{
    <div class="post-list">
        @foreach (var post in _posts)
        {
            <PostCard Post="post" />
        }
    </div>

    <Pagination CurrentPage="_currentPage" TotalPages="_totalPages" BaseUrl="/" />
}

@code {
    private IReadOnlyList<PostListItemDto>? _posts;
    private int _currentPage = 1;
    private int _totalPages = 1;

    [SupplyParameterFromQuery(Name = "page")]
    public int? Page { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _currentPage = Page ?? 1;
        if (_currentPage < 1) _currentPage = 1;

        var pageSize = Configuration.GetValue("Application:PostsPerPage", AppConstants.DefaultPageSize);
        var (posts, totalCount) = await PostRepository.GetPublishedPostsAsync(_currentPage, pageSize);
        
        _posts = posts;
        _totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
```

```razor src/MyBlog.Web/Components/Pages/Login.razor
@page "/login"
@inject NavigationManager Navigation

<PageTitle>Login</PageTitle>

<div class="login-page">
    <h1>Login</h1>

    @if (!string.IsNullOrEmpty(_error))
    {
        <div class="error-message">@_error</div>
    }

    <form method="post" action="/account/login" data-enhance="false">
        <AntiforgeryToken />
        <input type="hidden" name="returnUrl" value="@ReturnUrl" />

        <div class="form-group">
            <label for="username">Username</label>
            <input type="text" id="username" name="username" required autocomplete="username" autocapitalize="none" />
        </div>

        <div class="form-group">
            <label for="password">Password</label>
            <input type="password" id="password" name="password" required autocomplete="current-password" />
        </div>

        <button type="submit" class="btn btn-primary">Login</button>
    </form>
</div>

@code {
    private string? _error;

    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    public string? Error { get; set; }

    protected override void OnInitialized() =>
        _error = Error switch
        {
            "required" => "Username and password are required",
            "invalid" => "Invalid username or password",
            _ => null
        };
}
```

```css src/MyBlog.Web/Components/Pages/Login.razor.css

.login-page {
    max-width: 400px;
    margin: 0 auto;
    padding: var(--spacing);
}

.login-page h1 {
    text-align: center;
    margin-bottom: calc(var(--spacing) * 1.5);
}

.login-page .btn-primary {
    width: 100%;
    justify-content: center;
    margin-top: var(--spacing);
}

@media (max-width: 480px) {
    .login-page {
        max-width: 100%;
        padding: var(--spacing) 0;
    }
}
```

```razor src/MyBlog.Web/Components/Pages/PostDetail.razor
@page "/post/{Slug}"
@using System.Text.Json.Nodes
@using System.Text.RegularExpressions
@inject IPostRepository PostRepository
@inject NavigationManager Navigation
@inject IConfiguration Configuration

@if (_post is null)
{
    @if (_notFound)
    {
        <PageTitle>Not Found</PageTitle>
        <h1>Post Not Found</h1>
        <p>The post you're looking for doesn't exist.</p>
        <a href="/">← Back to Home</a>
    }
    else
    {
        <p>Loading...</p>
    }
}
else
{
    <PageTitle>@_post.Title</PageTitle>

    <HeadContent>
        <meta name="robots" content="index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1" />
        <meta name="description" content="@_post.Summary" />
        <meta name="author" content="@_post.AuthorDisplayName" />
        <link rel="canonical" href="@_currentUrl" />

        <meta property="og:type" content="article" />
        <meta property="og:site_name" content="@(Configuration["Application:Title"] ?? "MyBlog")" />
        <meta property="og:title" content="@_post.Title" />
        <meta property="og:description" content="@_post.Summary" />
        <meta property="og:url" content="@_currentUrl" />
        @if (_post.PublishedAtUtc.HasValue)
        {
            <meta property="article:published_time" content="@_post.PublishedAtUtc.Value.ToString("O")" />
        }
        <meta property="article:modified_time" content="@_post.UpdatedAtUtc.ToString("O")" />

        @if (!string.IsNullOrEmpty(_previewImage))
        {
            <meta property="og:image" content="@_previewImage" />
            <meta property="og:image:alt" content="@_post.Title" />
        }

        <meta name="twitter:card" content="@(string.IsNullOrEmpty(_previewImage) ? "summary" : "summary_large_image")" />
        <meta name="twitter:title" content="@_post.Title" />
        <meta name="twitter:description" content="@_post.Summary" />
        @if (!string.IsNullOrEmpty(_previewImage))
        {
            <meta name="twitter:image" content="@_previewImage" />
        }

        <script type="application/ld+json">
            @((MarkupString)GetJsonLd())
        </script>
    </HeadContent>

    <article class="post-detail">
        <header class="post-header">
            <h1>@_post.Title</h1>
            <div class="post-meta">
                <span class="author">By @_post.AuthorDisplayName</span>
                @if (_post.PublishedAtUtc.HasValue)
                {
                    <span class="date">Published @_post.PublishedAtUtc.Value.ToString("MMMM d, yyyy")</span>
                }

                <ReaderBadge Slug="@Slug" />

                <button type="button" class="btn-link share-btn" data-title="@_post.Title" onclick="sharePost(this.dataset.title)">
                    <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <circle cx="18" cy="5" r="3"></circle>
                        <circle cx="6" cy="12" r="3"></circle>
                        <circle cx="18" cy="19" r="3"></circle>
                        <line x1="8.59" y1="13.51" x2="15.42" y2="17.49"></line>
                        <line x1="15.41" y1="6.51" x2="8.59" y2="10.49"></line>
                    </svg>
                    Share
                </button>
            </div>
        </header>

        <div class="post-content">
            <MarkdownRenderer Content="@_post.Content" />
        </div>
    </article>

    <a href="/" class="back-link">← Back to Home</a>
}

@code {
    [Parameter]
    public string Slug { get; set; } = "";

    private PostDetailDto? _post;
    private bool _notFound;
    private string _currentUrl = "";
    private string? _previewImage;

    private string GetJsonLd()
    {
        if (_post is null) return "";

        var schema = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BlogPosting",
            ["headline"] = _post.Title,
            ["description"] = _post.Summary,
            ["author"] = new JsonObject
            {
                ["@type"] = "Person",
                ["name"] = _post.AuthorDisplayName
            },
            ["publisher"] = new JsonObject
            {
                ["@type"] = "Organization",
                ["name"] = Configuration["Application:Title"] ?? "MyBlog"
            },
            ["datePublished"] = _post.PublishedAtUtc?.ToString("O"),
            ["dateModified"] = _post.UpdatedAtUtc.ToString("O"),
            ["mainEntityOfPage"] = new JsonObject
            {
                ["@type"] = "WebPage",
                ["@id"] = _currentUrl
            }
        };

        if (!string.IsNullOrEmpty(_previewImage))
        {
            schema["image"] = _previewImage;
        }

        return schema.ToJsonString();
    }

    protected override async Task OnParametersSetAsync()
    {
        _post = await PostRepository.GetBySlugAsync(Slug);
        _notFound = _post is null;

        if (_post is not null && !_post.IsPublished)
        {
            _post = null;
            _notFound = true;
        }

        if (_post is not null)
        {
            _currentUrl = Navigation.Uri;

            var imageMatch = Regex.Match(_post.Content, @"!\[.*?\]\((.*?)\)");
            if (imageMatch.Success)
            {
                var url = imageMatch.Groups[1].Value;
                if (url.StartsWith("/"))
                {
                    _previewImage = Navigation.ToAbsoluteUri(url).ToString();
                }
                else
                {
                    _previewImage = url;
                }
            }
        }
    }
}
```

```css src/MyBlog.Web/Components/Pages/PostDetail.razor.css

.share-btn {
    display: inline-flex;
    align-items: center;
    gap: 0.375rem;
    font-size: 0.875rem;
    color: var(--color-text-muted);
    background: none;
    border: none;
    padding: 0.5rem;
    cursor: pointer;
    min-height: 44px;
    border-radius: var(--radius);
    transition: color var(--transition-fast), background-color var(--transition-fast);
}

.share-btn:hover {
    color: var(--color-primary);
    background-color: var(--color-bg-alt);
    text-decoration: none;
}

.share-btn:active {
    transform: scale(0.95);
}

.share-btn svg {
    flex-shrink: 0;
}

.share-btn.success {
    color: var(--color-success);
}

.share-btn.error {
    color: var(--color-danger);
}

article {
    word-break: break-word;
}

@media (max-width: 768px) {
    .post-header h1 {
        font-size: 1.5rem;
        line-height: 1.3;
    }

    .share-btn {
        padding: 0.75rem;
        margin: -0.25rem;
    }
}
```

```razor src/MyBlog.Web/Components/Routes.razor
<Router AppAssembly="typeof(Program).Assembly" NotFoundPage="typeof(NotFound)">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)">
            <NotAuthorized>
                @if (context.User.Identity?.IsAuthenticated != true)
                {
                    <RedirectToLogin />
                }
                else
                {
                    <p role="alert">You are not authorized to access this resource.</p>
                }
            </NotAuthorized>
        </AuthorizeRouteView>
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
```

```razor src/MyBlog.Web/Components/Shared/Footer.razor
@inject IConfiguration Configuration

<footer class="footer">
    <div class="container">
        <p>&copy; @DateTime.Now.Year @(Configuration["Application:Title"] ?? "MyBlog")</p>

        @{
            var gitUrl = Configuration["Application:GitForgeUrl"];
            if (!string.IsNullOrWhiteSpace(gitUrl) &&
                Uri.TryCreate(gitUrl, UriKind.Absolute, out var validatedUri) &&
                (validatedUri.Scheme == Uri.UriSchemeHttp || validatedUri.Scheme == Uri.UriSchemeHttps))
            {
                <a href="@gitUrl" target="_blank" rel="noopener noreferrer" class="footer-link">
                    <svg height="16" width="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
                        <path d="M8 0c4.42 0 8 3.58 8 8a8.013 8.013 0 0 1-5.45 7.59c-.4.08-.55-.17-.55-.38 0-.27.01-1.13.01-2.2 0-.75-.25-1.23-.54-1.48 1.78-.2 3.65-.88 3.65-3.95 0-.88-.31-1.59-.82-2.15.08-.2.36-1.02-.08-2.12 0 0-.67-.22-2.2.82-.64-.18-1.32-.27-2-.27-.68 0-1.36.09-2 .27-1.53-1.03-2.2-.82-2.2-.82-.44 1.1-.16 1.92-.08 2.12-.51.56-.82 1.28-.82 2.15 0 3.06 1.86 3.75 3.64 3.95-.23.2-.44.55-.51 1.07-.46.21-1.61.55-2.33-.66-.15-.24-.6-.83-1.23-.82-.67.01-.27.38.01.53.34.19.73.9.82 1.13.16.45.68 1.31 2.69.94 0 .67.01 1.3.01 1.49 0 .21-.15.45-.55.38A7.995 7.995 0 0 1 0 8c0-4.42 3.58-8 8-8Z"></path>
                    </svg>
                    <span>Source Code</span>
                </a>
            }
        }
    </div>
</footer>
```

```css src/MyBlog.Web/Components/Shared/Footer.razor.css

.footer {
    background-color: var(--color-bg-alt);
    border-top: 1px solid var(--color-border);
    padding: var(--spacing) 0;
    text-align: center;
    color: var(--color-text-muted);
}

.footer-link {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    margin-top: 0.5rem;
    color: var(--color-text-muted);
    text-decoration: none;
    transition: color 0.2s ease;
}

.footer-link:hover {
    color: var(--color-primary);
    text-decoration: underline;
}

.footer-link svg {
    flex-shrink: 0;
}
```

```razor src/MyBlog.Web/Components/Shared/MarkdownRenderer.razor
@inject IMarkdownService MarkdownService

<div class="markdown-content">
    @((MarkupString)_htmlContent)
</div>

@code {
    [Parameter]
    public string? Content { get; set; }

    private string _htmlContent = "";
    private string? _parsedContent;

    protected override async Task OnParametersSetAsync()
    {
        var content = Content ?? "";

        if (_parsedContent is not null && string.Equals(_parsedContent, content, StringComparison.Ordinal))
        {
            return;
        }

        _htmlContent = await MarkdownService.ToHtmlAsync(content);
        _parsedContent = content;
    }
}
```

```razor src/MyBlog.Web/Components/Shared/Pagination.razor
@if (TotalPages > 1)
{
    <nav class="pagination" aria-label="Page navigation">
        @if (CurrentPage > 1)
        {
            <a href="@GetPageUrl(CurrentPage - 1)" class="page-link">← Previous</a>
        }
        
        <span class="page-info">Page @CurrentPage of @TotalPages</span>
        
        @if (CurrentPage < TotalPages)
        {
            <a href="@GetPageUrl(CurrentPage + 1)" class="page-link">Next →</a>
        }
    </nav>
}

@code {
    [Parameter, EditorRequired]
    public int CurrentPage { get; set; }

    [Parameter, EditorRequired]
    public int TotalPages { get; set; }

    [Parameter]
    public string BaseUrl { get; set; } = "/";

    private string GetPageUrl(int page) =>
        page == 1 ? BaseUrl : $"{BaseUrl}?page={page}";
}
```

```razor src/MyBlog.Web/Components/Shared/PostCard.razor
<article class="post-card">
    <h2 class="post-card-title">
        <a href="/post/@Post.Slug">@Post.Title</a>
    </h2>
    <div class="post-card-meta">
        <span class="author">By @Post.AuthorDisplayName</span>
        @if (Post.PublishedAtUtc.HasValue)
        {
            <span class="date">@Post.PublishedAtUtc.Value.ToString("MMMM d, yyyy")</span>
        }
        @if (!Post.IsPublished)
        {
            <span class="draft-badge">Draft</span>
        }
    </div>
    <p class="post-card-summary">@Post.Summary</p>
    <a href="/post/@Post.Slug" class="read-more">Read more →</a>
</article>

@code {
    [Parameter, EditorRequired]
    public PostListItemDto Post { get; set; } = default!;
}
```

```css src/MyBlog.Web/Components/Shared/PostCard.razor.css
article {
    word-break: break-word;
}
```

```razor src/MyBlog.Web/Components/Shared/ReaderBadge.razor
@inject NavigationManager Navigation
@inject ILogger<ReaderBadge> Logger
@implements IAsyncDisposable

<div class="reader-badge" title="Active readers on this post. Share this post to increase this number!">
    <span class="reader-dot">●</span>
    <span>@_count @(_count == 1 ? "person" : "people") reading now.</span>
</div>

@code {
    [Parameter, EditorRequired]
    public string Slug { get; set; } = "";

    private HubConnection? _hubConnection;
    private int _count;

    protected override async Task OnInitializedAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/readerHub"))
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<int>("UpdateCount", (newCount) =>
        {
            _count = newCount;
            InvokeAsync(StateHasChanged);
        });

        try
        {
            await _hubConnection.StartAsync();

            if (!string.IsNullOrEmpty(Slug))
            {
                await _hubConnection.InvokeAsync("JoinPage", Slug);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error connecting to ReaderHub for {Slug}", Slug);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            if (!string.IsNullOrEmpty(Slug) && _hubConnection.State == HubConnectionState.Connected)
            {
                try
                {
                    await _hubConnection.InvokeAsync("LeavePage", Slug);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(ex, "Could not leave ReaderHub group for {Slug}", Slug);
                }
            }

            await _hubConnection.DisposeAsync();
        }
    }
}
```

```css src/MyBlog.Web/Components/Shared/ReaderBadge.razor.css
.reader-badge {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.875rem;
    color: var(--color-text-muted);
    background-color: var(--color-bg-alt);
    padding: 0.25rem 0.5rem;
    border-radius: var(--radius);
    border: 1px solid var(--color-border);
}

.reader-dot {
    color: var(--color-success);
    font-size: 1.2em;
    line-height: 0.5;
    animation: pulse 2s infinite;
}

@keyframes pulse {
    0% { opacity: 1; }
    50% { opacity: 0.4; }
    100% { opacity: 1; }
}
```

```razor src/MyBlog.Web/Components/Shared/RedirectToLogin.razor
@inject NavigationManager Navigation

@code {
    protected override void OnInitialized() => Navigation.NavigateTo($"/login?returnUrl={Uri.EscapeDataString(Navigation.Uri)}", forceLoad: true);
}
```

```razor src/MyBlog.Web/Components/Shared/ThemeSwitcher.razor
@implements IDisposable
@inject IJSRuntime JS
@inject ILogger<ThemeSwitcher> Logger

<div class="theme-switcher">
    <button class="theme-switcher-btn"
            @onclick="ToggleMenu"
            @onclick:stopPropagation="true"
            aria-label="Change theme"
            aria-expanded="@_isOpen"
            aria-haspopup="true"
            title="Change theme">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            @if (IsDarkTheme(_currentTheme))
            {
                <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
            }
            else
            {
                <circle cx="12" cy="12" r="5"></circle>
                <line x1="12" y1="1" x2="12" y2="3"></line>
                <line x1="12" y1="21" x2="12" y2="23"></line>
                <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
                <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
                <line x1="1" y1="12" x2="3" y2="12"></line>
                <line x1="21" y1="12" x2="23" y2="12"></line>
                <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
                <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
            }
        </svg>
    </button>

    <div class="theme-menu @(_isOpen ? "open" : "")" role="menu" aria-label="Theme options">
        @foreach (var theme in Themes)
        {
            <button class="theme-option @(theme.Id == _currentTheme ? "active" : "")"
                    @onclick="() => SelectThemeAsync(theme.Id)"
                    @onclick:stopPropagation="true"
                    role="menuitem"
                    aria-current="@(theme.Id == _currentTheme ? "true" : null)">
                <span class="theme-preview theme-preview-@theme.Id" aria-hidden="true"></span>
                <span>@theme.Name</span>
            </button>
        }
    </div>
</div>

@code {
    private bool _isOpen;
    private string _currentTheme = "light";
    private DotNetObjectReference<ThemeSwitcher>? _objRef;
    private bool _initialized;

    private record ThemeOption(string Id, string Name, bool IsDark);

    private static readonly ThemeOption[] Themes =
    [
        new("light", "Light", false),
        new("dark", "Dark", true),
        new("sepia", "Sepia", false),
        new("nord", "Nord", true),
        new("solarized-light", "Solarized Light", false),
        new("dracula", "Dracula", true)
    ];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !_initialized)
        {
            _initialized = true;
            _objRef = DotNetObjectReference.Create(this);

            try
            {
                _currentTheme = await JS.InvokeAsync<string>("themeManager.init", _objRef);

                await JS.InvokeVoidAsync("themeManager.registerClickOutside", _objRef);

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Theme initialization error");
            }
        }
    }

    private void ToggleMenu() => _isOpen = !_isOpen;

    [JSInvokable]
    public void CloseMenu()
    {
        if (_isOpen)
        {
            _isOpen = false;
            StateHasChanged();
        }
    }

    private async Task SelectThemeAsync(string themeId)
    {
        _currentTheme = themeId;
        _isOpen = false;

        try
        {
            await JS.InvokeVoidAsync("themeManager.setTheme", themeId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Theme selection error");
        }
    }

    private static bool IsDarkTheme(string themeId) => Themes.Any(t => t.Id == themeId && t.IsDark);

    public void Dispose() => _objRef?.Dispose();
}
```

```css src/MyBlog.Web/Components/Shared/ThemeSwitcher.razor.css

.theme-switcher {
    position: relative;
    display: inline-block;
}

.theme-switcher-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 40px;
    height: 40px;
    padding: 0;
    background-color: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: 50%;
    color: var(--color-text);
    cursor: pointer;
    transition: all 150ms ease;
}

.theme-switcher-btn:hover {
    background-color: var(--color-bg-elevated);
    border-color: var(--color-primary-muted);
    color: var(--color-primary);
}

.theme-switcher-btn:focus-visible {
    outline: 2px solid var(--color-focus-ring);
    outline-offset: 2px;
}

.theme-switcher-btn svg {
    width: 20px;
    height: 20px;
    flex-shrink: 0;
}

.theme-menu {
    position: absolute;
    top: calc(100% + 8px);
    right: 0;
    min-width: 180px;
    background-color: var(--color-bg);
    border: 1px solid var(--color-border);
    border-radius: 0.5rem;
    box-shadow: 0 8px 24px var(--color-shadow);
    padding: 0.5rem;
    z-index: 1000;
    opacity: 0;
    visibility: hidden;
    transform: translateY(-8px);
    transition: all 150ms ease;
}

.theme-menu.open {
    opacity: 1;
    visibility: visible;
    transform: translateY(0);
}

.theme-option {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    width: 100%;
    padding: 0.625rem 0.75rem;
    background: none;
    border: none;
    border-radius: 0.375rem;
    color: var(--color-text);
    cursor: pointer;
    font: inherit;
    font-size: 0.9rem;
    text-align: left;
    transition: background-color 150ms ease;
}

.theme-option:hover {
    background-color: var(--color-bg-alt);
}

.theme-option:focus-visible {
    outline: 2px solid var(--color-focus-ring);
    outline-offset: -2px;
}

.theme-option.active {
    background-color: var(--color-primary);
    color: white;
}

.theme-option .theme-preview {
    width: 20px;
    height: 20px;
    border-radius: 50%;
    border: 2px solid var(--color-border);
    flex-shrink: 0;
}

.theme-option.active .theme-preview {
    border-color: rgba(255, 255, 255, 0.5);
}

.theme-preview-light {
    background: linear-gradient(135deg, #ffffff 50%, #2563eb 50%);
}

.theme-preview-dark {
    background: linear-gradient(135deg, #0f172a 50%, #60a5fa 50%);
}

.theme-preview-sepia {
    background: linear-gradient(135deg, #faf6f1 50%, #a0522d 50%);
}

.theme-preview-nord {
    background: linear-gradient(135deg, #2e3440 50%, #88c0d0 50%);
}

.theme-preview-solarized-light {
    background: linear-gradient(135deg, #fdf6e3 50%, #268bd2 50%);
}

.theme-preview-dracula {
    background: linear-gradient(135deg, #282a36 50%, #bd93f9 50%);
}

@media (max-width: 768px) {
    .theme-menu {
        right: -20px;
        min-width: 160px;
    }
}

@media (max-width: 480px) {
    .theme-menu {
        position: fixed;
        top: auto;
        bottom: 0;
        left: 0;
        right: 0;
        border-radius: 1rem 1rem 0 0;
        padding: 1rem;
        transform: translateY(100%);
    }

    .theme-menu.open {
        transform: translateY(0);
    }

    .theme-option {
        padding: 1rem;
    }
}
```

```dockerfile src/MyBlog.Web/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY MyBlog.Core/MyBlog.Core.csproj MyBlog.Core/
COPY MyBlog.Infrastructure/MyBlog.Infrastructure.csproj MyBlog.Infrastructure/
COPY MyBlog.Web/MyBlog.Web.csproj MyBlog.Web/

RUN dotnet restore MyBlog.Web/MyBlog.Web.csproj

COPY MyBlog.Core/ MyBlog.Core/
COPY MyBlog.Infrastructure/ MyBlog.Infrastructure/
COPY MyBlog.Web/ MyBlog.Web/

RUN dotnet publish MyBlog.Web/MyBlog.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

RUN useradd -m -s /bin/bash appuser && mkdir -p /app/data && chown appuser:appuser /app/data

COPY --from=build --chown=appuser:appuser /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/myblog.db"

USER appuser

EXPOSE 5000

ENTRYPOINT ["dotnet", "MyBlog.Web.dll"]
```

```csharp src/MyBlog.Web/Endpoints/RssEndpoints.cs
using System.Text;
using System.Xml;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;

namespace MyBlog.Web.Endpoints;

public static class RssEndpoints
{
    public const int FeedItemLimit = 20;

    private const string AtomNamespace = "http://www.w3.org/2005/Atom";
    private const string ContentNamespace = "http://purl.org/rss/1.0/modules/content/";

    public static IEndpointRouteBuilder MapRssEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/feed.xml", GenerateRssFeed)
            .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(10)));

        return endpoints;
    }

    private static async Task<IResult> GenerateRssFeed(
        HttpContext context,
        IPostRepository postRepository,
        IConfiguration configuration,
        IMarkdownService markdownService)
    {
        var request = context.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}";
        var siteTitle = configuration["Application:Title"] ?? "MyBlog";

        var bytes = await BuildFeedAsync(baseUrl, siteTitle, postRepository, markdownService, context.RequestAborted);
        return Results.Bytes(bytes, "application/rss+xml; charset=utf-8");
    }

    public static async Task<byte[]> BuildFeedAsync(
        string baseUrl,
        string siteTitle,
        IPostRepository postRepository,
        IMarkdownService markdownService,
        CancellationToken cancellationToken = default)
    {
        var feedUrl = $"{baseUrl}/feed.xml";

        var (posts, _) = await postRepository.GetPublishedPostsAsync(1, FeedItemLimit, cancellationToken);

        var fullPosts = new List<(PostListItemDto ListItem, PostDetailDto? Detail)>();
        foreach (var post in posts)
        {
            var detail = await postRepository.GetBySlugAsync(post.Slug, cancellationToken);
            fullPosts.Add((post, detail));
        }

        var settings = new XmlWriterSettings
        {
            Async = true,
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        using var stream = new MemoryStream();
        await using (var writer = XmlWriter.Create(stream, settings))
        {
            await writer.WriteStartDocumentAsync();

            await writer.WriteStartElementAsync(null, "rss", null);
            await writer.WriteAttributeStringAsync(null, "version", null, "2.0");
            await writer.WriteAttributeStringAsync("xmlns", "atom", null, AtomNamespace);
            await writer.WriteAttributeStringAsync("xmlns", "content", null, ContentNamespace);

            await writer.WriteStartElementAsync(null, "channel", null);

            await writer.WriteElementStringAsync(null, "title", null, siteTitle);
            await writer.WriteElementStringAsync(null, "link", null, baseUrl);
            await writer.WriteElementStringAsync(null, "description", null, $"{siteTitle} — Recent posts");
            await writer.WriteElementStringAsync(null, "language", null, "en-us");
            await writer.WriteElementStringAsync(null, "generator", null, "MyBlog/.NET 10");
            await writer.WriteElementStringAsync(null, "lastBuildDate", null, DateTime.UtcNow.ToString("R"));

            await writer.WriteStartElementAsync("atom", "link", AtomNamespace);
            await writer.WriteAttributeStringAsync(null, "href", null, feedUrl);
            await writer.WriteAttributeStringAsync(null, "rel", null, "self");
            await writer.WriteAttributeStringAsync(null, "type", null, "application/rss+xml");
            await writer.WriteEndElementAsync();

            foreach (var (post, detail) in fullPosts)
            {
                var permalink = $"{baseUrl}/post/{post.Slug}";

                await writer.WriteStartElementAsync(null, "item", null);

                await writer.WriteElementStringAsync(null, "title", null, post.Title);
                await writer.WriteElementStringAsync(null, "link", null, permalink);
                await writer.WriteElementStringAsync(null, "description", null, post.Summary);

                if (post.PublishedAtUtc.HasValue)
                {
                    await writer.WriteElementStringAsync(null, "pubDate", null, post.PublishedAtUtc.Value.ToString("R"));
                }

                await writer.WriteElementStringAsync(null, "author", null, post.AuthorDisplayName);

                await writer.WriteStartElementAsync(null, "guid", null);
                await writer.WriteAttributeStringAsync(null, "isPermaLink", null, "true");
                await writer.WriteStringAsync(permalink);
                await writer.WriteEndElementAsync();

                if (detail is not null)
                {
                    var htmlContent = await markdownService.ToHtmlAsync(detail.Content);
                    await writer.WriteStartElementAsync("content", "encoded", ContentNamespace);
                    await writer.WriteCDataAsync(htmlContent);
                    await writer.WriteEndElementAsync();
                }

                await writer.WriteEndElementAsync();
            }

            await writer.WriteEndElementAsync();
            await writer.WriteEndElementAsync();
            await writer.WriteEndDocumentAsync();

            await writer.FlushAsync();
        }

        return stream.ToArray();
    }
}
```

```csharp src/MyBlog.Web/Hubs/ReaderHub.cs
using Microsoft.AspNetCore.SignalR;
using MyBlog.Core.Interfaces;

namespace MyBlog.Web.Hubs;

public class ReaderHub(IReaderTrackingService trackingService) : Hub
{
    public async Task JoinPage(string slug)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, slug);

        var newCount = trackingService.JoinPost(slug, Context.ConnectionId);

        await Clients.Group(slug).SendAsync("UpdateCount", newCount);
    }

    public async Task LeavePage(string slug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, slug);

        var newCount = trackingService.LeavePost(slug, Context.ConnectionId);

        await Clients.Group(slug).SendAsync("UpdateCount", newCount);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var (slug, newCount) = trackingService.Disconnect(Context.ConnectionId);

        if (!string.IsNullOrEmpty(slug))
        {
            await Clients.Group(slug).SendAsync("UpdateCount", newCount);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
```

```csharp src/MyBlog.Web/Middleware/LoginRateLimitMiddleware.cs
using System.Collections.Concurrent;

namespace MyBlog.Web.Middleware;

public sealed class LoginRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoginRateLimitMiddleware> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delayFunc;
    private readonly bool _isEnabled;

    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Attempts = new();

    private const int WindowMinutes = 15;
    private const int AttemptsBeforeDelay = 5;
    private const int MaxDelaySeconds = 30;

    private const int MaxTrackedIps = 10_000;

    public LoginRateLimitMiddleware(
        RequestDelegate next,
        ILogger<LoginRateLimitMiddleware> logger,
        IWebHostEnvironment environment)
        : this(next, logger, null, !environment.IsDevelopment())
    {
    }

    public LoginRateLimitMiddleware(
        RequestDelegate next,
        ILogger<LoginRateLimitMiddleware> logger,
        Func<TimeSpan, CancellationToken, Task>? delayFunc)
        : this(next, logger, delayFunc, true)
    {
    }

    private LoginRateLimitMiddleware(
        RequestDelegate next,
        ILogger<LoginRateLimitMiddleware> logger,
        Func<TimeSpan, CancellationToken, Task>? delayFunc,
        bool isEnabled)
    {
        _next = next;
        _logger = logger;
        _delayFunc = delayFunc;
        _isEnabled = isEnabled;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_isEnabled || !IsLoginPostRequest(context))
        {
            await _next(context);
            return;
        }

        var ip = GetClientIp(context);
        RecordAttempt(ip);
        var delay = CalculateDelay(ip);
        if (delay > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Rate limiting login attempt from {IP}, delaying {Seconds}s",
                ip, delay.TotalSeconds);
            if (_delayFunc != null)
            {
                await _delayFunc(delay, context.RequestAborted);
            }
            else
            {
                await Task.Delay(delay, context.RequestAborted);
            }
        }

        await _next(context);
    }

    private static bool IsLoginPostRequest(HttpContext context) =>
        context.Request.Method == HttpMethods.Post &&
        context.Request.Path.StartsWithSegments("/account/login", StringComparison.OrdinalIgnoreCase);

    private static string GetClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static TimeSpan CalculateDelay(string ip)
    {
        if (!Attempts.TryGetValue(ip, out var record))
        {
            return TimeSpan.Zero;
        }

        if (DateTime.UtcNow - record.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
        {
            Attempts.TryRemove(ip, out _);
            return TimeSpan.Zero;
        }

        if (record.Count <= AttemptsBeforeDelay)
        {
            return TimeSpan.Zero;
        }

        var delayMultiplier = record.Count - AttemptsBeforeDelay;
        var delaySeconds = Math.Min(Math.Pow(2, delayMultiplier - 1), MaxDelaySeconds);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private static void RecordAttempt(string ip)
    {
        if (Attempts.Count >= MaxTrackedIps && !Attempts.ContainsKey(ip))
        {
            CleanupExpiredEntries();

            if (Attempts.Count >= MaxTrackedIps)
            {
                return;
            }
        }

        Attempts.AddOrUpdate(
            ip,
            _ => (1, DateTime.UtcNow),
            (_, existing) =>
            {
                if (DateTime.UtcNow - existing.WindowStart > TimeSpan.FromMinutes(WindowMinutes))
                {
                    return (1, DateTime.UtcNow);
                }
                return (existing.Count + 1, existing.WindowStart);
            });
    }

    private static void CleanupExpiredEntries()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-WindowMinutes);
        foreach (var kvp in Attempts)
        {
            if (kvp.Value.WindowStart < cutoff)
            {
                Attempts.TryRemove(kvp.Key, out _);
            }
        }
    }

    public static void ClearAttempts() => Attempts.Clear();
}

public static class LoginRateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseLoginRateLimit(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<ILogger<LoginRateLimitMiddleware>>();
        var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        return app.Use(next => new LoginRateLimitMiddleware(next, logger, environment).InvokeAsync);
    }
}
```

```xml src/MyBlog.Web/MyBlog.Web.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <UserSecretsId>4e79e86d-8e95-470e-9b17-af16e5a9e8f0</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
    <PackageReference Include="OpenTelemetry.Exporter.Console" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyBlog.Infrastructure\MyBlog.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

```csharp src/MyBlog.Web/Program.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyBlog.Core.Constants;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Telemetry;
using MyBlog.Web.Components;
using MyBlog.Web.Endpoints;
using MyBlog.Web.Hubs;
using MyBlog.Web.Middleware;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var maxCircuitMessageBytes = builder.Configuration.GetValue(
    "Blazor:MaximumReceiveMessageSizeBytes", 1024 * 1024);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
    })
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = maxCircuitMessageBytes;
    });

builder.Services.AddSignalR();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOutputCache();

var sessionTimeout = builder.Configuration.GetValue("Authentication:SessionTimeoutMinutes", 30);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = AppConstants.AuthCookieName;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionTimeout);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Configuration.GetValue("Application:RequireHttps", false)
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var serviceName = "MyBlog";
var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

var otlpEndpoint = builder.Configuration["Otlp:Endpoint"];
var otlpApiKey = builder.Configuration["Otlp:ApiKey"];
var otlpEnabled = !string.IsNullOrWhiteSpace(otlpEndpoint) && !string.IsNullOrWhiteSpace(otlpApiKey);

var otlpProtocol = OtlpExportProtocol.HttpProtobuf;
var configuredProtocol = builder.Configuration["Otlp:Protocol"];
if (!string.IsNullOrWhiteSpace(configuredProtocol) &&
    configuredProtocol.Equals("Grpc", StringComparison.OrdinalIgnoreCase))
{
    otlpProtocol = OtlpExportProtocol.Grpc;
}

Action<OtlpExporterOptions> configureOtlp = o =>
{
    o.Endpoint = new Uri(otlpEndpoint!);
    o.Protocol = otlpProtocol;
    o.Headers = $"x-honeycomb-team={otlpApiKey}";
};

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();

        if (otlpEnabled)
        {
            tracing.AddOtlpExporter(configureOtlp);
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();

        if (otlpEnabled)
        {
            metrics.AddOtlpExporter(configureOtlp);
        }
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddConsoleExporter();

    logging.AddLocalExporters(builder.Configuration);

    if (otlpEnabled)
    {
        logging.AddOtlpExporter(configureOtlp);
    }
});
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseLoginRateLimit();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseOutputCache();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapPost("/account/login", async (HttpContext context, IAuthService authService) =>
{
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect($"/login?error=required&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
    }

    var user = await authService.AuthenticateAsync(username, password);
    if (user is null)
    {
        return Results.Redirect($"/login?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Username),
        new("DisplayName", user.DisplayName),
        new(ClaimTypes.Role, AppConstants.AdminRole)
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

    return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/admin" : returnUrl);
});
app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();
app.MapGet("/api/images/{id:guid}", async (Guid id, IImageRepository imageRepository) =>
{
    var image = await imageRepository.GetByIdAsync(id);
    if (image is null)
    {
        return Results.NotFound();
    }
    return Results.File(image.Data, image.ContentType);
});

app.MapRssEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<ReaderHub>("/readerHub");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await context.Database.EnsureCreatedAsync();
    await DatabaseSchemaUpdater.ApplyUpdatesAsync(context);

    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureAdminUserAsync();
}

app.Run();
```

```json src/MyBlog.Web/Properties/launchSettings.json
{
  "profiles": {
    "MyBlog.Web": {
      "commandName": "Project",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "applicationUrl": "https://localhost:51226;http://localhost:51227"
    }
  }
}
```

```css src/MyBlog.Web/wwwroot/css/site.css

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

[data-theme="dark"] {
    --color-bg: #0f172a;
    --color-bg-alt: #1e293b;
    --color-bg-elevated: #334155;
    --color-text: #e2e8f0;
    --color-text-muted: #94a3b8;
    --color-primary: #60a5fa;
    --color-primary-hover: #93c5fd;
    --color-primary-muted: #3b82f6;
    --color-border: #334155;
    --color-border-light: #475569;
    --color-danger: #f87171;
    --color-danger-hover: #fca5a5;
    --color-success: #34d399;
    --color-success-hover: #6ee7b7;
    --color-warning: #fbbf24;
    --color-info: #22d3ee;
    --color-code-bg: #1e293b;
    --color-blockquote-border: #60a5fa;
    --color-selection-bg: #1e3a5f;
    --color-selection-text: #bfdbfe;
    --color-focus-ring: #60a5fa;
    --color-shadow: rgba(0, 0, 0, 0.3);
    --color-overlay: rgba(0, 0, 0, 0.7);
    color-scheme: dark;
}

[data-theme="sepia"] {
    --color-bg: #f4ecd8;
    --color-bg-alt: #e8dfc8;
    --color-bg-elevated: #f4ecd8;
    --color-text: #433422;
    --color-text-muted: #6b5a45;
    --color-primary: #8b4513;
    --color-primary-hover: #a0522d;
    --color-primary-muted: #cd853f;
    --color-border: #c4b8a0;
    --color-border-light: #d4c8b0;
    --color-danger: #b22222;
    --color-danger-hover: #dc143c;
    --color-success: #228b22;
    --color-success-hover: #2e8b57;
    --color-warning: #b8860b;
    --color-info: #4682b4;
    --color-code-bg: #e8dfc8;
    --color-blockquote-border: #8b4513;
    --color-selection-bg: #deb887;
    --color-selection-text: #433422;
    --color-focus-ring: #8b4513;
    --color-shadow: rgba(67, 52, 34, 0.15);
    --color-overlay: rgba(67, 52, 34, 0.5);
    color-scheme: light;
}

[data-theme="nord"] {
    --color-bg: #2e3440;
    --color-bg-alt: #3b4252;
    --color-bg-elevated: #434c5e;
    --color-text: #eceff4;
    --color-text-muted: #d8dee9;
    --color-primary: #88c0d0;
    --color-primary-hover: #8fbcbb;
    --color-primary-muted: #81a1c1;
    --color-border: #4c566a;
    --color-border-light: #434c5e;
    --color-danger: #bf616a;
    --color-danger-hover: #d08770;
    --color-success: #a3be8c;
    --color-success-hover: #8fbcbb;
    --color-warning: #ebcb8b;
    --color-info: #81a1c1;
    --color-code-bg: #3b4252;
    --color-blockquote-border: #88c0d0;
    --color-selection-bg: #4c566a;
    --color-selection-text: #eceff4;
    --color-focus-ring: #88c0d0;
    --color-shadow: rgba(0, 0, 0, 0.3);
    --color-overlay: rgba(0, 0, 0, 0.6);
    color-scheme: dark;
}

[data-theme="solarized-light"] {
    --color-bg: #fdf6e3;
    --color-bg-alt: #eee8d5;
    --color-bg-elevated: #fdf6e3;
    --color-text: #657b83;
    --color-text-muted: #93a1a1;
    --color-primary: #268bd2;
    --color-primary-hover: #2aa198;
    --color-primary-muted: #6c71c4;
    --color-border: #93a1a1;
    --color-border-light: #eee8d5;
    --color-danger: #dc322f;
    --color-danger-hover: #cb4b16;
    --color-success: #859900;
    --color-success-hover: #2aa198;
    --color-warning: #b58900;
    --color-info: #2aa198;
    --color-code-bg: #eee8d5;
    --color-blockquote-border: #268bd2;
    --color-selection-bg: #eee8d5;
    --color-selection-text: #657b83;
    --color-focus-ring: #268bd2;
    --color-shadow: rgba(0, 0, 0, 0.1);
    --color-overlay: rgba(0, 0, 0, 0.4);
    color-scheme: light;
}

[data-theme="dracula"] {
    --color-bg: #282a36;
    --color-bg-alt: #44475a;
    --color-bg-elevated: #44475a;
    --color-text: #f8f8f2;
    --color-text-muted: #6272a4;
    --color-primary: #bd93f9;
    --color-primary-hover: #ff79c6;
    --color-primary-muted: #8be9fd;
    --color-border: #44475a;
    --color-border-light: #6272a4;
    --color-danger: #ff5555;
    --color-danger-hover: #ff6e6e;
    --color-success: #50fa7b;
    --color-success-hover: #69ff94;
    --color-warning: #f1fa8c;
    --color-info: #8be9fd;
    --color-code-bg: #44475a;
    --color-blockquote-border: #bd93f9;
    --color-selection-bg: #44475a;
    --color-selection-text: #f8f8f2;
    --color-focus-ring: #bd93f9;
    --color-shadow: rgba(0, 0, 0, 0.4);
    --color-overlay: rgba(0, 0, 0, 0.7);
    color-scheme: dark;
}

:root {
    --max-width: 900px;
    --spacing: 1rem;
    --radius: 0.375rem;
    --radius-lg: 0.5rem;
    --transition-fast: 0.15s ease;
    --transition-normal: 0.3s ease;
}

*, *::before, *::after {
    box-sizing: border-box;
}

html {
    font-size: 16px;
    line-height: 1.6;
    -webkit-text-size-adjust: 100%;
    -webkit-tap-highlight-color: transparent;
}

body {
    margin: 0;
    font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
    background-color: var(--color-bg);
    color: var(--color-text);
    transition: background-color var(--transition-normal), color var(--transition-normal);
}

::selection {
    background-color: var(--color-selection-bg);
    color: var(--color-selection-text);
}

:focus-visible {
    outline: 2px solid var(--color-focus-ring);
    outline-offset: 2px;
}

:focus:not(:focus-visible) {
    outline: none;
}

h1, h2, h3, h4, h5, h6 {
    margin-top: 0;
    margin-bottom: 0.5em;
    font-weight: 600;
    line-height: 1.2;
    color: var(--color-text);
}

h1 { font-size: 2.5rem; }
h2 { font-size: 2rem; }
h3 { font-size: 1.5rem; }
h4 { font-size: 1.25rem; }
h5 { font-size: 1.125rem; }
h6 { font-size: 1rem; }

p {
    margin-top: 0;
    margin-bottom: 1rem;
}

a {
    color: var(--color-primary);
    text-decoration: none;
    transition: color var(--transition-fast);
}

a:hover {
    color: var(--color-primary-hover);
    text-decoration: underline;
}

.container {
    max-width: var(--max-width);
    margin: 0 auto;
    padding: 0 var(--spacing);
}

.layout {
    min-height: 100vh;
    display: flex;
    flex-direction: column;
}

.header {
    background-color: var(--color-bg-alt);
    border-bottom: 1px solid var(--color-border);
    padding: var(--spacing) 0;
    transition: background-color var(--transition-normal), border-color var(--transition-normal);
}

.header .container {
    display: flex;
    justify-content: space-between;
    align-items: center;
    flex-wrap: wrap;
    gap: var(--spacing);
}

.logo {
    font-size: 1.5rem;
    font-weight: 700;
    color: var(--color-text);
    transition: color var(--transition-fast);
}

.logo:hover {
    color: var(--color-primary);
    text-decoration: none;
}

.nav {
    display: flex;
    gap: var(--spacing);
    align-items: center;
    flex-wrap: wrap;
}

.nav a {
    color: var(--color-text);
    font-weight: 500;
    padding: 0.5rem;
    border-radius: var(--radius);
    transition: color var(--transition-fast), background-color var(--transition-fast);
    min-height: 44px;
    display: inline-flex;
    align-items: center;
}

.nav a:hover {
    color: var(--color-primary);
    text-decoration: none;
}

.logout-form {
    display: inline;
}

.logout-form button {
    background: none;
    border: none;
    color: var(--color-primary);
    cursor: pointer;
    font: inherit;
    padding: 0.5rem;
    min-height: 44px;
    transition: color var(--transition-fast);
}

.logout-form button:hover {
    color: var(--color-primary-hover);
    text-decoration: underline;
}

.main {
    flex: 1;
    padding: calc(var(--spacing) * 2) 0;
}

.btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 0.5rem;
    padding: 0.625rem 1.25rem;
    background-color: var(--color-bg-alt);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    color: var(--color-text);
    cursor: pointer;
    font: inherit;
    font-weight: 500;
    text-decoration: none;
    transition: all var(--transition-fast);
    min-height: 44px;
}

.btn:hover {
    background-color: var(--color-bg-elevated);
    border-color: var(--color-border-light);
    text-decoration: none;
}

.btn:active {
    transform: translateY(1px);
}

.btn-primary {
    background-color: var(--color-primary);
    border-color: var(--color-primary);
    color: white;
}

.btn-primary:hover {
    background-color: var(--color-primary-hover);
    border-color: var(--color-primary-hover);
    color: white;
}

.btn-danger {
    background-color: var(--color-danger);
    border-color: var(--color-danger);
    color: white;
}

.btn-danger:hover {
    background-color: var(--color-danger-hover);
    border-color: var(--color-danger-hover);
    color: white;
}

.btn-success {
    background-color: var(--color-success);
    border-color: var(--color-success);
    color: white;
}

.btn-success:hover {
    background-color: var(--color-success-hover);
    border-color: var(--color-success-hover);
    color: white;
}

.btn-link {
    background: none;
    border: none;
    color: var(--color-primary);
    cursor: pointer;
    padding: 0.5rem;
    font: inherit;
    min-height: 44px;
}

.btn-link:hover {
    color: var(--color-primary-hover);
    text-decoration: underline;
}

.btn-link.danger {
    color: var(--color-danger);
}

.btn-link.danger:hover {
    color: var(--color-danger-hover);
}

.btn-sm {
    padding: 0.375rem 0.75rem;
    font-size: 0.875rem;
    min-height: 36px;
}

.btn-icon {
    padding: 0.5rem;
    border-radius: 50%;
    line-height: 1;
    min-width: 44px;
    min-height: 44px;
}

.form-group {
    margin-bottom: var(--spacing);
}

.form-group label {
    display: block;
    margin-bottom: 0.375rem;
    font-weight: 500;
    color: var(--color-text);
}

.form-group input[type="text"],
.form-group input[type="password"],
.form-group input[type="email"],
.form-group textarea,
.form-group select {
    width: 100%;
    padding: 0.75rem;
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    font: inherit;
    font-size: 16px;
    color: var(--color-text);
    background-color: var(--color-bg);
    transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
    -webkit-appearance: none;
    appearance: none;
}

.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {
    border-color: var(--color-primary);
    box-shadow: 0 0 0 3px var(--color-selection-bg);
    outline: none;
}

.form-group textarea {
    resize: vertical;
    min-height: 120px;
    line-height: 1.5;
}

.form-group.checkbox label {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    cursor: pointer;
    padding: 0.5rem 0;
    min-height: 44px;
}

.form-group.checkbox input {
    width: 20px;
    height: 20px;
    min-width: 20px;
    accent-color: var(--color-primary);
    cursor: pointer;
}

.form-actions {
    display: flex;
    gap: 0.75rem;
    margin-top: calc(var(--spacing) * 1.5);
    flex-wrap: wrap;
}

.error-message {
    background-color: color-mix(in srgb, var(--color-danger) 15%, transparent);
    border: 1px solid var(--color-danger);
    color: var(--color-danger);
    padding: var(--spacing);
    border-radius: var(--radius);
    margin-bottom: var(--spacing);
}

.success-message {
    background-color: color-mix(in srgb, var(--color-success) 15%, transparent);
    border: 1px solid var(--color-success);
    color: var(--color-success);
    padding: var(--spacing);
    border-radius: var(--radius);
    margin-bottom: var(--spacing);
}

.post-list {
    display: flex;
    flex-direction: column;
    gap: calc(var(--spacing) * 2);
}

.post-card {
    padding: calc(var(--spacing) * 1.5);
    border: 1px solid var(--color-border);
    border-radius: var(--radius-lg);
    background-color: var(--color-bg);
    transition: border-color var(--transition-fast), box-shadow var(--transition-fast);
}

.post-card:hover {
    border-color: var(--color-primary-muted);
    box-shadow: 0 4px 12px var(--color-shadow);
}

.post-card-title {
    margin-bottom: 0.5rem;
}

.post-card-title a {
    color: var(--color-text);
    font-weight: 600;
}

.post-card-title a:hover {
    color: var(--color-primary);
}

.post-card-meta {
    color: var(--color-text-muted);
    font-size: 0.875rem;
    margin-bottom: 0.75rem;
    display: flex;
    flex-wrap: wrap;
    gap: 1rem;
}

.draft-badge {
    background-color: var(--color-warning);
    color: #1a1a2e;
    padding: 0.125rem 0.5rem;
    border-radius: var(--radius);
    font-size: 0.75rem;
    font-weight: 600;
}

.read-more {
    font-weight: 500;
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.5rem 0;
    min-height: 44px;
}

.read-more::after {
    content: "→";
    transition: transform var(--transition-fast);
}

.read-more:hover::after {
    transform: translateX(4px);
}

.pagination {
    display: flex;
    justify-content: center;
    align-items: center;
    gap: var(--spacing);
    margin-top: calc(var(--spacing) * 2);
    flex-wrap: wrap;
}

.pagination .page-link {
    min-height: 44px;
    display: inline-flex;
    align-items: center;
    padding: 0.5rem 1rem;
}

.page-info {
    color: var(--color-text-muted);
    font-weight: 500;
}

.post-detail {
    max-width: 100%;
}

.post-header {
    margin-bottom: calc(var(--spacing) * 2);
    padding-bottom: var(--spacing);
    border-bottom: 1px solid var(--color-border);
}

.post-meta {
    color: var(--color-text-muted);
    font-size: 0.875rem;
    display: flex;
    flex-wrap: wrap;
    gap: 1rem;
    align-items: center;
}

.post-content {
    line-height: 1.8;
}

.back-link {
    display: inline-flex;
    align-items: center;
    margin-top: calc(var(--spacing) * 2);
    padding: 0.5rem 0;
    min-height: 44px;
}

.markdown-content {
    line-height: 1.8;
}

.markdown-content h1,
.markdown-content h2,
.markdown-content h3 {
    margin-top: 1.5em;
}

.markdown-content pre {
    background-color: var(--color-code-bg);
    border: 1px solid var(--color-border);
    border-radius: var(--radius);
    padding: var(--spacing);
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
}

.markdown-content code {
    background-color: var(--color-code-bg);
    padding: 0.125rem 0.375rem;
    border-radius: 0.25rem;
    font-family: ui-monospace, 'SF Mono', SFMono-Regular, Menlo, Monaco, Consolas, monospace;
    font-size: 0.875em;
}

.markdown-content pre code {
    background: none;
    padding: 0;
}

.markdown-content blockquote {
    border-left: 4px solid var(--color-blockquote-border);
    margin: var(--spacing) 0;
    padding: 0.5rem 0 0.5rem var(--spacing);
    color: var(--color-text-muted);
    font-style: italic;
}

.markdown-content img {
    max-width: 100%;
    height: auto;
    border-radius: var(--radius);
}

.markdown-content ul,
.markdown-content ol {
    padding-left: 1.5rem;
}

.markdown-content li {
    margin-bottom: 0.5rem;
}

.markdown-content a {
    text-decoration: underline;
    text-underline-offset: 2px;
}

.admin-table {
    width: 100%;
    border-collapse: collapse;
}

.admin-table th,
.admin-table td {
    padding: 0.75rem;
    text-align: left;
    border-bottom: 1px solid var(--color-border);
}

.admin-table th {
    background-color: var(--color-bg-alt);
    font-weight: 600;
    color: var(--color-text);
}

.admin-table tr:hover td {
    background-color: var(--color-bg-alt);
}

.success { color: var(--color-success); }
.error { color: var(--color-danger); }
.warning { color: var(--color-warning); }
.info { color: var(--color-info); }
.muted { color: var(--color-text-muted); }

.visually-hidden {
    position: absolute;
    width: 1px;
    height: 1px;
    padding: 0;
    margin: -1px;
    overflow: hidden;
    clip: rect(0, 0, 0, 0);
    white-space: nowrap;
    border: 0;
}

.skip-link {
    position: absolute;
    top: -100%;
    left: 0;
    background-color: var(--color-primary);
    color: white;
    padding: 0.5rem 1rem;
    z-index: 9999;
    transition: top var(--transition-fast);
}

.skip-link:focus {
    top: 0;
}

.footer {
    background-color: var(--color-bg-alt);
    border-top: 1px solid var(--color-border);
    padding: calc(var(--spacing) * 1.5) 0;
    margin-top: auto;
    text-align: center;
    color: var(--color-text-muted);
    font-size: 0.875rem;
}

.footer a {
    color: var(--color-primary);
}

.reader-badge {
    display: inline-flex;
    align-items: center;
    gap: 0.375rem;
    font-size: 0.875rem;
    color: var(--color-text-muted);
}

.reader-dot {
    color: var(--color-success);
    animation: pulse 2s ease-in-out infinite;
}

@keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.5; }
}

@media (prefers-reduced-motion: reduce) {
    *,
    *::before,
    *::after {
        animation-duration: 0.01ms !important;
        animation-iteration-count: 1 !important;
        transition-duration: 0.01ms !important;
        scroll-behavior: auto !important;
    }
}

@media (max-width: 768px) {
    html { font-size: 15px; }
    h1 { font-size: 1.75rem; }
    h2 { font-size: 1.5rem; }

    .header .container {
        flex-direction: column;
        text-align: center;
        gap: 0.75rem;
    }

    .nav {
        flex-wrap: wrap;
        justify-content: center;
        gap: 0.5rem;
    }

    .post-meta {
        flex-direction: column;
        gap: 0.5rem;
        align-items: flex-start;
    }

    .form-actions {
        flex-direction: column;
    }

    .form-actions .btn {
        width: 100%;
        justify-content: center;
    }
}

@media (max-width: 480px) {
    :root {
        --spacing: 0.875rem;
    }

    h1 { font-size: 1.5rem; }
    h2 { font-size: 1.25rem; }

    .pagination {
        flex-direction: column;
        gap: 0.75rem;
    }

    .post-card {
        padding: var(--spacing);
    }

    .container {
        padding: 0 calc(var(--spacing) * 0.75);
    }
}

@supports (padding: env(safe-area-inset-bottom)) {
    .footer {
        padding-bottom: calc(var(--spacing) * 1.5 + env(safe-area-inset-bottom));
    }

    .header {
        padding-top: calc(var(--spacing) + env(safe-area-inset-top));
    }

    .container {
        padding-left: max(var(--spacing), env(safe-area-inset-left));
        padding-right: max(var(--spacing), env(safe-area-inset-right));
    }
}
```

```javascript src/MyBlog.Web/wwwroot/js/site.js
window.themeManager = {
    storageKey: 'myblog-theme',
    defaultTheme: 'light',
    dotNetRef: null,

    themes: {
        'light': { isDark: false },
        'dark': { isDark: true },
        'sepia': { isDark: false },
        'nord': { isDark: true },
        'solarized-light': { isDark: false },
        'dracula': { isDark: true }
    },

    initialTheme: function() {
        var theme = localStorage.getItem(this.storageKey);

        if (!theme) {
            var prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
            theme = prefersDark ? 'dark' : 'light';
        }

        return this.themes[theme] ? theme : this.defaultTheme;
    },

    init: function(dotNetRef) {
        this.dotNetRef = dotNetRef;

        var savedTheme = this.initialTheme();

        this.applyTheme(savedTheme);

        var self = this;
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function(e) {
            if (!localStorage.getItem(self.storageKey)) {
                var newTheme = e.matches ? 'dark' : 'light';
                self.setTheme(newTheme);
            }
        });

        return savedTheme;
    },

    applyTheme: function(themeId) {
        document.documentElement.setAttribute('data-theme', themeId);

        var isDark = this.themes[themeId] ? this.themes[themeId].isDark : false;
        this.updateMetaThemeColor(isDark);
    },

    setTheme: function(themeId) {
        if (!this.themes[themeId]) {
            console.warn('Theme "' + themeId + '" not found, using default');
            themeId = this.defaultTheme;
        }

        this.applyTheme(themeId);

        localStorage.setItem(this.storageKey, themeId);

        this.announceThemeChange(themeId);
    },

    updateMetaThemeColor: function(isDark) {
        var metaThemeColor = document.querySelector('meta[name="theme-color"]');

        if (!metaThemeColor) {
            metaThemeColor = document.createElement('meta');
            metaThemeColor.name = 'theme-color';
            document.head.appendChild(metaThemeColor);
        }

        metaThemeColor.content = isDark ? '#0f172a' : '#f8f9fa';
    },

    announceThemeChange: function(themeId) {
        var themeName = themeId.split('-').map(function(w) {
            return w.charAt(0).toUpperCase() + w.slice(1);
        }).join(' ');

        var announcer = document.getElementById('theme-announcer');
        if (!announcer) {
            announcer = document.createElement('div');
            announcer.id = 'theme-announcer';
            announcer.setAttribute('role', 'status');
            announcer.setAttribute('aria-live', 'polite');
            announcer.setAttribute('aria-atomic', 'true');
            announcer.className = 'visually-hidden';
            document.body.appendChild(announcer);
        }

        announcer.textContent = 'Theme changed to ' + themeName;
    },

    registerClickOutside: function(dotNetRef) {
        this.dotNetRef = dotNetRef;
        var self = this;

        document.addEventListener('click', function(e) {
            var themeSwitcher = e.target.closest('.theme-switcher');
            if (!themeSwitcher && self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('CloseMenu');
            }
        });

        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape' && self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('CloseMenu');
            }
        });
    }
};

function sharePost(title) {
    var url = window.location.href;
    var shareBtn = document.querySelector('.share-btn');

    if (typeof navigator.share === 'function' && window.isSecureContext) {
        navigator.share({
            title: title,
            text: title,
            url: url
        }).then(function() {
            showShareSuccess(shareBtn, 'Shared!');
        }).catch(function(err) {
            if (err.name !== 'AbortError') {
                console.warn('Share failed, falling back to clipboard:', err.message);
                copyToClipboard(url, shareBtn);
            }
        });
    } else {
        copyToClipboard(url, shareBtn);
    }
}

function copyToClipboard(url, shareBtn) {
    if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
        navigator.clipboard.writeText(url).then(function() {
            showShareSuccess(shareBtn, 'Link copied!');
        }).catch(function(err) {
            console.error('Clipboard API failed:', err);
            fallbackCopyToClipboard(url, shareBtn);
        });
    } else {
        fallbackCopyToClipboard(url, shareBtn);
    }
}

function fallbackCopyToClipboard(url, shareBtn) {
    var textArea = document.createElement('textarea');
    textArea.value = url;
    
    textArea.style.cssText = 'position:fixed;top:0;left:0;width:2em;height:2em;padding:0;border:none;outline:none;box-shadow:none;background:transparent;';
    
    document.body.appendChild(textArea);
    
    if (navigator.userAgent.match(/ipad|iphone/i)) {
        var range = document.createRange();
        range.selectNodeContents(textArea);
        var selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
        textArea.setSelectionRange(0, 999999);
    } else {
        textArea.focus();
        textArea.select();
    }

    var successful = false;
    try {
        successful = document.execCommand('copy');
    } catch (err) {
        console.error('execCommand copy failed:', err);
    }

    document.body.removeChild(textArea);

    if (successful) {
        showShareSuccess(shareBtn, 'Link copied!');
    } else {
        showShareError(shareBtn, 'Copy failed');
    }
}

function showShareSuccess(shareBtn, message) {
    if (!shareBtn) return;

    var originalHTML = shareBtn.innerHTML;
    shareBtn.classList.add('success');
    shareBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg> ' + message;

    setTimeout(function() {
        shareBtn.classList.remove('success');
        shareBtn.innerHTML = originalHTML;
    }, 2000);
}

function showShareError(shareBtn, message) {
    if (!shareBtn) return;

    var originalHTML = shareBtn.innerHTML;
    shareBtn.classList.add('error');
    shareBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg> ' + message;

    setTimeout(function() {
        shareBtn.classList.remove('error');
        shareBtn.innerHTML = originalHTML;
    }, 2000);
}

window.themeManager.applyTheme(window.themeManager.initialTheme());
```

