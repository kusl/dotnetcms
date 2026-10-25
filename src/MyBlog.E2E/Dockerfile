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
    && printf '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies></PropertyGroup><ItemGroup><PackageReference Include="Microsoft.Playwright" /></ItemGroup></Project>\n' > PlaywrightInstaller/PlaywrightInstaller.csproj \
    && dotnet build PlaywrightInstaller/PlaywrightInstaller.csproj -c Release -o /tmp/playwright -p:TreatWarningsAsErrors=false \
    && test -f /tmp/playwright/Microsoft.Playwright.dll \
    && pwsh /tmp/playwright/playwright.ps1 install chromium --with-deps \
    && find /root/.cache/ms-playwright -maxdepth 1 -name 'chromium*' | grep -q . \
    && rm -rf PlaywrightInstaller /tmp/playwright /var/lib/apt/lists/*

COPY . .

RUN dotnet build MyBlog.E2E/MyBlog.E2E.csproj -c Release

WORKDIR /src/MyBlog.E2E

ENV MYBLOG_BASE_URL=http://myblog-web:5000
ENV PLAYWRIGHT_HEADLESS=true

ENTRYPOINT ["dotnet", "run", "-c", "Release", "--no-build"]
