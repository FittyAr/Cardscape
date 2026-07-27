# Radzen Components Quick Reference

## RadzenDataGrid

### Key Parameters
- `Data` - IEnumerable<T> data source
- `TItem` - Type of data item
- `AllowSorting` - Enable column sorting (default: false)
- `AllowFiltering` - Enable filtering (default: false)
- `AllowPaging` - Enable pagination (default: false)
- `PageSize` - Items per page
- `SelectionMode` - DataGridSelectionMode (Single/Multiple)
- `EditMode` - DataGridEditMode (Single/Multiple)
- `@bind-Value` - Selected items (requires SelectionMode)
- `RowUpdate` - EventCallback for row update
- `RowCreate` - EventCallback for row create
- `LoadData` - EventCallback for server-side data loading

### Column Configuration
```razor
<RadzenDataGridColumn TItem="Product"
                     Property="Name"              @* Property to display *@
                     Title="Product Name"         @* Column header *@
                     FormatString="{0:C}"         @* Format (C=currency, N2=decimal) *@
                     Width="150px"                @* Fixed width *@
                     Sortable="true"              @* Allow sorting *@
                     Filterable="true"            @* Allow filtering *@
                     Editable="true"              @* Allow inline editing *@
                     TextAlign="@TextAlign.Right" @* Text alignment *@
                     Frozen="true"                @* Freeze column *@
                     Visible="true" />            @* Show/hide column *@
```

### Template Columns
```razor
<RadzenDataGridColumn TItem="Product" Title="Custom">
    <Template Context="product">
        @* Custom content with full access to product *@
        <RadzenBadge Text="@product.Status" />
    </Template>
</RadzenDataGridColumn>

<RadzenDataGridColumn TItem="Product" Title="Edit">
    <EditTemplate Context="product">
        @* Custom edit control *@
        <RadzenDropDown @bind-Value="@product.CategoryId" Data="@categories" />
    </EditTemplate>
</RadzenDataGridColumn>
```

### Filtering
```razor
<RadzenDataGridColumn TItem="Product" Property="Name" Title="Name">
    <FilterTemplate>
        <RadzenTextBox @bind-Value="@nameFilter" Change="@ApplyFilter" />
    </FilterTemplate>
</RadzenDataGridColumn>
```

### Server-Side Data Loading
```razor
<RadzenDataGrid @ref="grid"
                Data="@products"
                Count="@totalCount"
                TItem="Product"
                LoadData="@LoadData"
                AllowPaging="true"
                PageSize="20">
</RadzenDataGrid>

@code {
    int totalCount;
    IEnumerable<Product> products;

    async Task LoadData(LoadDataArgs args) {
        // args.Skip, args.Top, args.OrderBy, args.Filter
        var result = await GetProductsAsync(args);
        products = result.Items;
        totalCount = result.TotalCount;
    }
}
```

### Grouping
```razor
<RadzenDataGrid Data="@products" TItem="Product" AllowGrouping="true">
    <GroupHeaderTemplate>
        @context.GroupDescriptor.Property: @context.Data.Key (Count: @context.Data.Count)
    </GroupHeaderTemplate>
</RadzenDataGrid>
```

---

## RadzenStack

### Parameters
- `Orientation` - Horizontal | Vertical (default: Vertical)
- `Gap` - Spacing between items ("8px", "1rem", etc.)
- `AlignItems` - Start | Center | End | Stretch | Baseline
- `JustifyContent` - Start | Center | End | SpaceBetween | SpaceAround | SpaceEvenly
- `Wrap` - FlexWrap behavior (NoWrap | Wrap | WrapReverse)
- `Reverse` - Reverse item order

### Examples
```razor
@* Vertical with gap *@
<RadzenStack Gap="20px">
    <RadzenCard>Item 1</RadzenCard>
    <RadzenCard>Item 2</RadzenCard>
</RadzenStack>

@* Horizontal with space between *@
<RadzenStack Orientation="@Orientation.Horizontal"
             JustifyContent="@JustifyContent.SpaceBetween"
             AlignItems="@AlignItems.Center">
    <RadzenText Text="Left" />
    <RadzenButton Text="Right" />
</RadzenStack>

@* Wrap items *@
<RadzenStack Orientation="@Orientation.Horizontal"
             Wrap="@FlexWrap.Wrap"
             Gap="8px">
    <RadzenBadge Text="Tag 1" />
    <RadzenBadge Text="Tag 2" />
    <RadzenBadge Text="Tag 3" />
</RadzenStack>
```

---

## RadzenRow & RadzenColumn

### Column Size Parameters
- `Size` - 1-12 (extra small, all screens)
- `Small` - 1-12 (≥576px)
- `Medium` - 1-12 (≥768px)
- `Large` - 1-12 (≥992px)
- `ExtraLarge` - 1-12 (≥1200px)

### Examples
```razor
<RadzenRow Gap="20px">
    @* Full width on mobile, half on medium, quarter on large *@
    <RadzenColumn Size="12" Medium="6" Large="3">
        <RadzenCard>Content</RadzenCard>
    </RadzenColumn>

    @* Full width on mobile, half on medium, three-quarters on large *@
    <RadzenColumn Size="12" Medium="6" Large="9">
        <RadzenCard>Content</RadzenCard>
    </RadzenColumn>
</RadzenRow>

@* Nested rows *@
<RadzenRow>
    <RadzenColumn Size="12" Large="6">
        <RadzenRow Gap="10px">
            <RadzenColumn Size="6">
                <RadzenCard>Nested 1</RadzenCard>
            </RadzenColumn>
            <RadzenColumn Size="6">
                <RadzenCard>Nested 2</RadzenCard>
            </RadzenColumn>
        </RadzenRow>
    </RadzenColumn>
</RadzenRow>
```

---

## RadzenText

### Parameters
- `Text` - Text content (alternative to child content)
- `TextStyle` - Typography variant
  - `H1`, `H2`, `H3`, `H4`, `H5`, `H6`
  - `Subtitle1`, `Subtitle2`
  - `Body1`, `Body2`
  - `Caption`, `Overline`
- `TagName` - HTML tag (H1-H6, P, Span, Div)
- `Style` - Inline CSS

### Examples
```razor
<RadzenText Text="Page Title" TextStyle="@TextStyle.H3" TagName="@TagName.H1" />
<RadzenText Text="Subtitle" TextStyle="@TextStyle.Subtitle1" />
<RadzenText Text="Body text" TextStyle="@TextStyle.Body1" />
<RadzenText Text="@($"${revenue:N2}")" TextStyle="@TextStyle.H4" />
<RadzenText TextStyle="@TextStyle.Caption" Style="color: var(--rz-danger);">
    @errorMessage
</RadzenText>
```

---

## RadzenButton

### Parameters
- `Text` - Button text
- `Icon` - Material icon name (e.g., "save", "delete", "edit")
- `Click` - EventCallback for click event
- `ButtonType` - Submit | Button | Reset (default: Button)
- `ButtonStyle` - Primary | Secondary | Success | Danger | Warning | Info | Light | Dark
- `Size` - Small | Medium | Large
- `Disabled` - bool
- `BusyText` - Text shown when IsBusy=true
- `IsBusy` - Shows loading indicator

### Examples
```razor
<RadzenButton Text="Save"
              Icon="save"
              Click="@OnSave"
              ButtonStyle="@ButtonStyle.Primary" />

<RadzenButton Icon="delete"
              Size="@ButtonSize.Small"
              ButtonStyle="@ButtonStyle.Danger"
              Click="@OnDelete" />

<RadzenButton Text="Processing..."
              IsBusy="@isProcessing"
              BusyText="Loading..."
              Click="@ProcessAsync" />
```

---

## RadzenDropDown

### Parameters
- `Data` - IEnumerable data source
- `@bind-Value` - Selected value
- `TextProperty` - Property name for display text
- `ValueProperty` - Property name for value
- `AllowClear` - Show clear button
- `AllowFiltering` - Enable search/filter
- `Placeholder` - Placeholder text
- `Multiple` - Allow multiple selection
- `Chips` - Show selected items as chips (with Multiple)
- `Change` - EventCallback when selection changes

### Examples
```razor
@* Simple dropdown *@
<RadzenDropDown Data="@countries"
                @bind-Value="@selectedCountryId"
                TextProperty="Name"
                ValueProperty="Id"
                Placeholder="Select country" />

@* With filtering *@
<RadzenDropDown Data="@cities"
                @bind-Value="@selectedCity"
                TextProperty="Name"
                ValueProperty="Id"
                AllowFiltering="true"
                AllowClear="true" />

@* Multiple selection *@
<RadzenDropDown Data="@tags"
                @bind-Value="@selectedTags"
                Multiple="true"
                Chips="true"
                Placeholder="Select tags" />

@* Custom template *@
<RadzenDropDown Data="@users" @bind-Value="@selectedUserId">
    <Template Context="user">
        <div style="display: flex; align-items: center;">
            <RadzenImage Path="@user.Avatar" Style="width: 24px; height: 24px; border-radius: 50%; margin-right: 8px;" />
            <span>@user.Name</span>
        </div>
    </Template>
</RadzenDropDown>
```

---

## RadzenAlert

### Parameters
- `AlertStyle` - Danger | Warning | Info | Success
- `Variant` - Filled | Flat | Outlined | Text
- `Shade` - Default | Lighter | Darker | Light | Dark
- `Size` - Small | Medium | Large
- `AllowClose` - Show close button
- `Visible` - Control visibility
- `Icon` - Custom icon

### Examples
```razor
<RadzenAlert AlertStyle="@AlertStyle.Danger"
             Variant="@Variant.Flat"
             Shade="@Shade.Lighter">
    <RadzenText TextStyle="@TextStyle.Subtitle2" TagName="@TagName.H4">Error</RadzenText>
    <RadzenText>@errorMessage</RadzenText>
</RadzenAlert>

<RadzenAlert AlertStyle="@AlertStyle.Success"
             Variant="@Variant.Filled"
             AllowClose="true"
             Visible="@showSuccess">
    Operation completed!
</RadzenAlert>

<RadzenAlert AlertStyle="@AlertStyle.Warning"
             Icon="warning"
             Size="@AlertSize.Small">
    <RadzenText TextStyle="@TextStyle.Caption">@warningText</RadzenText>
</RadzenAlert>
```

---

## RadzenNumeric<T>

### Parameters
- `@bind-Value` - Numeric value (int, decimal, double, etc.)
- `TValue` - Type parameter (int, decimal, long, etc.)
- `Min` - Minimum value
- `Max` - Maximum value
- `Step` - Increment step
- `Format` - Number format ("c" for currency, "n2" for 2 decimals)
- `ShowUpDown` - Show increment/decrement buttons

### Examples
```razor
<RadzenNumeric @bind-Value="@quantity" TValue="int" Min="0" Max="100" />

<RadzenNumeric @bind-Value="@price" TValue="decimal" Format="c" Step="0.01M" />

<RadzenNumeric @bind-Value="@percentage" TValue="double" Min="0" Max="100" Format="n2" />
```

---

## RadzenDatePicker

### Parameters
- `@bind-Value` - DateTime value
- `DateFormat` - Display format ("MM/dd/yyyy", "yyyy-MM-dd", etc.)
- `ShowTime` - Enable time selection
- `TimeOnly` - Show only time picker
- `ShowCalendarWeek` - Show week numbers
- `Min` - Minimum date
- `Max` - Maximum date
- `Inline` - Display inline (no dropdown)

### Examples
```razor
<RadzenDatePicker @bind-Value="@startDate" DateFormat="MM/dd/yyyy" />

<RadzenDatePicker @bind-Value="@appointmentTime"
                  DateFormat="MM/dd/yyyy HH:mm"
                  ShowTime="true" />

<RadzenDatePicker @bind-Value="@selectedTime"
                  TimeOnly="true"
                  DateFormat="HH:mm" />
```

---

## RadzenDialog (via DialogService)

### DialogOptions
- `Width` - Dialog width ("700px", "90%")
- `Height` - Dialog height ("512px", "auto")
- `Resizable` - Allow resizing
- `Draggable` - Allow dragging
- `CloseDialogOnOverlayClick` - Close on backdrop click
- `CloseDialogOnEsc` - Close on Escape key
- `ShowTitle` - Show title bar
- `ShowClose` - Show close button

### Usage Pattern
```razor
@inject DialogService DialogService

@code {
    async Task OpenDialog() {
        var result = await DialogService.OpenAsync<MyDialogComponent>(
            "Dialog Title",
            new Dictionary<string, object>() {
                { "Parameter1", value1 },
                { "Parameter2", value2 }
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

    // Confirm dialog shortcut
    async Task ConfirmDelete() {
        var confirmed = await DialogService.Confirm(
            "Are you sure?",
            "Delete Item",
            new ConfirmOptions() { OkButtonText = "Yes", CancelButtonText = "No" }
        );

        if (confirmed == true) {
            // Delete
        }
    }
}
```

---

## RadzenNotification (via NotificationService)

### NotificationMessage Properties
- `Severity` - Success | Info | Warning | Error
- `Summary` - Title text
- `Detail` - Message text
- `Duration` - Display duration in milliseconds (default: 3000)
- `CloseOnClick` - Close when clicked
- `Style` - Custom CSS

### Usage Pattern
```razor
@inject NotificationService NotificationService

@code {
    void NotifySuccess(string message) {
        NotificationService.Notify(new NotificationMessage {
            Severity = NotificationSeverity.Success,
            Summary = "Success",
            Detail = message,
            Duration = 4000
        });
    }

    void NotifyError(string message) {
        NotificationService.Notify(new NotificationMessage {
            Severity = NotificationSeverity.Error,
            Summary = "Error",
            Detail = message,
            Duration = 8000,
            CloseOnClick = true
        });
    }

    void NotifyWarning(string message) {
        NotificationService.Notify(new NotificationMessage {
            Severity = NotificationSeverity.Warning,
            Summary = "Warning",
            Detail = message,
            Duration = 6000
        });
    }
}
```

---

## RadzenPanelMenu

### Parameters
- `DisplayStyle` - Icon | Text | IconAndText
- `Multiple` - Allow multiple expanded sections

### RadzenPanelMenuItem Parameters
- `Text` - Menu item text
- `Icon` - Material icon name
- `Path` - Navigation path (automatically navigates)
- `Expanded` - Initially expanded
- `Click` - EventCallback for click

### Examples
```razor
<RadzenPanelMenu DisplayStyle="@MenuItemDisplayStyle.IconAndText" Multiple="false">
    <RadzenPanelMenuItem Text="Home" Icon="home" Path="/" />

    <RadzenPanelMenuItem Text="Admin" Icon="admin_panel_settings" Expanded="true">
        <RadzenPanelMenuItem Text="Users" Icon="people" Path="/admin/users" />
        <RadzenPanelMenuItem Text="Settings" Icon="settings" Path="/admin/settings" />
    </RadzenPanelMenuItem>

    <RadzenPanelMenuItem Text="Custom Action" Icon="play_arrow" Click="@OnCustomAction" />
</RadzenPanelMenu>
```

---

## RadzenTextBox

### Parameters
- `@bind-Value` - Text value
- `Placeholder` - Placeholder text
- `MaxLength` - Maximum length
- `ReadOnly` - Read-only mode
- `Disabled` - Disabled state
- `Change` - EventCallback on value change
- `Input` - EventCallback on input event

### Examples
```razor
<RadzenTextBox @bind-Value="@model.Name"
               Placeholder="Enter name"
               MaxLength="100" />

<RadzenTextBox Value="@searchText"
               Change="@OnSearchChanged"
               Placeholder="Search..." />
```

---

## RadzenFieldset

### Parameters
- `Text` - Legend text
- `AllowCollapse` - Enable collapse functionality
- `Collapsed` - Initial collapsed state

### Examples
```razor
<RadzenFieldset Text="User Information">
    <RadzenStack Gap="12px">
        <RadzenLabel Text="Name" />
        <RadzenTextBox @bind-Value="@name" />
    </RadzenStack>
</RadzenFieldset>

<RadzenFieldset Text="Advanced Options" AllowCollapse="true" Collapsed="true">
    <RadzenStack Gap="12px">
        @* Advanced fields *@
    </RadzenStack>
</RadzenFieldset>
```

---

## Material Icons Reference

Common icons used with Radzen components:

**Actions:**
- `add`, `edit`, `delete`, `save`, `cancel`, `refresh`, `search`, `filter_list`
- `visibility`, `visibility_off`, `more_vert`, `more_horiz`

**Navigation:**
- `home`, `menu`, `arrow_back`, `arrow_forward`, `close`, `chevron_left`, `chevron_right`

**Content:**
- `file_copy`, `content_copy`, `content_paste`, `attachment`, `download`, `upload`

**Status:**
- `check`, `check_circle`, `error`, `warning`, `info`, `help`

**Data:**
- `table_chart`, `dashboard`, `analytics`, `pie_chart`, `bar_chart`

**Users:**
- `person`, `people`, `account_circle`, `admin_panel_settings`

**Commerce:**
- `shopping_cart`, `inventory`, `category`, `store`, `payment`

**Communication:**
- `email`, `phone`, `chat`, `notifications`, `message`

Full icon list: https://fonts.google.com/icons
