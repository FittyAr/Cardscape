# Radzen Blazor Setup Patterns

## Prerequisites

- .NET 8.0 or later
- Visual Studio 2022, VS Code, or Rider
- Blazor Server or Blazor WebAssembly project

---

## Installation

### NuGet Package
```bash
dotnet add package Radzen.Blazor
```

**Current Stable Version:** 7.1.0

---

## Blazor Server Setup (.NET 8+)

### 1. Program.cs Configuration
```csharp
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddRadzenComponents();  // ← Add this

var app = builder.Build();

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

### 2. _Imports.razor
Add global using statements:
```razor
@using Radzen
@using Radzen.Blazor
```

### 3. _Layout.cshtml or App.razor
Add CSS and JavaScript references in the `<head>` and before closing `</body>`:

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>My App</title>
    <base href="~/" />

    <!-- Radzen CSS (choose one theme) -->
    <link href="_content/Radzen.Blazor/css/material-base.css" rel="stylesheet" />
    <link href="_content/Radzen.Blazor/css/material.css" rel="stylesheet" />

    <!-- Your custom CSS -->
    <link href="css/app.css" rel="stylesheet" />
</head>
<body>
    @RenderBody()

    <script src="_framework/blazor.server.js"></script>

    <!-- Radzen JavaScript -->
    <script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
</body>
</html>
```

### 4. MainLayout.razor
Add `<RadzenComponents />` at the end:

```razor
@inherits LayoutComponentBase

<RadzenLayout>
    <RadzenHeader>
        <h3>My App</h3>
    </RadzenHeader>
    <RadzenBody>
        @Body
    </RadzenBody>
</RadzenLayout>

<RadzenComponents />  @*← REQUIRED for dialogs/notifications*@
```

---

## Blazor WebAssembly Setup (.NET 8+)

### 1. Program.cs Configuration
```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using MyApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddRadzenComponents();  // ← Add this

await builder.Build().RunAsync();
```

### 2. wwwroot/index.html
```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>My App</title>
    <base href="/" />

    <!-- Radzen CSS -->
    <link href="_content/Radzen.Blazor/css/material-base.css" rel="stylesheet" />
    <link href="_content/Radzen.Blazor/css/material.css" rel="stylesheet" />

    <!-- App CSS -->
    <link href="css/app.css" rel="stylesheet" />
</head>
<body>
    <div id="app">Loading...</div>

    <script src="_framework/blazor.webassembly.js"></script>

    <!-- Radzen JavaScript -->
    <script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
</body>
</html>
```

### 3. _Imports.razor
```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.JSInterop
@using MyApp
@using MyApp.Shared
@using Radzen
@using Radzen.Blazor
```

### 4. MainLayout.razor
```razor
@inherits LayoutComponentBase

<RadzenLayout>
    <RadzenHeader>
        <h3>My App</h3>
    </RadzenHeader>
    <RadzenBody>
        @Body
    </RadzenBody>
</RadzenLayout>

<RadzenComponents />
```

---

## .NET 8+ Render Modes

### Interactive Server
Components with events require interactive render mode:

```razor
@* Page-level *@
@page "/counter"
@rendermode InteractiveServer

<RadzenButton Click="@IncrementCount" Text="Click me" />

@code {
    private int currentCount = 0;
    void IncrementCount() => currentCount++;
}
```

### Interactive Auto
Automatically uses WebAssembly when available, falls back to Server:

```razor
@page "/products"
@rendermode InteractiveAuto

<RadzenDataGrid Data="@products" TItem="Product">
    @* Grid content *@
</RadzenDataGrid>
```

### Component-Level Render Mode
```razor
@* In a static page *@
@page "/static-page"

<h1>This is static</h1>

<RadzenButton @rendermode="InteractiveServer" Click="@HandleClick" Text="Interactive" />

@code {
    void HandleClick() { }
}
```

---

## Theme Configuration

### Available Themes

**Material Design:**
- Light: `material-base.css` + `material.css`
- Dark: `material-base.css` + `material-dark.css`

**Default:**
- Light: `default-base.css` + `default.css`
- Dark: `default-base.css` + `default-dark.css`

**Humanistic:**
- Light: `humanistic-base.css` + `humanistic.css`
- Dark: `humanistic-base.css` + `humanistic-dark.css`

**Software:**
- Light: `software-base.css` + `software.css`
- Dark: `software-base.css` + `software-dark.css`

### Switching Themes
Change the CSS references in your layout file:

```html
<!-- Material Light -->
<link href="_content/Radzen.Blazor/css/material-base.css" rel="stylesheet" />
<link href="_content/Radzen.Blazor/css/material.css" rel="stylesheet" />

<!-- Material Dark -->
<link href="_content/Radzen.Blazor/css/material-base.css" rel="stylesheet" />
<link href="_content/Radzen.Blazor/css/material-dark.css" rel="stylesheet" />
```

### Dynamic Theme Switching
```csharp
// JavaScript interop to swap CSS
@inject IJSRuntime JSRuntime

async Task SwitchTheme(bool isDark)
{
    var theme = isDark ? "material-dark" : "material";
    await JSRuntime.InvokeVoidAsync("eval",
        $@"document.querySelector('link[href*=""material""]').href =
           '_content/Radzen.Blazor/css/{theme}.css';");
}
```

### Custom Theme Variables
Override CSS variables in your `app.css`:

```css
:root {
    --rz-primary: #1976d2;
    --rz-secondary: #424242;
    --rz-info: #0288d1;
    --rz-success: #388e3c;
    --rz-warning: #f57c00;
    --rz-danger: #d32f2f;
    --rz-dark: #212121;
    --rz-light: #f5f5f5;

    --rz-border-radius: 4px;
    --rz-panel-border-radius: 8px;
    --rz-card-padding: 1.5rem;
}
```

---

## Service Configuration

### DialogService
Automatically registered with `AddRadzenComponents()`. Inject and use:

```razor
@inject DialogService DialogService

<RadzenButton Click="@OpenDialog" Text="Open Dialog" />

@code {
    async Task OpenDialog()
    {
        await DialogService.OpenAsync<MyDialogComponent>("Title");
    }
}
```

### NotificationService
```razor
@inject NotificationService NotificationService

@code {
    void ShowNotification()
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Success,
            Summary = "Success",
            Detail = "Operation completed",
            Duration = 4000
        });
    }
}
```

### TooltipService
```razor
@inject TooltipService TooltipService

<RadzenButton @ref="buttonRef" Text="Hover me" MouseEnter="@ShowTooltip" />

@code {
    ElementReference buttonRef;

    void ShowTooltip(ElementReference elementRef)
    {
        TooltipService.Open(elementRef, "Tooltip content");
    }
}
```

### ContextMenuService
```razor
@inject ContextMenuService ContextMenuService

<div @oncontextmenu="@ShowContextMenu" @oncontextmenu:preventDefault>
    Right-click me
</div>

@code {
    void ShowContextMenu(MouseEventArgs args)
    {
        ContextMenuService.Open(args, new List<ContextMenuItem>
        {
            new ContextMenuItem { Text = "Option 1", Value = 1 },
            new ContextMenuItem { Text = "Option 2", Value = 2 }
        }, OnMenuItemClick);
    }

    void OnMenuItemClick(MenuItemEventArgs args) { }
}
```

---

## Common Setup Issues

### Issue: Components Not Rendering
**Cause:** Missing `AddRadzenComponents()` or CSS/JS references

**Solution:**
1. Verify `builder.Services.AddRadzenComponents();` in Program.cs
2. Check CSS in `<head>`
3. Check JS before `</body>`
4. Add `<RadzenComponents />` in MainLayout.razor

### Issue: Dialogs/Notifications Not Working
**Cause:** Missing `<RadzenComponents />` directive

**Solution:** Add at end of MainLayout.razor:
```razor
<RadzenComponents />
```

### Issue: Events Not Firing
**Cause:** Static render mode in .NET 8+

**Solution:** Add render mode to page or component:
```razor
@page "/mypage"
@rendermode InteractiveServer
```

### Issue: Styles Not Applied
**Cause:** Wrong CSS order or missing base CSS

**Solution:** Always include base CSS first:
```html
<link href="_content/Radzen.Blazor/css/material-base.css" rel="stylesheet" />
<link href="_content/Radzen.Blazor/css/material.css" rel="stylesheet" />
```

### Issue: JavaScript Errors
**Cause:** Radzen.Blazor.js not loaded or loaded in wrong order

**Solution:** Ensure Radzen JS loads after Blazor framework:
```html
<script src="_framework/blazor.server.js"></script>
<script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
```

---

## Migration from Older Versions

### From Radzen 4.x to 7.x

**Breaking Changes:**
1. `AddRadzenComponents()` replaces individual service registrations
2. Some component parameter names changed
3. `DialogService.OpenAsync()` signature changed

**Migration Steps:**
1. Update NuGet package: `dotnet add package Radzen.Blazor`
2. Replace service registrations:
   ```csharp
   // OLD
   builder.Services.AddScoped<DialogService>();
   builder.Services.AddScoped<NotificationService>();

   // NEW
   builder.Services.AddRadzenComponents();
   ```
3. Review component usage for parameter changes
4. Test thoroughly

---

## Performance Optimization

### Virtualization for Large Lists
```razor
<RadzenDataList Data="@largeDataset" TItem="Item" AllowVirtualization="true" Style="height:500px;">
    <Template Context="item">
        @item.Name
    </Template>
</RadzenDataList>
```

### Server-Side DataGrid
For large datasets, use server-side data loading:
```razor
<RadzenDataGrid Data="@items"
                Count="@totalCount"
                LoadData="@LoadData"
                AllowPaging="true"
                PageSize="20"
                TItem="Item">
</RadzenDataGrid>

@code {
    async Task LoadData(LoadDataArgs args)
    {
        // Load only requested page from database
        var result = await GetPagedDataAsync(args.Skip, args.Top);
        items = result.Items;
        totalCount = result.TotalCount;
    }
}
```

### Lazy Loading
Load components only when needed:
```razor
@if (showDataGrid)
{
    <RadzenDataGrid Data="@data" TItem="Item">
        @* Grid definition *@
    </RadzenDataGrid>
}
```

---

## Additional Resources

- **Official Documentation**: https://blazor.radzen.com/
- **Getting Started**: https://blazor.radzen.com/get-started
- **Component Demos**: https://blazor.radzen.com/datagrid
- **GitHub Repository**: https://github.com/radzenhq/radzen-blazor
- **NuGet Package**: https://www.nuget.org/packages/Radzen.Blazor
- **Release Notes**: https://github.com/radzenhq/radzen-blazor/releases
