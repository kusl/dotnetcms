# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY Directory.Build.props .
COPY Directory.Packages.props .
COPY MyBlog.Core/MyBlog.Core.csproj MyBlog.Core/
COPY MyBlog.Infrastructure/MyBlog.Infrastructure.csproj MyBlog.Infrastructure/
COPY MyBlog.Web/MyBlog.Web.csproj MyBlog.Web/

# Restore dependencies
RUN dotnet restore MyBlog.Web/MyBlog.Web.csproj

# Copy source code
COPY MyBlog.Core/ MyBlog.Core/
COPY MyBlog.Infrastructure/ MyBlog.Infrastructure/
COPY MyBlog.Web/ MyBlog.Web/

# Build and publish
RUN dotnet publish MyBlog.Web/MyBlog.Web.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN useradd -m -s /bin/bash appuser

# Create data directory for SQLite
RUN mkdir -p /app/data && chown appuser:appuser /app/data

COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/myblog.db"

# Switch to non-root user
USER appuser

EXPOSE 5000

ENTRYPOINT ["dotnet", "MyBlog.Web.dll"]
