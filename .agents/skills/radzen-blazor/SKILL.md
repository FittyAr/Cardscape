---
name: radzen-blazor
description: >
  Expert guidance for Radzen Blazor UI components - a free, open-source library
  of 100+ native Blazor components. Use when working with Radzen components,
  creating Blazor UI with DataGrids, forms, charts, layouts, or when user mentions
  "Radzen". Covers setup, component usage, theming, data binding, validation,
  dialogs, notifications, and CRUD operations with Radzen.Blazor components.
---

# Radzen Blazor Components Skill

Expert guidance for building Blazor applications with Radzen components (v7.1.0).

## 🚨 CRITICAL: Enum Properties Require @ Prefix

**This is the #1 compile error when using Radzen components.**

**WRONG** (compile error):
```razor
<RadzenStack Orientation="Orientation.Horizontal" />
<RadzenPanelMenu DisplayStyle="MenuItemDisplayStyle.IconAndText" />
```

**CORRECT** (always use @ prefix):
```razor
<RadzenStack Orientation="@Orientation.Horizontal" />
<RadzenPanelMenu DisplayStyle="@MenuItemDisplayStyle.IconAndText" />
```

**Common Radzen Enums:**
- `Orientation` - Horizontal, Vertical
- `AlignItems` - Start, Center, End, Stretch
- `JustifyContent` - Start, Center, End, SpaceBetween, SpaceAround, SpaceEvenly
- `MenuItemDisplayStyle` - Icon, Text, IconAndText
- `ButtonStyle` - Primary, Secondary, Danger, Warning, Success, Info, Light
- `ButtonSize` - Small, Medium, Large
- `TextStyle` - H1-H6, Subtitle1, Subtitle2, Body1, Body2, Caption, Overline
- `AlertStyle` - Danger, Warning, Info, Success
- `Variant` - Filled, Flat, Outlined, Text
- `Shade` - Default, Lighter, Darker, Light, Dark
- `TextAlign` - Left, Center, Right
- `DataGridEditMode` - Single, Multiple
- `DataGridSelectionMode` - Single, Multiple
- `NotificationSeverity` - Success, Info, Warning, Error

---

## Quick Setup

### 1. Install NuGet Package
```bash
dotnet add package Radzen.Blazor
```

### 2. Add Service Registration
In `Program.cs`:
```csharp
using Radzen;
builder.Services.AddRadzenComponents();
```

### 3. Add References
In `_Imports.razor`:
```razor
@using Radzen
@using Radzen.Blazor
```

### 4. Include CSS/JS
In `_Layout.cshtml` or `App.razor`:
```html
<link rel="stylesheet" href="_content/Radzen.Blazor/css/material-base.css">
<link rel="stylesheet" href="_content/Radzen.Blazor/css/material.css">
<script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
```

**Available Themes:**
- Material: `material.css` / `material-dark.css`
- Default: `default.css` / `default-dark.css`
- Humanistic: `humanistic.css` / `humanistic-dark.css`
- Software: `software.css` / `software-dark.css`

### 5. Add RadzenComponents Directive
**CRITICAL:** Add at end of `MainLayout.razor`:
```razor
<RadzenComponents />
```
This is **required** for DialogService, NotificationService, TooltipService, and ContextMenuService.

For detailed setup, see [references/setup-patterns.md](references/setup-patterns.md).

---

## Layout Components

### RadzenLayout (Application Structure)
Complete application layout with header, sidebar, body, and footer.

```razor
<RadzenLayout class="app-layout">
    <RadzenHeader class="app-header">
        <RadzenSidebarToggle Click="@ToggleSidebar" />
        <RadzenLabel Text="My App" />
    </RadzenHeader>
    <RadzenSidebar class="app-sidebar" @bind-Expanded="sidebarExpanded">
        <NavMenu />
    </RadzenSidebar>
    <RadzenBody class="app-body">
        @Body
    </RadzenBody>
    <RadzenFooter class="app-footer">
        <RadzenText Text="© 2026 My Company" />
    </RadzenFooter>
</RadzenLayout>

<RadzenComponents />

@code {
    private bool sidebarExpanded = true;
    private void ToggleSidebar() => sidebarExpanded = !sidebarExpanded;
}
```

**Key Properties:**
- `RadzenSidebar` - `@bind-Expanded` for collapse state
- `RadzenSidebarToggle` - Automatically toggles sidebar

---

## Navigation Components

### RadzenPanelMenu (Hierarchical Menu)
```razor
<RadzenPanelMenu DisplayStyle="@MenuItemDisplayStyle.IconAndText" Multiple="false">
    <RadzenPanelMenuItem Text="Home" Icon="home" Path="/" />
    <RadzenPanelMenuItem Text="Products" Icon="inventory" Expanded="true">
        <RadzenPanelMenuItem Text="Categories" Icon="category" Path="/categories" />
        <RadzenPanelMenuItem Text="Items" Icon="list" Path="/items" />
    </RadzenPanelMenuItem>
</RadzenPanelMenu>
```

### RadzenMenu (Horizontal Menu)
```razor
<RadzenMenu>
    <RadzenMenuItem Text="File">
        <RadzenMenuItem Text="New" Icon="add" Click="@OnNew" />
        <RadzenMenuItem Text="Open" Icon="folder_open" Click="@OnOpen" />
    </RadzenMenuItem>
    <RadzenMenuItem Text="Edit">
        <RadzenMenuItem Text="Copy" Icon="content_copy" />
        <RadzenMenuItem Text="Paste" Icon="content_paste" />
    </RadzenMenuItem>
</RadzenMenu>
```

### RadzenBreadCrumb
```razor
<RadzenBreadCrumb>
    <RadzenBreadCrumbItem Path="/" Text="Home" />
    <RadzenBreadCrumbItem Path="/products" Text="Products" />
    <RadzenBreadCrumbItem Text="Category A" />
</RadzenBreadCrumb>
```

### RadzenSteps (Wizard)
```razor
<RadzenSteps @bind-SelectedIndex="@selectedIndex">
    <Steps>
        <RadzenStepsItem Text="Account Info" />
        <RadzenStepsItem Text="Billing" />
        <RadzenStepsItem Text="Confirmation" />
    </Steps>
</RadzenSteps>
```

---

## Container Components

### RadzenStack (Flexible Layout)
Arrange elements horizontally or vertically with gap spacing.

```razor
@* Vertical stack (default) *@
<RadzenStack Gap="20px">
    <RadzenCard>Item 1</RadzenCard>
    <RadzenCard>Item 2</RadzenCard>
</RadzenStack>

@* Horizontal stack with alignment *@
<RadzenStack Orientation="@Orientation.Horizontal"
             JustifyContent="@JustifyContent.SpaceBetween"
             AlignItems="@AlignItems.Center"
             Gap="8px">
    <RadzenText Text="Left" />
    <RadzenButton Text="Right" />
</RadzenStack>
```

### RadzenRow & RadzenColumn (Responsive Grid)
Bootstrap-style responsive grid system.

```razor
<RadzenRow Gap="20px">
    <RadzenColumn Size="12" Medium="6" Large="3">
        <RadzenCard>Quarter on large screens</RadzenCard>
    </RadzenColumn>
    <RadzenColumn Size="12" Medium="6" Large="9">
        <RadzenCard>Three-quarters on large screens</RadzenCard>
    </RadzenColumn>
</RadzenRow>
```

**Breakpoints:**
- `Size` - Extra small (default, all screens)
- `Small` - ≥576px
- `Medium` - ≥768px
- `Large` - ≥992px
- `ExtraLarge` - ≥1200px

### RadzenCard
```razor
<RadzenCard>
    <RadzenStack Gap="8px">
        <RadzenText Text="Card Title" TextStyle="@TextStyle.H6" />
        <RadzenText Text="Card content goes here." />
    </RadzenStack>
</RadzenCard>
```

### RadzenFieldset
```razor
<RadzenFieldset Text="User Information">
    <RadzenStack Gap="12px">
        <RadzenLabel Text="Name" />
        <RadzenTextBox @bind-Value="@name" />
    </RadzenStack>
</RadzenFieldset>
```

### RadzenAccordion
```razor
<RadzenAccordion>
    <Items>
        <RadzenAccordionItem Text="Section 1" Icon="info">
            Content for section 1
        </RadzenAccordionItem>
        <RadzenAccordionItem Text="Section 2" Icon="settings">
            Content for section 2
        </RadzenAccordionItem>
    </Items>
</RadzenAccordion>
```

### RadzenTabs
```razor
<RadzenTabs @bind-SelectedIndex="@selectedTab">
    <Tabs>
        <RadzenTabsItem Text="Profile">
            Profile content
        </RadzenTabsItem>
        <RadzenTabsItem Text="Settings">
            Settings content
        </RadzenTabsItem>
    </Tabs>
</RadzenTabs>
```

---

## Data Display Components

### RadzenDataGrid (Primary Data Component)
Powerful grid with sorting, filtering, paging, editing, selection, grouping.

**Basic Grid:**
```razor
<RadzenDataGrid Data="@products"
                TItem="Product"
                AllowFiltering="true"
                AllowSorting="true"
                AllowPaging="true"
                PageSize="20"
                Style="width: 100%;">
    <Columns>
        <RadzenDataGridColumn TItem="Product" Property="Name" Title="Product Name" />
        <RadzenDataGridColumn TItem="Product" Property="Price" Title="Price" FormatString="{0:C}" />
        <RadzenDataGridColumn TItem="Product" Property="InStock" Title="In Stock" />
    </Columns>
</RadzenDataGrid>
```

**With Inline Editing:**
```razor
<RadzenDataGrid @ref="grid"
                Data="@products"
                TItem="Product"
                EditMode="@DataGridEditMode.Single"
                RowUpdate="@OnRowUpdated"
                RowCreate="@OnRowCreated">
    <Columns>
        <RadzenDataGridColumn TItem="Product" Property="Name" Title="Name" Editable="true" />
        <RadzenDataGridColumn TItem="Product" Property="Price" Title="Price" Editable="true" />
        <RadzenDataGridColumn TItem="Product" Title="Actions">
            <Template Context="row">
                <RadzenButton Icon="edit" Size="@ButtonSize.Small"
                            Click="@(_ => grid.EditRow(row))"
                            ButtonStyle="@ButtonStyle.Light" />
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>

@code {
    RadzenDataGrid<Product> grid;

    void OnRowUpdated(Product product) { /* Save */ }
    void OnRowCreated(Product product) { /* Create */ }
}
```

**With Selection:**
```razor
<RadzenDataGrid Data="@users"
                TItem="User"
                SelectionMode="@DataGridSelectionMode.Single"
                @bind-Value="@selectedUsers">
    <Columns>
        <RadzenDataGridColumn TItem="User" Property="Name" />
    </Columns>
</RadzenDataGrid>

@code {
    IList<User> selectedUsers;
}
```

**Template Columns:**
```razor
<RadzenDataGridColumn TItem="Product" Title="Actions"
                     Sortable="false" Filterable="false"
                     Width="120px" TextAlign="@TextAlign.Center">
    <Template Context="product">
        <RadzenButton Icon="edit" Size="@ButtonSize.Small"
                    Click="@(_ => Edit(product))"
                    ButtonStyle="@ButtonStyle.Light" />
        <RadzenButton Icon="delete" Size="@ButtonSize.Small"
                    Click="@(_ => Delete(product))"
                    ButtonStyle="@ButtonStyle.Danger" />
    </Template>
</RadzenDataGridColumn>
```

See [references/components-quick-ref.md](references/components-quick-ref.md) for complete DataGrid reference.

### RadzenDataList
```razor
<RadzenDataList Data="@products" TItem="Product">
    <Template Context="product">
        <RadzenCard>
            <RadzenText Text="@product.Name" TextStyle="@TextStyle.H6" />
            <RadzenText Text="@($"${product.Price:N2}")" />
        </RadzenCard>
    </Template>
</RadzenDataList>
```

### RadzenTree
```razor
<RadzenTree Data="@treeData" Change="@OnChange">
    <RadzenTreeLevel Text="@GetTextForItem" />
</RadzenTree>
```

### RadzenScheduler
```razor
<RadzenScheduler @ref="scheduler" TItem="Appointment" Data="@appointments">
    <RadzenDayView />
    <RadzenWeekView />
    <RadzenMonthView />
</RadzenScheduler>
```

### RadzenChart
```razor
<RadzenChart>
    <RadzenColumnSeries Data="@revenue" CategoryProperty="Month" ValueProperty="Amount" />
</RadzenChart>
```

---

## Form Components

### RadzenTextBox
```razor
<RadzenLabel Text="Name" />
<RadzenTextBox @bind-Value="@model.Name"
               Placeholder="Enter name"
               MaxLength="100" />
```

### RadzenTextArea
```razor
<RadzenTextArea @bind-Value="@model.Description"
                Rows="5"
                MaxLength="500" />
```

### RadzenNumeric<T>
```razor
<RadzenNumeric @bind-Value="@model.Quantity" TValue="int" Min="0" Max="100" />
<RadzenNumeric @bind-Value="@model.Price" TValue="decimal" Format="c" />
```

### RadzenDatePicker
```razor
<RadzenDatePicker @bind-Value="@model.StartDate"
                  DateFormat="MM/dd/yyyy"
                  ShowTime="false" />
```

### RadzenDropDown
```razor
<RadzenDropDown Data="@countries"
                @bind-Value="@selectedCountryId"
                TextProperty="Name"
                ValueProperty="Id"
                AllowClear="true"
                Placeholder="Select a country" />
```

### RadzenAutoComplete
```razor
<RadzenAutoComplete Data="@cities"
                    @bind-Value="@selectedCity"
                    TextProperty="Name"
                    Change="@OnCityChanged" />
```

### RadzenCheckBox
```razor
<RadzenCheckBox @bind-Value="@model.IsActive" />
<RadzenLabel Text="Is Active" Component="IsActive" />
```

### RadzenRadioButtonList
```razor
<RadzenRadioButtonList @bind-Value="@selectedOption" TValue="string">
    <Items>
        <RadzenRadioButtonListItem Text="Option 1" Value="opt1" />
        <RadzenRadioButtonListItem Text="Option 2" Value="opt2" />
    </Items>
</RadzenRadioButtonList>
```

### RadzenSwitch
```razor
<RadzenSwitch @bind-Value="@model.Enabled" />
```

### RadzenSlider
```razor
<RadzenSlider @bind-Value="@volume" TValue="int" Min="0" Max="100" />
```

### RadzenRating
```razor
<RadzenRating @bind-Value="@rating" Stars="5" />
```

### RadzenFileInput
```razor
<RadzenFileInput Choose="@OnFileChosen" />

@code {
    void OnFileChosen(FileChangedEventArgs args) {
        // Handle file upload
    }
}
```

### RadzenPassword
```razor
<RadzenPassword @bind-Value="@password" />
```

### RadzenMask
```razor
<RadzenMask @bind-Value="@phone" Mask="(999) 999-9999" />
```

---

## Button Components

### RadzenButton
```razor
<RadzenButton Text="Save"
              Icon="save"
              Click="@OnSave"
              ButtonStyle="@ButtonStyle.Primary"
              Size="@ButtonSize.Medium"
              Disabled="@isSaving" />
```

### RadzenSplitButton
```razor
<RadzenSplitButton Text="Actions" Click="@OnDefaultAction">
    <ChildContent>
        <RadzenSplitButtonItem Text="Edit" Icon="edit" Click="@OnEdit" />
        <RadzenSplitButtonItem Text="Delete" Icon="delete" Click="@OnDelete" />
    </ChildContent>
</RadzenSplitButton>
```

### RadzenToggleButton
```razor
<RadzenToggleButton @bind-Value="@isToggled"
                    Text="@(isToggled ? "On" : "Off")"
                    Icon="@(isToggled ? "check_circle" : "cancel")" />
```

---

## Typography & Display

### RadzenText
```razor
<RadzenText Text="Heading" TextStyle="@TextStyle.H4" />
<RadzenText Text="Subtitle" TextStyle="@TextStyle.Subtitle2" />
<RadzenText Text="Body text" TextStyle="@TextStyle.Body1" />
<RadzenText Text="Caption" TextStyle="@TextStyle.Caption" />
<RadzenText Text="@($"${amount:N2}")" TextStyle="@TextStyle.H4" />
```

### RadzenLabel
```razor
<RadzenLabel Text="Product Name" Component="productName" />
<RadzenTextBox Name="productName" @bind-Value="@name" />
```

### RadzenHeading
```razor
<RadzenHeading Size="1" Text="Page Title" />
<RadzenHeading Size="2" Text="Section Title" />
```

### RadzenBadge
```razor
<RadzenBadge BadgeStyle="@BadgeStyle.Danger" Text="3" />
<RadzenBadge BadgeStyle="@BadgeStyle.Success" Text="New" />
```

### RadzenIcon
```razor
<RadzenIcon Icon="home" />
<RadzenIcon Icon="shopping_cart" Style="color: red;" />
```

---

## Feedback Components

### RadzenAlert
```razor
<RadzenAlert AlertStyle="@AlertStyle.Danger"
             Variant="@Variant.Flat"
             Shade="@Shade.Lighter">
    <RadzenText TextStyle="@TextStyle.Subtitle2" TagName="@TagName.H4">Error</RadzenText>
    <RadzenText>@errorMessage</RadzenText>
</RadzenAlert>

<RadzenAlert AlertStyle="@AlertStyle.Success" Variant="@Variant.Flat">
    <RadzenText>Operation completed successfully!</RadzenText>
</RadzenAlert>
```

### RadzenProgressBar
```razor
<RadzenProgressBar Value="@progress" Max="100" ShowValue="true" />
```

### RadzenProgressBarCircular
```razor
<RadzenProgressBarCircular Value="@progress" Max="100" ShowValue="true" />
```

### RadzenTooltip
```razor
<RadzenButton Text="Hover me" MouseEnter="@(args => ShowTooltip(args))" />

@inject TooltipService TooltipService

@code {
    void ShowTooltip(ElementReference elementRef) {
        TooltipService.Open(elementRef, "Tooltip content", new TooltipOptions());
    }
}
```

---

## Services

### DialogService
Open modal dialogs with custom components.

```razor
@inject DialogService DialogService

<RadzenButton Click="@OpenDialog" Text="Open Dialog" />

@code {
    async Task OpenDialog() {
        var result = await DialogService.OpenAsync<EditProductDialog>(
            "Edit Product",
            new Dictionary<string, object>() {
                { "Product", selectedProduct }
            },
            new DialogOptions() {
                Width = "700px",
                Height = "512px",
                Resizable = true,
                Draggable = true,
                CloseDialogOnOverlayClick = false
            }
        );

        if (result != null) {
            // Handle result
        }
    }
}
```

**In the dialog component:**
```razor
@inject DialogService DialogService

<RadzenStack Gap="16px">
    @* Form content *@
    <RadzenStack Orientation="@Orientation.Horizontal" JustifyContent="@JustifyContent.End">
        <RadzenButton Text="Cancel" Click="@(() => DialogService.Close(null))" />
        <RadzenButton Text="Save" Click="@Save" />
    </RadzenStack>
</RadzenStack>

@code {
    [Parameter] public Product Product { get; set; }

    void Save() {
        DialogService.Close(Product);
    }
}
```

### NotificationService
Show toast notifications.

```razor
@inject NotificationService NotificationService

@code {
    void ShowSuccess(string message) {
        NotificationService.Notify(new NotificationMessage {
            Severity = NotificationSeverity.Success,
            Summary = "Success",
            Detail = message,
            Duration = 4000
        });
    }

    void ShowError(string message) {
        NotificationService.Notify(new NotificationMessage {
            Severity = NotificationSeverity.Error,
            Summary = "Error",
            Detail = message,
            Duration = 8000
        });
    }

    void ShowWarning(string message) {
        NotificationService.Notify(new NotificationMessage {
            Severity = NotificationSeverity.Warning,
            Summary = "Warning",
            Detail = message,
            Duration = 6000
        });
    }
}
```

### ContextMenuService
Show context menus on right-click.

```razor
@inject ContextMenuService ContextMenuService

<div @oncontextmenu="@ShowContextMenu" @oncontextmenu:preventDefault="true">
    Right-click me
</div>

@code {
    void ShowContextMenu(MouseEventArgs args) {
        ContextMenuService.Open(args, new List<ContextMenuItem> {
            new ContextMenuItem { Text = "Edit", Value = 1 },
            new ContextMenuItem { Text = "Delete", Value = 2 }
        }, OnMenuItemClick);
    }

    void OnMenuItemClick(MenuItemEventArgs args) {
        var value = args.Value;
        // Handle menu item click
    }
}
```

---

## Best Practices

### .NET 8+ Render Modes
Components with events require interactive render mode:
```razor
<RadzenButton @rendermode="InteractiveServer" Click="@HandleClick" />
```

### Data Binding Patterns
**Two-way binding:**
```razor
<RadzenTextBox @bind-Value="@model.Name" />
```

**One-way binding with change handler:**
```razor
<RadzenTextBox Value="@model.Name" Change="@(args => model.Name = args)" />
```

### Event Handling
```razor
<RadzenButton Click="@(() => OnClick())" Text="Click" />
<RadzenButton Click="@OnClickAsync" Text="Async Click" />

@code {
    void OnClick() { }
    async Task OnClickAsync() { await Task.Delay(100); }
}
```

### Form Validation
```razor
<RadzenTemplateForm Data="@model" Submit="@OnSubmit">
    <RadzenTextBox @bind-Value="@model.Email" Name="Email" />
    <RadzenRequiredValidator Component="Email" Text="Email required" />
    <RadzenEmailValidator Component="Email" Text="Invalid email" />

    <RadzenButton ButtonType="@ButtonType.Submit" Text="Submit" />
</RadzenTemplateForm>

@code {
    void OnSubmit(MyModel model) {
        // Handle valid form submission
    }
}
```

---

## Common Patterns

### Complete CRUD Page
```razor
<RadzenStack Gap="16px">
    <RadzenStack Orientation="@Orientation.Horizontal" JustifyContent="@JustifyContent.SpaceBetween">
        <RadzenText Text="Products" TextStyle="@TextStyle.H4" />
        <RadzenButton Text="Add Product" Icon="add" Click="@OpenAddDialog" />
    </RadzenStack>

    <RadzenDataGrid @ref="grid" Data="@products" TItem="Product">
        <Columns>
            <RadzenDataGridColumn TItem="Product" Property="Name" Title="Name" />
            <RadzenDataGridColumn TItem="Product" Property="Price" Title="Price" FormatString="{0:C}" />
            <RadzenDataGridColumn TItem="Product" Title="Actions">
                <Template Context="product">
                    <RadzenButton Icon="edit" Size="@ButtonSize.Small" Click="@(_ => Edit(product))" />
                    <RadzenButton Icon="delete" Size="@ButtonSize.Small"
                                ButtonStyle="@ButtonStyle.Danger"
                                Click="@(_ => Delete(product))" />
                </Template>
            </RadzenDataGridColumn>
        </Columns>
    </RadzenDataGrid>
</RadzenStack>
```

### Dashboard with Metrics
```razor
<RadzenRow Gap="20px">
    <RadzenColumn Size="12" Medium="6" Large="3">
        <RadzenCard>
            <RadzenStack Gap="8px">
                <RadzenText Text="Total Revenue" TextStyle="@TextStyle.Subtitle2" />
                <RadzenText Text="@($"${revenue:N2}")" TextStyle="@TextStyle.H4" />
            </RadzenStack>
        </RadzenCard>
    </RadzenColumn>
</RadzenRow>
```

---

## References

- **Component API Reference**: [references/components-quick-ref.md](references/components-quick-ref.md)
- **Setup Patterns**: [references/setup-patterns.md](references/setup-patterns.md)
- **Best Practices**: [references/best-practices.md](references/best-practices.md)
- **Official Docs**: https://blazor.radzen.com/
- **GitHub**: https://github.com/radzenhq/radzen-blazor
- **NuGet**: https://www.nuget.org/packages/Radzen.Blazor


## 11. Theming (added by v1.2.0 plan)

The Cardscape.Web client ships with 12 themes � 5 Radzen
free themes (default, humanistic, material, software,
standard) + their 5 `-dark` siblings + 2 custom Cardscape
Classic variants (Light + Dark). The single source of
truth is `src/Cardscape.Web/Theming/ThemeCatalog.cs`;
the application-layer validator
(`Cardscape.Application.UserPreferences.Commands.UpdateUserPreferencesCommandHandler.ValidThemeNames`)
mirrors the same names and rejects unknown values with 400.

The runtime is the standard Radzen pipeline: the
`AddRadzenCookieThemeService` (wired in `Program.cs:50-54`)
persists the choice in a cookie, and `<RadzenTheme
Theme="@name" CssPath="@path" />` in `App.razor` emits
the matching `<link>`. The 10 free themes use the default
Radzen CSS path (no CssPath needed); the 2 custom
variants use `CssPath="/css/cardscape-classic*.css"` and
the brand-colour override files in `wwwroot/css/`.

When touching any of the following, the v1.2.0 plan
([docs/roadmap/06-plan-radzen-themes.md](../../docs/roadmap/06-plan-radzen-themes.md))
and ADR 0011 are the source of truth:

- The 12-entry catalog (`ThemeCatalog.All`).
- The 2 brand CSS files (`cardscape-classic.css`,
  `cardscape-classic-dark.css`).
- The `UserPreferencesService` (singleton, Web-side).
- The `UserPreferences` aggregate (Domain).
- The `GET / POST / PUT /api/users/me/preferences` endpoints.
- The `AppearanceToggle` and `/settings/appearance` UI.

Adding a 13th theme is a coordinated change: add the
name to `ThemeCatalog.All`, add it to
`UpdateUserPreferencesCommandHandler.ValidThemeNames`,
add a `Theme POCO` factory if it's custom, and (if
custom) add a `wwwroot/css/cardscape-{name}.css`. The
`ThemeCatalogTests` will fail loudly if the Web side
and the Application side drift apart.