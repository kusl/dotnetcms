# E2E Test Runner with Playwright
# Single stage using .NET 10 SDK for both build and run
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble

WORKDIR /src

# Install Playwright browser dependencies (Chromium) for Ubuntu Noble
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget \
    curl \
    gnupg \
    ca-certificates \
    fonts-liberation \
    libasound2t64 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libatspi2.0-0 \
    libcairo2 \
    libcups2 \
    libdbus-1-3 \
    libdrm2 \
    libgbm1 \
    libglib2.0-0 \
    libgtk-3-0 \
    libnspr4 \
    libnss3 \
    libpango-1.0-0 \
    libx11-6 \
    libxcb1 \
    libxcomposite1 \
    libxdamage1 \
    libxext6 \
    libxfixes3 \
    libxkbcommon0 \
    libxrandr2 \
    xdg-utils \
    && rm -rf /var/lib/apt/lists/*

# Copy all project files first for restore
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

# Copy all source code
COPY . .

# Build the E2E project (with restore to be safe)
RUN dotnet build MyBlog.E2E/MyBlog.E2E.csproj -c Release

# Install Playwright CLI and browsers
RUN dotnet tool install --global Microsoft.Playwright.CLI --version 1.57.0
ENV PATH="$PATH:/root/.dotnet/tools"
RUN playwright install chromium

WORKDIR /src/MyBlog.E2E

# Set environment variables
ENV MYBLOG_BASE_URL=http://myblog-web:5000
ENV PLAYWRIGHT_HEADLESS=true

# Run tests using dotnet run (xUnit v3 style)
ENTRYPOINT ["dotnet", "run", "-c", "Release", "--no-build"]
