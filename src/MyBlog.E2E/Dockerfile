# E2E Test Runner with Playwright
# Uses the official Playwright .NET image which includes browsers
FROM mcr.microsoft.com/playwright/dotnet:v1.49.0-noble

WORKDIR /src

# Install .NET 10 SDK (Playwright image has .NET 8)
RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet && \
    rm dotnet-install.sh

# Copy project files for restore
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY MyBlog.Core/MyBlog.Core.csproj MyBlog.Core/
COPY MyBlog.Infrastructure/MyBlog.Infrastructure.csproj MyBlog.Infrastructure/
COPY MyBlog.Web/MyBlog.Web.csproj MyBlog.Web/
COPY MyBlog.E2E/MyBlog.E2E.csproj MyBlog.E2E/

# Restore E2E project and all dependencies
RUN dotnet restore MyBlog.E2E/MyBlog.E2E.csproj

# Copy all source code
COPY . .

# Build the E2E project
RUN dotnet build MyBlog.E2E/MyBlog.E2E.csproj -c Release --no-restore

WORKDIR /src/MyBlog.E2E

# Set environment variables
ENV MYBLOG_BASE_URL=http://myblog-web:5000
ENV PLAYWRIGHT_HEADLESS=true

# Run tests using dotnet run (xUnit v3 style)
ENTRYPOINT ["dotnet", "run", "-c", "Release", "--no-build"]
