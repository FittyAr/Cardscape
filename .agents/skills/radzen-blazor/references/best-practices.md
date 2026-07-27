# Radzen Blazor Best Practices

## General Principles

### 1. Always Use @ Prefix for Enum Properties
**Most common compile error:**
```razor
@* WRONG - Compile error *@
<RadzenStack Orientation="Orientation.Horizontal" />

@* CORRECT *@
<RadzenStack Orientation="@Orientation.Horizontal" />
```

This applies to ALL enum properties in Radzen components.

### 2. Add RadzenComponents Directive
Always include at end of MainLayout.razor:
```razor
<RadzenComponents />
```

Required for DialogService, NotificationService, TooltipService, ContextMenuService.

### 3. Use Interactive Render Mode (.NET 8+)
Components with events require interactive render mode:
```razor
@page "/products"
@rendermode InteractiveServer

<RadzenButton Click="@HandleClick" />
```

---

## Data Grid Best Practices

### Use Server-Side Loading for Large Datasets
Don't load 10,000+ rows client-side. Use LoadData:

```razor
<RadzenDataGrid @ref="grid"
                Data="@items"
                Count="@totalCount"
                LoadData="@LoadData"
                AllowPaging="true"
                AllowSorting="true"
                AllowFiltering="true"
                PageSize="50"
                TItem="Product">
    <Columns>
        <RadzenDataGridColumn TItem="Product" Property="Name" Title="Name" />
    </Columns>
</RadzenDataGrid>

@code {
    RadzenDataGrid<Product> grid;
    IEnumerable<Product> items;
    int totalCount;

    async Task LoadData(LoadDataArgs args)
    {
        // Only load requested page
        // args.Skip = page offset
        // args.Top = page size
        // args.OrderBy = sort expression
        // args.Filter = filter expression

        var result = await ProductService.GetPagedAsync(
            skip: args.Skip ?? 0,
            take: args.Top ?? 50,
            orderBy: args.OrderBy,
            filter: args.Filter
        );

        items = result.Items;
        totalCount = result.TotalCount;

        // Grid will automatically update
    }
}
```

### Optimize Column Rendering
Only show necessary columns, use Width for fixed columns:

```razor
<RadzenDataGridColumn TItem="Product"
                     Property="Name"
                     Title="Name"
                     Width="200px"        @* Fixed width *@
                     Frozen="true" />     @* Freeze left column *@

<RadzenDataGridColumn TItem="Product"
                     Property="Description"
                     Visible="@showDetails" />  @* Conditionally show *@
```

### Use Template Columns Sparingly
Template columns disable some optimizations. Use Property when possible:

```razor
@* GOOD - Uses property binding *@
<RadzenDataGridColumn TItem="Product" Property="Price" FormatString="{0:C}" />

@* SLOWER - Uses template (but necessary for custom content) *@
<RadzenDataGridColumn TItem="Product" Title="Price">
    <Template Context="product">
        <span style="color: @(product.Price > 100 ? "red" : "green")">
            @product.Price.ToString("C")
        </span>
    </Template>
</RadzenDataGridColumn>
```

### Reuse Grid References
Store grid reference for programmatic control:

```razor
<RadzenDataGrid @ref="grid" Data="@items" TItem="Product">
    @* Columns *@
</RadzenDataGrid>

<RadzenButton Text="Refresh" Click="@(() => grid.Reload())" />
<RadzenButton Text="Export" Click="@ExportGrid" />

@code {
    RadzenDataGrid<Product> grid;

    async Task ExportGrid()
    {
        await grid.ExportExcel();
    }
}
```

---

## Form Best Practices

### Use Two-Way Binding
Simplifies form state management:

```razor
<RadzenTextBox @bind-Value="@model.Name" />
<RadzenNumeric @bind-Value="@model.Quantity" TValue="int" />
<RadzenDatePicker @bind-Value="@model.StartDate" />
```

### Group Related Fields
Use RadzenFieldset for logical grouping:

```razor
<RadzenStack Gap="20px">
    <RadzenFieldset Text="Personal Information">
        <RadzenStack Gap="12px">
            <div class="rz-form-group">
                <RadzenLabel Text="Name" />
                <RadzenTextBox @bind-Value="@model.Name" />
            </div>
            <div class="rz-form-group">
                <RadzenLabel Text="Email" />
                <RadzenTextBox @bind-Value="@model.Email" />
            </div>
        </RadzenStack>
    </RadzenFieldset>

    <RadzenFieldset Text="Address">
        <RadzenStack Gap="12px">
            @* Address fields *@
        </RadzenStack>
    </RadzenFieldset>
</RadzenStack>
```

### Validate Before Submit
Use RadzenTemplateForm with validators:

```razor
<RadzenTemplateForm Data="@model" Submit="@OnValidSubmit">
    <RadzenStack Gap="12px">
        <div class="rz-form-group">
            <RadzenLabel Text="Email" />
            <RadzenTextBox @bind-Value="@model.Email" Name="Email" />
            <RadzenRequiredValidator Component="Email" Text="Email is required" />
            <RadzenEmailValidator Component="Email" Text="Invalid email format" />
        </div>

        <div class="rz-form-group">
            <RadzenLabel Text="Age" />
            <RadzenNumeric @bind-Value="@model.Age" TValue="int" Name="Age" />
            <RadzenNumericRangeValidator Component="Age" Min="18" Max="120"
                                         Text="Age must be between 18 and 120" />
        </div>

        <RadzenButton ButtonType="@ButtonType.Submit" Text="Save" />
    </RadzenStack>
</RadzenTemplateForm>

@code {
    MyModel model = new();

    void OnValidSubmit(MyModel validModel)
    {
        // Form is valid, save data
    }
}
```

### Disable Submit While Processing
Prevent double-submission:

```razor
<RadzenButton ButtonType="@ButtonType.Submit"
              Text="@(isSaving ? "Saving..." : "Save")"
              IsBusy="@isSaving"
              Disabled="@isSaving" />

@code {
    bool isSaving = false;

    async Task OnValidSubmit(MyModel model)
    {
        isSaving = true;
        try
        {
            await SaveAsync(model);
        }
        finally
        {
            isSaving = false;
        }
    }
}
```

---

## Dialog Best Practices

### Create Reusable Dialog Components
Make self-contained dialog components:

```razor
@* EditProductDialog.razor *@
@inject DialogService DialogService

<RadzenStack Gap="16px">
    <RadzenFieldset Text="Edit Product">
        <RadzenStack Gap="12px">
            <RadzenTextBox @bind-Value="@Product.Name" Placeholder="Name" />
            <RadzenNumeric @bind-Value="@Product.Price" TValue="decimal" />
        </RadzenStack>
    </RadzenFieldset>

    <RadzenStack Orientation="@Orientation.Horizontal"
                 JustifyContent="@JustifyContent.End"
                 Gap="8px">
        <RadzenButton Text="Cancel"
                      ButtonStyle="@ButtonStyle.Light"
                      Click="@Cancel" />
        <RadzenButton Text="Save"
                      ButtonStyle="@ButtonStyle.Primary"
                      Click="@Save"
                      Disabled="@string.IsNullOrWhiteSpace(Product.Name)" />
    </RadzenStack>
</RadzenStack>

@code {
    [Parameter] public Product Product { get; set; } = new();

    void Cancel() => DialogService.Close(null);

    void Save()
    {
        // Validate
        if (string.IsNullOrWhiteSpace(Product.Name))
            return;

        DialogService.Close(Product);
    }
}
```

### Handle Dialog Results
Always check for null result (user cancelled):

```razor
@inject DialogService DialogService

async Task EditProduct(Product product)
{
    var result = await DialogService.OpenAsync<EditProductDialog>(
        "Edit Product",
        new Dictionary<string, object> { { "Product", product } },
        new DialogOptions { Width = "600px" }
    );

    if (result != null)
    {
        // User clicked Save
        await SaveProductAsync(result);
    }
    // If null, user clicked Cancel or closed dialog
}
```

### Use Confirm Dialogs for Destructive Actions
```razor
async Task DeleteProduct(Product product)
{
    var confirmed = await DialogService.Confirm(
        $"Are you sure you want to delete '{product.Name}'?",
        "Confirm Delete",
        new ConfirmOptions
        {
            OkButtonText = "Delete",
            CancelButtonText = "Cancel",
            AutoFocusFirstElement = false
        }
    );

    if (confirmed == true)
    {
        await ProductService.DeleteAsync(product.Id);
        NotificationService.Notify(NotificationSeverity.Success, "Deleted", "Product deleted successfully");
        await grid.Reload();
    }
}
```

---

## Notification Best Practices

### Use Appropriate Severity Levels
```razor
@inject NotificationService NotificationService

void NotifySuccess(string message)
{
    NotificationService.Notify(new NotificationMessage
    {
        Severity = NotificationSeverity.Success,
        Summary = "Success",
        Detail = message,
        Duration = 4000  // 4 seconds
    });
}

void NotifyError(string message)
{
    NotificationService.Notify(new NotificationMessage
    {
        Severity = NotificationSeverity.Error,
        Summary = "Error",
        Detail = message,
        Duration = 8000,  // Longer for errors
        CloseOnClick = true
    });
}

void NotifyWarning(string message)
{
    NotificationService.Notify(new NotificationMessage
    {
        Severity = NotificationSeverity.Warning,
        Summary = "Warning",
        Detail = message,
        Duration = 6000
    });
}
```

### Don't Spam Notifications
Avoid showing notifications in loops or rapid succession:

```razor
@* BAD - Shows 100 notifications *@
foreach (var item in items)
{
    NotificationService.Notify(NotificationSeverity.Info, "Processing", item.Name);
}

@* GOOD - Single summary notification *@
NotificationService.Notify(NotificationSeverity.Success, "Complete", $"Processed {items.Count} items");
```

### Provide Meaningful Messages
```razor
@* BAD *@
NotificationService.Notify(NotificationSeverity.Error, "Error", "An error occurred");

@* GOOD *@
NotificationService.Notify(NotificationSeverity.Error, "Save Failed",
    $"Could not save product '{product.Name}'. Please check required fields and try again.");
```

---

## Layout Best Practices

### Use Semantic Layout Structure
```razor
<RadzenLayout>
    <RadzenHeader>
        @* App title, user menu, etc. *@
    </RadzenHeader>

    <RadzenSidebar @bind-Expanded="sidebarExpanded">
        <RadzenPanelMenu>
            @* Navigation *@
        </RadzenPanelMenu>
    </RadzenSidebar>

    <RadzenBody>
        <div class="body-content">
            @Body
        </div>
    </RadzenBody>

    <RadzenFooter>
        @* Copyright, links, etc. *@
    </RadzenFooter>
</RadzenLayout>

<RadzenComponents />
```

### Use RadzenStack for Consistent Spacing
Better than manual margin/padding:

```razor
@* GOOD - Consistent spacing *@
<RadzenStack Gap="20px">
    <RadzenCard>Section 1</RadzenCard>
    <RadzenCard>Section 2</RadzenCard>
    <RadzenCard>Section 3</RadzenCard>
</RadzenStack>

@* BAD - Manual spacing *@
<div style="margin-bottom: 20px;">
    <RadzenCard>Section 1</RadzenCard>
</div>
<div style="margin-bottom: 20px;">
    <RadzenCard>Section 2</RadzenCard>
</div>
```

### Responsive Design with RadzenRow/RadzenColumn
```razor
<RadzenRow Gap="20px">
    @* Full width on mobile, half on medium+, third on large+ *@
    <RadzenColumn Size="12" Medium="6" Large="4">
        <RadzenCard>Card 1</RadzenCard>
    </RadzenColumn>
    <RadzenColumn Size="12" Medium="6" Large="4">
        <RadzenCard>Card 2</RadzenCard>
    </RadzenColumn>
    <RadzenColumn Size="12" Medium="12" Large="4">
        <RadzenCard>Card 3</RadzenCard>
    </RadzenColumn>
</RadzenRow>
```

---

## Performance Best Practices

### Use @key for Dynamic Lists
Helps Blazor track component identity:

```razor
<RadzenStack Gap="8px">
    @foreach (var item in items)
    {
        <RadzenCard @key="item.Id">
            <RadzenText Text="@item.Name" />
        </RadzenCard>
    }
</RadzenStack>
```

### Avoid Expensive Computations in Render
Move to methods or computed properties:

```razor
@* BAD - Recalculates every render *@
<RadzenText Text="@items.Where(x => x.IsActive).Sum(x => x.Price).ToString("C")" />

@* GOOD - Calculate once *@
<RadzenText Text="@totalActivePrice.ToString("C")" />

@code {
    decimal totalActivePrice => items.Where(x => x.IsActive).Sum(x => x.Price);
}
```

### Use Virtualization for Long Lists
```razor
<Virtualize Items="@longList" Context="item">
    <RadzenCard>
        <RadzenText Text="@item.Name" />
    </RadzenCard>
</Virtualize>
```

### Debounce Search Input
Don't search on every keystroke:

```razor
<RadzenTextBox @bind-Value="@searchText"
               Change="@OnSearchChanged"
               Placeholder="Search..." />

@code {
    string searchText;
    Timer debounceTimer;

    void OnSearchChanged(string value)
    {
        debounceTimer?.Dispose();
        debounceTimer = new Timer(_ =>
        {
            InvokeAsync(() =>
            {
                PerformSearch(value);
                StateHasChanged();
            });
        }, null, 500, Timeout.Infinite);  // 500ms delay
    }
}
```

---

## Accessibility Best Practices

### Use RadzenLabel with Component Attribute
Links label to input for screen readers:

```razor
<RadzenLabel Text="Email Address" Component="emailInput" />
<RadzenTextBox Name="emailInput" @bind-Value="@email" />
```

### Provide Alt Text for Icons
```razor
<RadzenButton Icon="delete"
              Text="Delete Product"  @* Screen readers read this *@
              ButtonStyle="@ButtonStyle.Danger" />

@* Icon-only button - use aria-label *@
<RadzenButton Icon="delete"
              aria-label="Delete Product"
              ButtonStyle="@ButtonStyle.Danger" />
```

### Keyboard Navigation
Ensure dialogs and menus are keyboard-accessible (Radzen handles this by default).

### Color Contrast
When using custom colors, ensure sufficient contrast:

```razor
@* GOOD - Meets WCAG AA standards *@
<RadzenText Style="color: #0066cc; background: #ffffff;">Readable</RadzenText>

@* BAD - Poor contrast *@
<RadzenText Style="color: #dddddd; background: #ffffff;">Hard to read</RadzenText>
```

---

## Styling Best Practices

### Use Theme Variables
Don't hardcode colors:

```razor
@* GOOD - Uses theme variable *@
<RadzenText Style="color: var(--rz-primary);">Primary color text</RadzenText>

@* BAD - Hardcoded color *@
<RadzenText Style="color: #1976d2;">Blue text</RadzenText>
```

### Override Variables, Not Components
In your app.css:

```css
/* GOOD - Override theme variables */
:root {
    --rz-primary: #ff5722;
    --rz-border-radius: 8px;
}

/* BAD - Override component styles (fragile) */
.rz-button {
    background-color: #ff5722 !important;
}
```

### Use Radzen Components Over Raw HTML
```razor
@* GOOD - Themed automatically *@
<RadzenCard>
    <RadzenText Text="Content" TextStyle="@TextStyle.H6" />
</RadzenCard>

@* BAD - Not themed *@
<div class="card">
    <h6>Content</h6>
</div>
```

---

## Error Handling Best Practices

### Catch Exceptions in Event Handlers
Blazor won't crash, but log errors:

```razor
@inject ILogger<MyComponent> Logger

async Task SaveProduct()
{
    try
    {
        await ProductService.SaveAsync(product);
        NotificationService.Notify(NotificationSeverity.Success, "Success", "Product saved");
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to save product {ProductId}", product.Id);
        NotificationService.Notify(NotificationSeverity.Error, "Error",
            $"Failed to save product: {ex.Message}");
    }
}
```

### Show User-Friendly Error Messages
```razor
try
{
    await DeleteProductAsync(id);
}
catch (DbUpdateException)
{
    NotificationService.Notify(NotificationSeverity.Error, "Cannot Delete",
        "This product cannot be deleted because it has associated orders.");
}
catch (Exception ex)
{
    NotificationService.Notify(NotificationSeverity.Error, "Error",
        "An unexpected error occurred. Please try again or contact support.");
    Logger.LogError(ex, "Error deleting product {ProductId}", id);
}
```

### Use RadzenAlert for Persistent Errors
```razor
@if (!string.IsNullOrEmpty(errorMessage))
{
    <RadzenAlert AlertStyle="@AlertStyle.Danger"
                 Variant="@Variant.Flat"
                 AllowClose="true"
                 Close="@(() => errorMessage = null)">
        <RadzenText TextStyle="@TextStyle.Subtitle2" TagName="@TagName.H4">Error</RadzenText>
        <RadzenText>@errorMessage</RadzenText>
    </RadzenAlert>
}
```

---

## Testing Best Practices

### Make Components Testable
Inject dependencies, avoid static references:

```razor
@* GOOD - Testable *@
@inject IProductService ProductService

@* BAD - Hard to test *@
@code {
    void LoadProducts() {
        var products = ProductRepository.GetAll();  // Static reference
    }
}
```

### Use Parameters for Configuration
```razor
@* GOOD - Configurable *@
<RadzenDataGrid Data="@products"
                AllowPaging="@allowPaging"
                PageSize="@pageSize"
                TItem="Product">
</RadzenDataGrid>

@code {
    [Parameter] public bool AllowPaging { get; set; } = true;
    [Parameter] public int PageSize { get; set; } = 20;
}
```

---

## Security Best Practices

### Validate User Input
Never trust client-side validation alone:

```csharp
// Server-side API
[HttpPost]
public async Task<IActionResult> SaveProduct([FromBody] Product product)
{
    // Validate on server
    if (string.IsNullOrWhiteSpace(product.Name))
        return BadRequest("Product name is required");

    if (product.Price < 0)
        return BadRequest("Price must be positive");

    await _productService.SaveAsync(product);
    return Ok();
}
```

### Sanitize HTML Content
Use Radzen components, avoid raw HTML injection:

```razor
@* GOOD - Safe *@
<RadzenText Text="@userInput" />

@* DANGEROUS - XSS risk *@
<div>@((MarkupString)userInput)</div>
```

### Use Authorization
```razor
@attribute [Authorize(Roles = "Admin")]

<RadzenButton Text="Delete All" Click="@DeleteAll" />
```

---

## Summary Checklist

- [ ] Always use `@` prefix for enum properties
- [ ] Add `<RadzenComponents />` in MainLayout
- [ ] Use interactive render mode for components with events
- [ ] Use server-side loading for large DataGrids
- [ ] Validate forms before submission
- [ ] Handle dialog results (check for null)
- [ ] Use appropriate notification durations and severities
- [ ] Use theme variables instead of hardcoded colors
- [ ] Catch and log exceptions in event handlers
- [ ] Make components testable with dependency injection
- [ ] Validate user input on server side
- [ ] Use `@key` for dynamic lists
- [ ] Debounce search inputs
- [ ] Provide meaningful error messages
