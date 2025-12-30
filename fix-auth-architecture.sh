#!/bin/bash
set -euo pipefail

echo "========================================================"
echo "  Fixing Blazor Authentication Architecture"
echo "  Switching from Global Interactivity to Per-Page Mode"
echo "========================================================"

# 1. Update App.razor: REMOVE global interactivity
# This forces pages to default to Static SSR (required for Login/Cookie setting)
echo "Updating App.razor..."
cat << 'EOF' > src/MyBlog.Web/Components/App.razor
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@(Title ?? "MyBlog")</title>
    <base href="/" />
    <link rel="stylesheet" href="css/site.css" />
    <HeadOutlet />
</head>
<body>
    <Routes />
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

# 2. Update PostEditor.razor: ADD interactivity
# This page uses complex event binding (@oninput) so it needs InteractiveServer
echo "Updating PostEditor.razor..."
sed -i '2i @rendermode InteractiveServer' src/MyBlog.Web/Components/Pages/Admin/PostEditor.razor

# 3. Update ImageManager.razor: ADD interactivity
# This page uses InputFile which requires interactivity
echo "Updating ImageManager.razor..."
sed -i '2i @rendermode InteractiveServer' src/MyBlog.Web/Components/Pages/Admin/ImageManager.razor

# 4. Update PostList.razor: ADD interactivity
# This page has a Delete button with @onclick, requiring interactivity
echo "Updating PostList.razor..."
sed -i '2i @rendermode InteractiveServer' src/MyBlog.Web/Components/Pages/Admin/PostList.razor

# 5. Update _Imports.razor
# Ensure InteractiveServer is available everywhere comfortably
echo "Updating _Imports.razor..."
cat << 'EOF' > src/MyBlog.Web/Components/_Imports.razor
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

echo "========================================================"
echo "  Fix Complete."
echo "========================================================"
echo "  1. Rebuild your project."
echo "  2. Deploy."
echo "  3. IMPORTANT: Clear your browser cookies for kush.runasp.net"
echo "     before trying to log in again."
echo "========================================================"
