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
