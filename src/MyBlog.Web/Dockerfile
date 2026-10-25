FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY MyBlog.Core/MyBlog.Core.csproj MyBlog.Core/
COPY MyBlog.Infrastructure/MyBlog.Infrastructure.csproj MyBlog.Infrastructure/
COPY MyBlog.Web/MyBlog.Web.csproj MyBlog.Web/

RUN dotnet restore MyBlog.Web/MyBlog.Web.csproj

COPY MyBlog.Core/ MyBlog.Core/
COPY MyBlog.Infrastructure/ MyBlog.Infrastructure/
COPY MyBlog.Web/ MyBlog.Web/

RUN dotnet publish MyBlog.Web/MyBlog.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

RUN useradd -m -s /bin/bash appuser && mkdir -p /app/data && chown appuser:appuser /app/data

COPY --from=build --chown=appuser:appuser /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/myblog.db"

USER appuser

EXPOSE 5000

ENTRYPOINT ["dotnet", "MyBlog.Web.dll"]
