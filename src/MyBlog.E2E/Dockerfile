# E2E Test Runner Dockerfile
# Uses Playwright to run E2E tests against the MyBlog application
#
# Layer caching strategy:
#   1. System tools (Node.js, PowerShell) — rarely changes
#   2. NuGet restore — changes when .csproj or Directory.Packages.props change
#   3. Playwright browser install — changes when Microsoft.Playwright version changes
#   4. Source code copy + build — changes on every source edit
#
# This ordering ensures the ~280MB Playwright browser download is cached
# based on package versions, NOT source code changes.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Node.js for Playwright browser management
RUN apt-get update && apt-get install -y nodejs npm && rm -rf /var/lib/apt/lists/*

# Install PowerShell (required for Playwright browser installation)
RUN apt-get update && apt-get install -y wget && \
    wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && apt-get install -y powershell && \
    rm -rf /var/lib/apt/lists/*

# Copy project files for restore
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY MyBlog.Core/MyBlog.Core.csproj MyBlog.Core/
COPY MyBlog.Infrastructure/MyBlog.Infrastructure.csproj MyBlog.Infrastructure/
COPY MyBlog.Web/MyBlog.Web.csproj MyBlog.Web/
COPY MyBlog.E2E/MyBlog.E2E.csproj MyBlog.E2E/

# Restore ALL projects to ensure transitive dependencies are available
RUN dotnet restore MyBlog.Core/MyBlog.Core.csproj && \
    dotnet restore MyBlog.Infrastructure/MyBlog.Infrastructure.csproj && \
    dotnet restore MyBlog.Web/MyBlog.Web.csproj && \
    dotnet restore MyBlog.E2E/MyBlog.E2E.csproj

# Install Playwright browsers BEFORE copying source code.
# The playwright.ps1 script is distributed inside the Microsoft.Playwright NuGet
# package and can be invoked directly from the NuGet cache after restore.
# This layer is only rebuilt when the Playwright package version changes
# (via Directory.Packages.props), saving ~280MB of browser downloads on
# every source-only change.
#
# NOTE: The node:NNN [DEP0169] DeprecationWarning about url.parse() is an
# upstream issue in Playwright's npm download scripts. It is harmless and
# cannot be fixed on our side. It will only appear when this layer rebuilds.
RUN PLAYWRIGHT_SCRIPT=$(find /root/.nuget/packages/microsoft.playwright -name 'playwright.ps1' -print -quit 2>/dev/null) && \
    if [ -n "$PLAYWRIGHT_SCRIPT" ]; then \
        pwsh "$PLAYWRIGHT_SCRIPT" install chromium --with-deps; \
    else \
        echo "WARNING: playwright.ps1 not found in NuGet cache; browsers will be installed after build"; \
    fi

# Copy all source code
COPY . .

# Build the E2E project
RUN dotnet build MyBlog.E2E/MyBlog.E2E.csproj -c Release

# Fallback: if browsers were not installed from the NuGet cache step above
# (e.g. the package layout changed), install them from the build output.
RUN if ! ls /root/.cache/ms-playwright/chromium-* 1>/dev/null 2>&1; then \
        echo "Installing Playwright browsers from build output (fallback)..."; \
        pwsh ./MyBlog.E2E/bin/Release/net10.0/playwright.ps1 install chromium --with-deps; \
    fi

WORKDIR /src/MyBlog.E2E

# Set environment variables
ENV MYBLOG_BASE_URL=http://myblog-web:5000
ENV PLAYWRIGHT_HEADLESS=true

# Run tests using dotnet run (xUnit v3 style)
ENTRYPOINT ["dotnet", "run", "-c", "Release", "--no-build"]
