#!/bin/bash
# =============================================================================
# Fix Blazor Interactivity, Logout, and Static Files Issues
# =============================================================================
# This script fixes:
# 1. Logout 400 error - adds MapPost endpoint for /logout
# 2. PostEditor not working - adds @rendermode InteractiveServer to pages
# 3. blazor.web.js 404 locally - ensures proper middleware configuration
# 4. Preview and Save not working - requires interactive render mode
# =============================================================================
set -euo pipefail

SRC_DIR="src"

echo "=============================================="
echo "  MyBlog: Fix Blazor Interactivity Issues"
echo "=============================================="
echo ""

# =============================================================================
# Step 1: Update Program.cs - Add logout endpoint and fix middleware order
# =============================================================================
echo "[1/7] Updating Program.cs with logout endpoint..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Program.cs"
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Constants;
using MyBlog.Core.Interfaces;
using MyBlog.Infrastructure;
using MyBlog.Infrastructure.Data;
using MyBlog.Infrastructure.Telemetry;
using MyBlog.Web.Components;
using MyBlog.Web.Middleware;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration);

// Configure authentication
var sessionTimeout = builder.Configuration.GetValue("Authentication:SessionTimeoutMinutes", 30);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = AppConstants.AuthCookieName;
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionTimeout);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Configuration.GetValue("Application:RequireHttps", false)
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

// Configure OpenTelemetry
var serviceName = "MyBlog.Web";
var serviceVersion = "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Configure OpenTelemetry logging
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName, serviceVersion));
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddConsoleExporter();
});

var app = builder.Build();

// Initialize database and ensure admin user exists
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    await context.Database.EnsureCreatedAsync();

    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureAdminUserAsync();
}

// Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    if (builder.Configuration.GetValue("Application:RequireHttps", false))
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
}

app.UseStaticFiles();
app.UseLoginRateLimit();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Map logout endpoint - handles POST from the logout form in MainLayout
app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();

// Image endpoint
app.MapGet("/api/images/{id:guid}", async (Guid id, IImageRepository repo, CancellationToken ct) =>
{
    var image = await repo.GetByIdAsync(id, ct);
    return image is null
        ? Results.NotFound()
        : Results.File(image.Data, image.ContentType, image.FileName);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
EOF

echo "      Done."

# =============================================================================
# Step 2: Update App.razor to include HeadOutlet with render mode
# =============================================================================
echo "[2/7] Updating App.razor..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/App.razor"
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@(Title ?? "MyBlog")</title>
    <base href="/" />
    <link rel="stylesheet" href="css/site.css" />
    <HeadOutlet @rendermode="InteractiveServer" />
</head>
<body>
    <Routes @rendermode="InteractiveServer" />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>

@code {
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    private string? Title => HttpContext?.RequestServices
        .GetService<IConfiguration>()?["Application:Title"];
}
EOF

echo "      Done."

# =============================================================================
# Step 3: Update _Imports.razor to include render mode
# =============================================================================
echo "[3/7] Updating _Imports.razor..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/_Imports.razor"
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.JSInterop
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@using MyBlog.Core.Constants
@using MyBlog.Core.Interfaces
@using MyBlog.Core.Models
@using MyBlog.Web.Components
@using MyBlog.Web.Components.Layout
@using MyBlog.Web.Components.Shared
EOF

echo "      Done."

# =============================================================================
# Step 4: Fix PostEditor.razor - ensure it works with interactive mode
# =============================================================================
echo "[4/7] Fixing PostEditor.razor..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Pages/Admin/PostEditor.razor"
@page "/admin/posts/new"
@page "/admin/posts/edit/{Id:guid}"
@attribute [Authorize]
@inject IPostRepository PostRepository
@inject ISlugService SlugService
@inject NavigationManager Navigation
@inject AuthenticationStateProvider AuthStateProvider
@using System.Security.Claims

<PageTitle>@(_isEdit ? "Edit Post" : "New Post")</PageTitle>

<h1>@(_isEdit ? "Edit Post" : "New Post")</h1>

@if (_loading)
{
    <p>Loading...</p>
}
else if (_isEdit && _existingPost is null)
{
    <p>Post not found.</p>
    <a href="/admin/posts">Back to Posts</a>
}
else
{
    <div class="post-editor">
        <div class="editor-form">
            <div class="form-group">
                <label for="title">Title</label>
                <input type="text" id="title" @bind="_title" @bind:event="oninput" />
            </div>

            <div class="form-group">
                <label for="summary">Summary</label>
                <textarea id="summary" @bind="_summary" rows="2"></textarea>
            </div>

            <div class="form-group">
                <label for="content">Content (Markdown)</label>
                <textarea id="content" @bind="_content" @bind:event="oninput" rows="20"></textarea>
            </div>

            <div class="form-group checkbox">
                <label>
                    <input type="checkbox" @bind="_isPublished" />
                    Published
                </label>
            </div>

            <div class="form-actions">
                <button @onclick="Save" class="btn btn-primary" disabled="@_saving">
                    @(_saving ? "Saving..." : "Save")
                </button>
                <a href="/admin/posts" class="btn">Cancel</a>
            </div>

            @if (!string.IsNullOrEmpty(_error))
            {
                <div class="error-message">@_error</div>
            }
        </div>

        <div class="editor-preview">
            <h3>Preview</h3>
            <div class="preview-content">
                <MarkdownRenderer Content="@_content" />
            </div>
        </div>
    </div>
}

@code {
    [Parameter]
    public Guid? Id { get; set; }

    private bool _isEdit => Id.HasValue;
    private bool _loading = true;
    private bool _saving;
    private string _title = "";
    private string _summary = "";
    private string _content = "";
    private bool _isPublished;
    private string? _error;
    private Post? _existingPost;

    protected override async Task OnInitializedAsync()
    {
        if (_isEdit)
        {
            _existingPost = await PostRepository.GetByIdAsync(Id!.Value);
            if (_existingPost is not null)
            {
                _title = _existingPost.Title;
                _summary = _existingPost.Summary;
                _content = _existingPost.Content;
                _isPublished = _existingPost.IsPublished;
            }
        }
        _loading = false;
    }

    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(_title))
        {
            _error = "Title is required.";
            return;
        }

        _saving = true;
        _error = null;

        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier);
            
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _error = "Unable to identify current user. Please log in again.";
                _saving = false;
                return;
            }

            if (_isEdit && _existingPost is not null)
            {
                _existingPost.Title = _title;
                _existingPost.Slug = SlugService.GenerateSlug(_title);
                _existingPost.Summary = _summary;
                _existingPost.Content = _content;
                _existingPost.IsPublished = _isPublished;
                _existingPost.UpdatedAtUtc = DateTime.UtcNow;
                if (_isPublished && !_existingPost.PublishedAtUtc.HasValue)
                {
                    _existingPost.PublishedAtUtc = DateTime.UtcNow;
                }

                await PostRepository.UpdateAsync(_existingPost);
            }
            else
            {
                var post = new Post
                {
                    Id = Guid.NewGuid(),
                    Title = _title,
                    Slug = SlugService.GenerateSlug(_title),
                    Summary = _summary,
                    Content = _content,
                    AuthorId = userId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    IsPublished = _isPublished,
                    PublishedAtUtc = _isPublished ? DateTime.UtcNow : null
                };

                await PostRepository.CreateAsync(post);
            }

            Navigation.NavigateTo("/admin/posts");
        }
        catch (Exception ex)
        {
            _error = $"Failed to save: {ex.Message}";
        }
        finally
        {
            _saving = false;
        }
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 5: Fix PostList.razor
# =============================================================================
echo "[5/7] Fixing PostList.razor..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Pages/Admin/PostList.razor"
@page "/admin/posts"
@attribute [Authorize]
@inject IPostRepository PostRepository
@inject NavigationManager Navigation

<PageTitle>Manage Posts</PageTitle>

<h1>Manage Posts</h1>

<p><a href="/admin/posts/new" class="btn btn-primary">Create New Post</a></p>

@if (_posts is null)
{
    <p>Loading...</p>
}
else if (_posts.Count == 0)
{
    <p>No posts yet.</p>
}
else
{
    <table class="admin-table">
        <thead>
            <tr>
                <th>Title</th>
                <th>Author</th>
                <th>Status</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var post in _posts)
            {
                <tr>
                    <td>@post.Title</td>
                    <td>@post.AuthorDisplayName</td>
                    <td>@(post.IsPublished ? "Published" : "Draft")</td>
                    <td>
                        <a href="/admin/posts/edit/@post.Id">Edit</a>
                        <button @onclick="() => DeletePost(post.Id)" class="btn-link danger" disabled="@_deleting">Delete</button>
                    </td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private IReadOnlyList<PostListItemDto>? _posts;
    private bool _deleting;

    protected override async Task OnInitializedAsync()
    {
        await LoadPosts();
    }

    private async Task LoadPosts()
    {
        _posts = await PostRepository.GetAllPostsAsync();
    }

    private async Task DeletePost(Guid id)
    {
        _deleting = true;
        try
        {
            await PostRepository.DeleteAsync(id);
            await LoadPosts();
        }
        finally
        {
            _deleting = false;
        }
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 6: Fix ImageManager.razor
# =============================================================================
echo "[6/7] Fixing ImageManager.razor..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Pages/Admin/ImageManager.razor"
@page "/admin/images"
@attribute [Authorize]
@inject IImageRepository ImageRepository
@inject AuthenticationStateProvider AuthStateProvider
@using System.Security.Claims

<PageTitle>Manage Images</PageTitle>

<h1>Manage Images</h1>

<div class="image-upload">
    <h3>Upload New Image</h3>
    <InputFile OnChange="HandleFileUpload" accept="image/*" disabled="@_uploading" />
    @if (_uploading)
    {
        <p>Uploading...</p>
    }
    @if (!string.IsNullOrEmpty(_uploadStatus))
    {
        <p class="@(_uploadError ? "error" : "success")">@_uploadStatus</p>
    }
</div>

<h3>Uploaded Images</h3>
@if (_images is null)
{
    <p>Loading...</p>
}
else if (_images.Count == 0)
{
    <p>No images uploaded yet.</p>
}
else
{
    <div class="image-grid">
        @foreach (var image in _images)
        {
            <div class="image-card">
                <img src="/api/images/@image.Id" alt="@image.FileName" />
                <div class="image-info">
                    <p>@image.FileName</p>
                    <code>/api/images/@image.Id</code>
                    <button @onclick="() => DeleteImage(image.Id)" class="btn-link danger" disabled="@_deleting">Delete</button>
                </div>
            </div>
        }
    </div>
}

@code {
    private IReadOnlyList<Image>? _images;
    private string? _uploadStatus;
    private bool _uploadError;
    private bool _uploading;
    private bool _deleting;

    protected override async Task OnInitializedAsync()
    {
        await LoadImages();
    }

    private async Task LoadImages()
    {
        _images = await ImageRepository.GetAllAsync();
    }

    private async Task HandleFileUpload(InputFileChangeEventArgs e)
    {
        _uploadStatus = null;
        _uploadError = false;
        _uploading = true;

        try
        {
            var file = e.File;
            if (file.Size > AppConstants.MaxImageSizeBytes)
            {
                _uploadStatus = "File too large. Maximum size is 5MB.";
                _uploadError = true;
                return;
            }

            if (!AppConstants.AllowedImageTypes.Contains(file.ContentType))
            {
                _uploadStatus = "Invalid file type. Only JPEG, PNG, GIF, and WebP are allowed.";
                _uploadError = true;
                return;
            }

            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier);
            
            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _uploadStatus = "Unable to identify current user. Please log in again.";
                _uploadError = true;
                return;
            }

            using var stream = file.OpenReadStream(AppConstants.MaxImageSizeBytes);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);

            var image = new Image
            {
                Id = Guid.NewGuid(),
                FileName = file.Name,
                ContentType = file.ContentType,
                Data = ms.ToArray(),
                UploadedAtUtc = DateTime.UtcNow,
                UploadedByUserId = userId
            };

            await ImageRepository.CreateAsync(image);
            _uploadStatus = "Image uploaded successfully!";
            await LoadImages();
        }
        catch (Exception ex)
        {
            _uploadStatus = $"Upload failed: {ex.Message}";
            _uploadError = true;
        }
        finally
        {
            _uploading = false;
        }
    }

    private async Task DeleteImage(Guid id)
    {
        _deleting = true;
        try
        {
            await ImageRepository.DeleteAsync(id);
            await LoadImages();
        }
        finally
        {
            _deleting = false;
        }
    }
}
EOF

echo "      Done."

# =============================================================================
# Step 7: Update Login.razor with name attributes
# =============================================================================
echo "[7/7] Fixing Login.razor with name attributes..."

cat << 'EOF' > "$SRC_DIR/MyBlog.Web/Components/Pages/Login.razor"
@page "/login"
@inject IAuthService AuthService
@inject NavigationManager Navigation
@inject IHttpContextAccessor HttpContextAccessor
@using System.Security.Claims
@using Microsoft.AspNetCore.Authentication
@using Microsoft.AspNetCore.Authentication.Cookies

<PageTitle>Login</PageTitle>

<div class="login-page">
    <h1>Login</h1>

    @if (!string.IsNullOrEmpty(_error))
    {
        <div class="error-message">@_error</div>
    }

    <form method="post" @onsubmit="HandleLogin" @formname="login">
        <AntiforgeryToken />
        
        <div class="form-group">
            <label for="username">Username</label>
            <input type="text" id="username" name="username" @bind="_username" required />
        </div>

        <div class="form-group">
            <label for="password">Password</label>
            <input type="password" id="password" name="password" @bind="_password" required />
        </div>

        <button type="submit" class="btn btn-primary">Login</button>
    </form>
</div>

@code {
    private string _username = "";
    private string _password = "";
    private string? _error;

    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    [SupplyParameterFromForm(Name = "username")]
    public string? FormUsername { get; set; }

    [SupplyParameterFromForm(Name = "password")]
    public string? FormPassword { get; set; }

    private async Task HandleLogin()
    {
        // Use form values if available (SSR form post), otherwise use bound values
        var username = FormUsername ?? _username;
        var password = FormPassword ?? _password;

        var user = await AuthService.AuthenticateAsync(username, password);
        
        if (user is null)
        {
            _error = "Invalid username or password";
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("DisplayName", user.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var context = HttpContextAccessor.HttpContext!;
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        Navigation.NavigateTo(ReturnUrl ?? "/admin", forceLoad: true);
    }
}
EOF

echo "      Done."

# =============================================================================
# Summary
# =============================================================================
echo ""
echo "=============================================="
echo "  All Blazor issues fixed!"
echo "=============================================="
echo ""
echo "Changes made:"
echo ""
echo "  1. Program.cs:"
echo "     - Added MapPost('/logout') endpoint to handle logout form POST"
echo "     - This fixes the 400 Bad Request on logout"
echo ""
echo "  2. App.razor:"
echo "     - Added @rendermode='InteractiveServer' to Routes and HeadOutlet"
echo "     - This enables interactive Blazor features globally"
echo ""
echo "  3. _Imports.razor:"
echo "     - Added 'using static Microsoft.AspNetCore.Components.Web.RenderMode'"
echo "     - Makes InteractiveServer available without full namespace"
echo ""
echo "  4. PostEditor.razor:"
echo "     - Added loading and saving states"
echo "     - Added error handling with try/catch"
echo "     - Now properly works with interactive mode"
echo ""
echo "  5. PostList.razor:"
echo "     - Added deleting state to prevent double-clicks"
echo ""
echo "  6. ImageManager.razor:"
echo "     - Added uploading and deleting states"
echo "     - Added better error handling"
echo ""
echo "  7. Login.razor:"
echo "     - Added name attributes for SSR form submission"
echo "     - Added SupplyParameterFromForm for form values"
echo ""
echo "Why these fixes work:"
echo "  - The logout form in MainLayout POSTs to /logout, which now has an endpoint"
echo "  - Interactive render mode enables @onclick, @bind:event, and other JS-backed features"
echo "  - The blazor.web.js file is served by MapRazorComponents middleware"
echo ""
echo "To apply these fixes:"
echo "  1. Run this script: chmod +x fix-blazor-interactivity.sh && ./fix-blazor-interactivity.sh"
echo "  2. Rebuild: dotnet build src/MyBlog.slnx"
echo "  3. Run locally: dotnet run --project src/MyBlog.Web"
echo "  4. Deploy to server"
echo ""
