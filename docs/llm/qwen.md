The Blazor form with data-enhance="false" doesn't behave the same way as a standard HTML form POST.
is this the right thing to do in the context of blazor and dotnet 10? 
all the source code and context is available in dump.txt
Please review each line and each word and do not hallucinate. 

```html /home/kushal/src/dotnet/MyBlog/src/MyBlog.Web/Components/Pages/Login.razor
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

    <form method="post" @onsubmit="HandleLogin" @formname="login" data-enhance="false">
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
        var username = FormUsername ?? _username;
        var password = FormPassword ?? _password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _error = "Username and password are required";
            return;
        }

        var user = await AuthService.AuthenticateAsync(username, password);
        if (user is null)
        {
            _error = "Invalid username or password";
            return;
        }

        // FIX: Added the Role claim here so [Authorize(Roles="Admin")] works
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("DisplayName", user.DisplayName),
            new(ClaimTypes.Role, AppConstants.AdminRole) 
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var context = HttpContextAccessor.HttpContext!;
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        
        Navigation.NavigateTo(ReturnUrl ?? "/admin", forceLoad: true);
    }
}
```








