FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY MyBlog.Core/MyBlog.Core.csproj MyBlog.Core/
COPY MyBlog.Infrastructure/MyBlog.Infrastructure.csproj MyBlog.Infrastructure/
COPY MyBlog.Web/MyBlog.Web.csproj MyBlog.Web/
COPY MyBlog.E2E/MyBlog.E2E.csproj MyBlog.E2E/

RUN dotnet restore MyBlog.E2E/MyBlog.E2E.csproj

RUN dotnet build MyBlog.E2E/MyBlog.E2E.csproj -c Release

RUN PLAYWRIGHT_SCRIPT="$(find /root/.nuget/packages/microsoft.playwright -name playwright.ps1 -print -quit)" \
    && test -n "$PLAYWRIGHT_SCRIPT" \
    && pwsh "$PLAYWRIGHT_SCRIPT" install chromium --with-deps \
    && rm -rf /var/lib/apt/lists/*

COPY . .

WORKDIR /src/MyBlog.E2E

ENV MYBLOG_BASE_URL=http://myblog-web:5000
ENV PLAYWRIGHT_HEADLESS=true

ENTRYPOINT ["dotnet", "run", "-c", "Release", "--no-build"]
