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
    public partial class EditTask
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

        [Parameter]
        public int Id { get; set; }

        [Inject]
        protected SecurityService Security { get; set; }

        [Inject]
        public CrmDBService CrmDBService { get; set; }

        protected override async Task OnInitializedAsync()
        {
            task = await CrmDBService.GetTaskById(id:Id);
        }

        protected bool errorVisible;
        protected CrmTask task;

        protected IEnumerable<Opportunity> opportunitiesForOpportunityId;
        protected IEnumerable<CrmTaskStatus> taskStatusesForStatusId;

        protected int opportunitiesForOpportunityIdCount;
        protected Opportunity opportunitiesForOpportunityIdValue;

        protected async Task opportunitiesForOpportunityIdLoadData(LoadDataArgs args)
        {
            try
            {

                var result = await CrmDBService.GetOpportunities(top: args.Top, skip: args.Skip, count:args.Top != null && args.Skip != null, filter: $"{args.Filter}", orderby: $"{args.OrderBy}");
                opportunitiesForOpportunityId = result.Value.AsODataEnumerable();
                opportunitiesForOpportunityIdCount = result.Count;

                if (!object.Equals(task.OpportunityId, null))
                {
                    var valueResult = await CrmDBService.GetOpportunities(filter: $"Id eq {task.OpportunityId}");
                    var firstItem = valueResult.Value.FirstOrDefault();
                    if (firstItem != null)
                    {
                        opportunitiesForOpportunityIdValue = firstItem;
                    }
                }
                }
            catch (System.Exception ex)
            {
                NotificationService.Notify(new NotificationMessage(){ Severity = NotificationSeverity.Error, Summary = $"Error", Detail = $"Unable to load Contact" });
            }
        }

        protected int taskStatusesForStatusIdCount;
        protected CrmTaskStatus taskStatusesForStatusIdValue;

        protected async Task taskStatusesForStatusIdLoadData(LoadDataArgs args)
        {
            try
            {

                var result = await CrmDBService.GetTaskStatuses(top: args.Top, skip: args.Skip, count:args.Top != null && args.Skip != null, filter: $"{args.Filter}", orderby: $"{args.OrderBy}");
                taskStatusesForStatusId = result.Value.AsODataEnumerable();
                taskStatusesForStatusIdCount = result.Count;

                if (!object.Equals(task.StatusId, null))
                {
                    var valueResult = await CrmDBService.GetTaskStatuses(filter: $"Id eq {task.StatusId}");
                    var firstItem = valueResult.Value.FirstOrDefault();
                    if (firstItem != null)
                    {
                        taskStatusesForStatusIdValue = firstItem;
                    }
                }
                }
            catch (System.Exception ex)
            {
                NotificationService.Notify(new NotificationMessage(){ Severity = NotificationSeverity.Error, Summary = $"Error", Detail = $"Unable to load OpportunityStatus" });
            }
        }

        protected int taskTypesForTypeIdCount;
        protected TaskType taskTypesForTypeIdValue;
        protected IEnumerable<TaskType> taskTypesForTypeId;

        protected async Task taskTypesForTypeIdLoadData(LoadDataArgs args)
        {
            try
            {

                var result = await CrmDBService.GetTaskTypes(top: args.Top, skip: args.Skip, count:args.Top != null && args.Skip != null, filter: $"{args.Filter}", orderby: $"{args.OrderBy}");
                taskTypesForTypeId = result.Value.AsODataEnumerable();
                taskTypesForTypeIdCount = result.Count;

                if (!object.Equals(task.TypeId, null))
                {
                    var valueResult = await CrmDBService.GetTaskTypes(filter: $"Id eq {task.TypeId}");
                    var firstItem = valueResult.Value.FirstOrDefault();
                    if (firstItem != null)
                    {
                        taskTypesForTypeIdValue = firstItem;
                    }
                }
                }
            catch (System.Exception ex)
            {
                NotificationService.Notify(new NotificationMessage(){ Severity = NotificationSeverity.Error, Summary = $"Error", Detail = $"Unable to load OpportunityStatus" });
            }
        }
        
        protected async Task FormSubmit()
        {
            try
            {
                await CrmDBService.UpdateTask(id:Id, task);
                DialogService.Close(task);
            }
            catch (Exception ex)
            {
                errorVisible = true;
            }
        }

        protected async Task CancelButtonClick(MouseEventArgs args)
        {
            DialogService.Close(null);
        }
    }
}