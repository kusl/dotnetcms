this repository is public and available free of cost 
replace the readme file with a better version when delivering changes 
always make sure this readme is up to date with the latest code changes where possible 
Use your best judgment
If only a few files (under five or there about) need to change, give me the full path of the files that need to change and the FULL file 
if there are a lot of files that need to change 
    Delivery
    Whole project as one tar.gz, unambiguous file placement
    ONLY files that change need to be included in the tar zip
    as I will put it on top of the existing repository
    Any files to delete on updates called out explicitly
No bespoke sed/patch scripts — full files only for anything that changes
Docs updated alongside code changes
Low verbosity, no code comments
I can't emphasize this 
NO CODE COMMENTS 
I have a small export.sh here with this repository 
that creates a dump.txt file 
which I will upload to the Claude project. 
Read this file every time 
and now you know why it is important to not add too much nonsense in the code 
the code should ideally fit within claude project without spilling over to "search mode" 
or worse without fitting in the project memory at all 
remove any code that is unused
give me a single git rm bla bla with all those files please
so please be concise 

don't try to change the coding style 
don't remove braces around the if 
don't change the primary constructor back to whatever you did 
don't make unnecessary changes 
do make the code build, 
do make the tests pass 
and do make everything work properly 
and follow engineering best practices 
and please do not hallucinate 
give me full files for all files that changed 


so for this particular prompt, this is a clean up 
we should NOT introduce any regressions here 
not that we ever intentionally introduce regressions 
but this prompt in particular is a clean up prompt 

clean up should include but not be limited to 
1. updating readme and documentation to match the package updates I have done 
2. inconsistencies and defects such as the following IF they are indeed inconsistencies and defects -- for example why do we get debian packages if we are on noble / ubuntu 24? once again this is an example so don't only fix this one file if you fix something fix it for the whole repo 
3. removal of dead code / code comments
4. failures should be self evident. we should have enough test coverage that defects and failures are very loud and obvious. add/modify tests if necessary 

```dockerfile /home/kushal/src/dotnet/MyBlog/src/MyBlog.E2E/Dockerfile
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
```

```dockerfile https://github.com/dotnet/dotnet-docker/blob/main/src/sdk/10.0/noble/amd64/Dockerfile
ARG REPO=mcr.microsoft.com/dotnet/aspnet
# Installer image
FROM amd64/buildpack-deps:noble-curl AS installer

# Install .NET SDK
RUN dotnet_sdk_version=10.0.402 \
    && curl --fail --show-error --location \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/Sdk/$dotnet_sdk_version/dotnet-sdk-$dotnet_sdk_version-linux-x64.tar.gz \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/Sdk/$dotnet_sdk_version/dotnet-sdk-$dotnet_sdk_version-linux-x64.tar.gz.sha512 \
    && sha512sum -c dotnet-sdk-$dotnet_sdk_version-linux-x64.tar.gz.sha512 \
    && mkdir --parents /dotnet \
    && tar --gzip --extract --no-same-owner --file dotnet-sdk-$dotnet_sdk_version-linux-x64.tar.gz --directory /dotnet ./dnx ./packs ./sdk ./sdk-manifests ./templates ./LICENSE.txt ./ThirdPartyNotices.txt \
    && rm \
        dotnet-sdk-$dotnet_sdk_version-linux-x64.tar.gz \
        dotnet-sdk-$dotnet_sdk_version-linux-x64.tar.gz.sha512

# Download PowerShell
RUN powershell_version=7.6.8 \
    && curl --fail --show-error --location --output PowerShell.Linux.x64.$powershell_version.nupkg https://powershellinfraartifacts-gkhedzdeaghdezhr.z01.azurefd.net/tool/$powershell_version/PowerShell.Linux.x64.$powershell_version.nupkg \
    && powershell_sha256='caeeff1d808dd54a9fde06976e17c5c92061989dc5fbe21e13bd5690883e4eb1' \
    && echo "$powershell_sha256  PowerShell.Linux.x64.$powershell_version.nupkg" | sha256sum -c -


# .NET SDK image
FROM $REPO:10.0.13-noble-amd64

ENV \
    # Do not generate certificate
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    # Do not show first run text
    DOTNET_NOLOGO=true \
    # SDK version
    DOTNET_SDK_VERSION=10.0.402 \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip \
    # PowerShell telemetry for docker image usage
    POWERSHELL_DISTRIBUTION_CHANNEL=PSDocker-DotnetSDK-Ubuntu-24.04

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        git \
        libatomic1 \
        wget \
    && rm -rf /var/lib/apt/lists/*

COPY --from=installer ["/dotnet", "/usr/share/dotnet"]

COPY --from=installer ["/PowerShell.Linux.x64.7.6.8.nupkg", "/"]

RUN ln -s /usr/share/dotnet/dnx /usr/bin/dnx \
    # Trigger first run experience by running arbitrary cmd
    && dotnet help

# Install PowerShell global tool
RUN powershell_version=7.6.8 \
    && mkdir --parents /usr/share/powershell \
    && dotnet tool install --add-source / --tool-path /usr/share/powershell --version $powershell_version PowerShell.Linux.x64 \
    && dotnet nuget locals all --clear \
    && rm PowerShell.Linux.x64.$powershell_version.nupkg \
    && ln -s /usr/share/powershell/pwsh /usr/bin/pwsh \
    && chmod 755 /usr/share/powershell/pwsh \
    && chmod 755 /usr/share/powershell/.store/powershell.linux.x64/$powershell_version/powershell.linux.x64/$powershell_version/tools/*/any/pwsh \
    # To reduce image size, remove the copy nupkg that nuget keeps.
    && find /usr/share/powershell -print | grep -i '.*[.]nupkg$' | xargs rm
```
