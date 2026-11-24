00
14

so now `dotnet list package --outdated` is practically useless for me 
my workflow was see what the latest version was 
and upgrade to that 
and see what broke 
but when I see something like this 
```bash
Project 'MyBlog.Core' has the following package references
   [net10.0]: 
   Top-level Package                                Requested   Resolved
   > Microsoft.Extensions.Logging.Abstractions      10.0.14     10.0.14 

Project 'MyBlog.E2E' has the following package references
   [net10.0]: 
   Top-level Package             Requested   Resolved
   > Microsoft.NET.Test.Sdk      18.11.0     18.11.0 
   > Microsoft.Playwright        1.64.1      1.64.1  
   > xunit.v3                    4.0.2       4.0.2   

Project 'MyBlog.Infrastructure' has the following package references
   [net10.0]: 
   Top-level Package                           Requested   Resolved
   > Microsoft.EntityFrameworkCore.Sqlite      10.0.14     10.0.14 
   > OpenTelemetry                             1.20.1      1.20.1  

Project 'MyBlog.Tests' has the following package references
   [net10.0]: 
   Top-level Package             Requested   Resolved
   > Microsoft.NET.Test.Sdk      18.11.0     18.11.0 
   > xunit.v3                    4.0.2       4.0.2   

Project 'MyBlog.Web' has the following package references
   [net10.0]: 
   Top-level Package                                      Requested     Resolved
   > Microsoft.AspNetCore.App.Internal.Assets       (A)   [10.0.13, )   10.0.13 
   > Microsoft.AspNetCore.SignalR.Client                  10.0.14       10.0.14 
   > OpenTelemetry.Exporter.Console                       1.20.1        1.20.1  
   > OpenTelemetry.Exporter.OpenTelemetryProtocol         1.20.1        1.20.1  
   > OpenTelemetry.Extensions.Hosting                     1.20.1        1.20.1  
   > OpenTelemetry.Instrumentation.AspNetCore             1.20.2        1.20.2  
   > OpenTelemetry.Instrumentation.Http                   1.20.2        1.20.2  

(A) : Auto-referenced package.
  Determining projects to restore...
  All projects are up-to-date for restore.

The following sources were used:
   https://api.nuget.org/v3/index.json

Project `MyBlog.Core` has the following updates to its packages
   [net10.0]: 
   Top-level Package                                Requested   Resolved   Latest
   > Microsoft.Extensions.Logging.Abstractions      10.0.14     10.0.14    11.0.0

Project `MyBlog.E2E` has the following updates to its packages
   [net10.0]: 
   Top-level Package             Requested   Resolved   Latest 
   > Microsoft.NET.Test.Sdk      18.11.0     18.11.0    18.12.0
   > Microsoft.Playwright        1.64.1      1.64.1     1.65.1 
   > xunit.v3                    4.0.2       4.0.2      4.0.3  

Project `MyBlog.Infrastructure` has the following updates to its packages
   [net10.0]: 
   Top-level Package                           Requested   Resolved   Latest
   > Microsoft.EntityFrameworkCore.Sqlite      10.0.14     10.0.14    11.0.0
   > OpenTelemetry                             1.20.1      1.20.1     1.21.0

Project `MyBlog.Tests` has the following updates to its packages
   [net10.0]: 
   Top-level Package             Requested   Resolved   Latest 
   > Microsoft.NET.Test.Sdk      18.11.0     18.11.0    18.12.0
   > xunit.v3                    4.0.2       4.0.2      4.0.3  

Project `MyBlog.Web` has the following updates to its packages
   [net10.0]: 
   Top-level Package                                   Requested   Resolved   Latest
   > Microsoft.AspNetCore.SignalR.Client               10.0.14     10.0.14    11.0.0
   > OpenTelemetry.Exporter.Console                    1.20.1      1.20.1     1.21.0
   > OpenTelemetry.Exporter.OpenTelemetryProtocol      1.20.1      1.20.1     1.21.0
   > OpenTelemetry.Extensions.Hosting                  1.20.1      1.20.1     1.21.0
   > OpenTelemetry.Instrumentation.AspNetCore          1.20.2      1.20.2     1.21.0
   > OpenTelemetry.Instrumentation.Http                1.20.2      1.20.2     1.21.0
```
because my project is on net10 and not net 11 (I have yet to verify whether monster asp dot net supports net 11)
I can no longer blindly upgrade dependencies anymore 
What solutions do I have here? 
for now I manually updated otel and left signal r client alone 
actually I updated everything that didn't say 11 
lets see... 
ok looks like dotnet 11 is supported 
About ASP.NET / .NET settings
Configure the ASP.NET runtime version and execution mode for your .NET application. Every change triggers an application pool recycle and applies immediately.
    Pick the runtime that matches your project — for classic ASP.NET .NET Framework 4.x, for modern ASP.NET Core .NET 11 / 10 / 9 / 8.
    Integrated mode is the recommended choice for all .NET applications. Classic mode exists only for legacy compatibility with old IIS 6 ASP.NET code.
    The default 32-bit (x86) is recommended for compatibility and a lower memory footprint — suitable for the majority of .NET Framework applications.
    Switch to 64-bit (x64) only if your application requires it (large in-memory datasets over 2 GB or native 64-bit libraries).
Now that dotnet 11 is out, 
lets reimagine this whole project for dotnet 11 
if you were writing this project from scratch today 
what would you do differently? 
do that 
for example, 
we do need to worry about backward compatibility when it comes to our output 
the existing file structure must be preserved 
no existing emails should be deleted ever 
do not break user's data
other than that we have free reign to change things 
even force a clean out of the search caches and rebuild if required 
but only if required, not just for fun 
it must have a valid justificatiion if we do it
and make our code cleaner and leaner 
there may be new things we could do in dotnet 11 and csharp 15
do them the new way when and where possible 
there might also be code that we won't need with dotnet 11
also there might be nuget packages and github actions 
and other dependencies that we can upgrade to latest stable as well
upgrade them all 
as we do all of this 
please be mindful to use best engineering practices 
and solid principles 
Use your best judgment
If only a few files (under five or there about) need to change, give me the full path of the files that need to change and the FULL file 
if there are a lot of files that need to change 
Delivery
    Whole project as one tar.gz, unambiguous file placement
    do not include any file that has not changed 
    Any files to delete on updates called out explicitly
    No bespoke sed/patch scripts — full files only for anything that changes
    Docs updated alongside code changes
    Low verbosity, no code comments
    I only have the rc3 locally so the CI will need to do everything
    unless you can structure the global.json to work with rc3 or better
    this will improve soon when dotnet 11 stable lands on fedora default repos 
one another fly in the ointment 
my laptop only has rc3 
so this new code will need to work with rc3 locally 
but use the latest stable build on github actions 
```bash
kushal@fedora:~/src/dotnet/MyImapDownloader$ dotnet --info
.NET SDK:
 Version:           11.0.100-rc.3.26522.148
 Commit:            5254d8bcf5
 Workload version:  11.0.100-manifests.2c46f3cc
 MSBuild version:   18.12.0-1.26522.148+5254d8bcf

Runtime Environment:
 OS Name:     fedora
 OS Version:  45
 OS Platform: Linux
 RID:         fedora.45-x64
 Base Path:   /usr/lib64/dotnet/sdk/11.0.100-rc.3.26522.148

.NET workloads installed:
There are no installed workloads to display.
Configured to use workload sets when installing new manifests.
No workload sets are installed. Run "dotnet workload restore" to install a workload set.

Host:
  Version:      11.0.0-rc.3.26522.148
  Architecture: x64
  Commit:       5254d8bcf5

.NET SDKs installed:
  10.0.113 [/usr/lib64/dotnet/sdk]
  11.0.100-rc.3.26522.148 [/usr/lib64/dotnet/sdk]

.NET runtimes installed:
  Microsoft.AspNetCore.App 10.0.13 [/usr/lib64/dotnet/shared/Microsoft.AspNetCore.App]
  Microsoft.AspNetCore.App 11.0.0-rc.3.26522.148 [/usr/lib64/dotnet/shared/Microsoft.AspNetCore.App]
  Microsoft.NETCore.App 10.0.13 [/usr/lib64/dotnet/shared/Microsoft.NETCore.App]
  Microsoft.NETCore.App 11.0.0-rc.3.26522.148 [/usr/lib64/dotnet/shared/Microsoft.NETCore.App]

Other architectures found:
  None

Environment variables:
  Not set

global.json file:
  /home/kushal/src/dotnet/MyImapDownloader/global.json

Learn more:
  https://aka.ms/dotnet/info

Download .NET:
  https://aka.ms/dotnet/download
kushal@fedora:~/src/dotnet/MyImapDownloader$ 

```
also looks like github dot com is having trouble with macos 
please make macos optional so we can limp along even if it fails or times out somehow
 build-test (macos-latest)
Started 8m 45s ago
Job is about to start running on the hosted runner: GitHub Actions 1000079313
Job is waiting for a hosted runner to come online.
Evaluating build-test.if
Evaluating: success()
Result: true
Requested labels: macos-latest
Job defined at: kusl/dotnetcms/.github/workflows/build-deploy.yml@refs/heads/main
Waiting for a runner to pick up this job...
Requested labels: macos-latest
Job defined at: kusl/dotnetcms/.github/workflows/build-deploy.yml@refs/heads/main
Waiting for a runner to pick up this job...
