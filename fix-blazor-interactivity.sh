#!/bin/bash
set -euo pipefail

echo "=============================================="
echo "  Fixing MyBlog Architecture"
echo "  Converting Public pages to Static SSR"
echo "  Keeping Admin pages Interactive"
echo "=============================================="

# 1. Disable Global Interactivity in App.razor
# We remove @rendermode from HeadOutlet and Routes
echo "Updating App.razor (Making default mode Static)..."
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

# 2. Update Routes.razor to remove global interactivity
echo "Updating Routes.razor..."
cat << 'EOF' > src/MyBlog.Web/Components/Routes.razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
            <NotAuthorized>
                <RedirectToLogin />
            </NotAuthorized>
        </AuthorizeRouteView>
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
</Router>
EOF

# 3. Add Interactivity ONLY to Admin Pages
# We loop through specific admin files and add the render mode to the top
ADMIN_FILES=(
    "src/MyBlog.Web/Components/Pages/Admin/PostEditor.razor"
    "src/MyBlog.Web/Components/Pages/Admin/ImageManager.razor"
    "src/MyBlog.Web/Components/Pages/Admin/PostList.razor"
    "src/MyBlog.Web/Components/Pages/Admin/Dashboard.razor"
    "src/MyBlog.Web/Components/Pages/Admin/ChangePassword.razor"
    "src/MyBlog.Web/Components/Pages/Admin/UserList.razor"
    "src/MyBlog.Web/Components/Pages/Admin/UserEditor.razor"
)

echo "Adding InteractiveServer mode to Admin pages..."
for file in "${ADMIN_FILES[@]}"; do
    if [ -f "$file" ]; then
        # Check if already interactive
        if ! grep -q "@rendermode" "$file"; then
            # Insert @rendermode after the last @page or @attribute directive
            # Using a temporary file approach for safety
            temp_file=$(mktemp)
            awk '
                /^@page/ || /^@attribute/ || /^@using/ || /^@inject/ { print; last_directive=NR; next }
                NR==last_directive+1 { print "@rendermode InteractiveServer" }
                { print }
            ' "$file" > "$temp_file"

            # Simple fallback if awk logic missed (just prepend to 2nd line if not found)
            if ! grep -q "@rendermode" "$temp_file"; then
                 sed -i '2i @rendermode InteractiveServer' "$file"
            else
                 mv "$temp_file" "$file"
            fi
            echo "  - Updated $file"
        else
            echo "  - $file already interactive"
        fi
    fi
done

# 4. Explicitly Fix Login.razor
# Ensure it DOES NOT have @rendermode (forces Static SSR)
# And ensure it has the correct form handling
echo "Verifying Login.razor is Static..."
if grep -q "@rendermode" "src/MyBlog.Web/Components/Pages/Login.razor"; then
    sed -i '/@rendermode/d' "src/MyBlog.Web/Components/Pages/Login.razor"
    echo "  - Removed interactive mode from Login.razor (Fixes 400 error)"
fi

echo ""
echo "=============================================="
echo "  Fix Complete!"
echo "=============================================="
echo "1. Stop the app."
echo "2. Rebuild: dotnet build src/MyBlog.slnx"
echo "3. Run: dotnet run --project src/MyBlog.Web"
echo "4. Clear your browser cookies for this site before trying again."
