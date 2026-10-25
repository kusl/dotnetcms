00
29

The biggest problems with dotnet 11 release candidate are

1. my build deploy currently releases on push to any branch and I don't want to change it 
2. I don't have dotnet 11 locally so any change won't work locally and I am unwilling to add any dotnet on this machine other than from official fedora sources.

So the combination means I don't think I will follow through with the dotnet 11 release candidate upgrade. 
I will upgrade to dotnet 11 when it is released. 


kushal@fedora:~/src/dotnet/MyBlog$ mylogpath="/home/kushal/src/dotnet/MyBlog/docs/llm/output/$(date +%Y-%m-%d-%H-%M-%S).txt";cd ~/src/dotnet/MyBlog/ && pwd > ${mylogpath} && export DOTNET_CLI_TELEMETRY_OPTOUT=1 && (cd src && time dotnet format >> ${mylogpath}; time dotnet restore >> ${mylogpath}; time dotnet clean >> ${mylogpath}; time dotnet build >> ${mylogpath}; time dotnet run --project MyBlog.Tests/MyBlog.Tests.csproj >> ${mylogpath}; time dotnet list package >> ${mylogpath}; time dotnet list package --outdated >> ${mylogpath}); cd ~/src/dotnet/MyBlog/ >> ${mylogpath}; cat export.sh >> ${mylogpath}; time bash export.sh >> ${mylogpath}; cat run-e2e.sh >> ${mylogpath}; time bash run-e2e.sh >> ${mylogpath};

real	0m10.266s
user	0m21.148s
sys	0m1.374s

real	0m0.815s
user	0m0.871s
sys	0m0.204s

real	0m0.735s
user	0m0.809s
sys	0m0.153s

real	0m7.769s
user	0m2.531s
sys	0m0.462s

real	0m44.356s
user	0m48.568s
sys	0m0.916s

real	0m1.592s
user	0m1.614s
sys	0m0.375s

real	0m3.132s
user	0m2.196s
sys	0m0.437s

real	0m1.520s
user	0m0.796s
sys	0m1.161s

real	0m24.494s
user	0m4.148s
sys	0m3.699s
kushal@fedora:~/src/dotnet/MyBlog$ 
