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
