using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

using RadzenBlazorApp1.Server.Models;
using RadzenBlazorApp1.Server.Models.CrmDB;

namespace RadzenBlazorApp1.Client.Pages
{
    public partial class TaskTypes
    {
        [Inject]
        protected IJSRuntime JSRuntime { get; set; }

        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        [Inject]
        protected DialogService DialogService { get; set; }

        [Inject]
        protected TooltipService TooltipService { get; set; }

        [Inject]
        protected ContextMenuService ContextMenuService { get; set; }

        [Inject]
        protected NotificationService NotificationService { get; set; }

        [Inject]
        public CrmDBService CrmDBService { get; set; }

        protected RadzenDataGrid<TaskType> grid0;
        protected int count;

        protected string search = "";

        [Inject]
        protected SecurityService Security { get; set; }

        protected async Task Search(ChangeEventArgs args)
        {
            search = $"{args.Value}";

            await grid0.GoToPage(0);

            await grid0.Reload();
        }

        protected IEnumerable<TaskType> taskTypes;
        protected IEnumerable<CompositeFilterDescriptor> filters;

        protected async Task Grid0LoadData(LoadDataArgs args)
        {
            filters = new CompositeFilterDescriptor[]
                {
                    new CompositeFilterDescriptor()
                    {
                        LogicalFilterOperator = LogicalFilterOperator.Or,
                        Filters = (!string.IsNullOrEmpty(search) ? grid0.ColumnsCollection
                                .Where(c => c.FilterPropertyType == typeof(string))
                                .Select(c => new CompositeFilterDescriptor()
                                {
                                    Property = c.Property,
                                    FilterOperator = FilterOperator.Contains,
                                    FilterValue = search
                                }) : Enumerable.Empty<CompositeFilterDescriptor>())
                    },
                    new CompositeFilterDescriptor()
                    {
                        LogicalFilterOperator = LogicalFilterOperator.Or,
                        Filters = args.Filters.Select(f => new CompositeFilterDescriptor()
                                {
                                    LogicalFilterOperator = f.LogicalFilterOperator,
                                    Filters = new CompositeFilterDescriptor[]
                                    {
                                        new CompositeFilterDescriptor()
                                        {
                                            Property = f.Property,
                                            FilterOperator = f.FilterOperator,
                                            FilterValue = f.FilterValue
                                        },
                                        new CompositeFilterDescriptor()
                                        {
                                            Property = f.Property,
                                            FilterOperator = f.SecondFilterOperator,
                                            FilterValue = f.SecondFilterValue
                                        }
                                    }
                                })
                    }
                };

            try
            {

                var result = await CrmDBService.GetTaskTypes(filter: filters.ToODataFilterString<TaskType>(LogicalFilterOperator.And, grid0.FilterCaseSensitivity), orderby: $"{args.OrderBy}", top: args.Top, skip: args.Skip, count:args.Top != null && args.Skip != null);
                taskTypes = result.Value.AsODataEnumerable();
                count = result.Count;
                }
            catch (System.Exception ex)
            {
                NotificationService.Notify(new NotificationMessage(){ Severity = NotificationSeverity.Error, Summary = $"Error", Detail = $"Unable to load TaskTypes" });
            }
        }    

        protected async Task AddButtonClick(MouseEventArgs args)
        {
            await DialogService.OpenAsync<AddTaskType>("Add TaskType", null);
            await grid0.Reload();
        }

        protected async Task EditRow(TaskType args)
        {
            await DialogService.OpenAsync<EditTaskType>("Edit TaskType", new Dictionary<string, object> { {"Id", args.Id} });
            await grid0.Reload();
        }

        protected async Task GridDeleteButtonClick(MouseEventArgs args, TaskType item)
        {
            try
            {
                if (await DialogService.Confirm("Are you sure you want to delete this record?") == true)
                {
                    var deleteResult = await CrmDBService.DeleteTaskType(id:item.Id);

                    if (deleteResult != null)
                    {
                        await grid0.Reload();
                    }
                }
            }
            catch (Exception ex)
            {
                NotificationService.Notify(new NotificationMessage
                { 
                    Severity = NotificationSeverity.Error,
                    Summary = $"Error", 
                    Detail = $"Unable to delete TaskType" 
                });
            }
        }

        protected async Task ExportClick(RadzenSplitButtonItem args)
        {
            if (args?.Value == "csv")
            {
                await CrmDBService.ExportTaskTypesToCSV(new Query
                { 
                    Filter = filters.ToFilterString<TaskType>(LogicalFilterOperator.And, grid0.FilterCaseSensitivity), 
                    OrderBy = $"{grid0.Query.OrderBy}", 
                    Expand = "", 
                    Select = string.Join(",", grid0.ColumnsCollection.Where(c => c.GetVisible() && !string.IsNullOrEmpty(c.Property)).Select(c => c.Property.Contains(".") ? c.Property + " as " + c.Property.Replace(".", "") : c.Property))
                }, "TaskTypes");
            }

            if (args == null || args.Value == "xlsx")
            {
                await CrmDBService.ExportTaskTypesToExcel(new Query
                { 
                    Filter = filters.ToFilterString<TaskType>(LogicalFilterOperator.And, grid0.FilterCaseSensitivity), 
                    OrderBy = $"{grid0.Query.OrderBy}", 
                    Expand = "", 
                    Select = string.Join(",", grid0.ColumnsCollection.Where(c => c.GetVisible() && !string.IsNullOrEmpty(c.Property)).Select(c => c.Property.Contains(".") ? c.Property + " as " + c.Property.Replace(".", "") : c.Property))
                }, "TaskTypes");
            }
        }
    }
}