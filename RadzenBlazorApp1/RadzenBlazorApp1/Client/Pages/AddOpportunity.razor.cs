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
    public partial class AddOpportunity
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

        [Inject]
        protected SecurityService Security { get; set; }
        
        protected override async Task OnInitializedAsync()
        {
            opportunity = new Opportunity();
        }

        protected bool errorVisible;
        protected Opportunity opportunity;

        protected IEnumerable<Contact> contactsForContactId;

        protected IEnumerable<OpportunityStatus> opportunityStatusesForStatusId;


        protected int contactsForContactIdCount;
        protected Contact contactsForContactIdValue;

        protected async Task contactsForContactIdLoadData(LoadDataArgs args)
        {
            try
            {

                var result = await CrmDBService.GetContacts(top: args.Top, skip: args.Skip, count:args.Top != null && args.Skip != null, filter: $"{args.Filter}", orderby: $"{args.OrderBy}");
                contactsForContactId = result.Value.AsODataEnumerable();
                contactsForContactIdCount = result.Count;

                if (!object.Equals(opportunity.ContactId, null))
                {
                    var valueResult = await CrmDBService.GetContacts(filter: $"Id eq {opportunity.ContactId}");
                    var firstItem = valueResult.Value.FirstOrDefault();
                    if (firstItem != null)
                    {
                        contactsForContactIdValue = firstItem;
                    }
                }
                }
            catch (System.Exception ex)
            {
                NotificationService.Notify(new NotificationMessage(){ Severity = NotificationSeverity.Error, Summary = $"Error", Detail = $"Unable to load Contact" });
            }
        }

        protected int opportunityStatusesForStatusIdCount;
        protected OpportunityStatus opportunityStatusesForStatusIdValue;

        protected async Task opportunityStatusesForStatusIdLoadData(LoadDataArgs args)
        {
            try
            {

                var result = await CrmDBService.GetOpportunityStatuses(top: args.Top, skip: args.Skip, count:args.Top != null && args.Skip != null, filter: $"{args.Filter}", orderby: $"{args.OrderBy}");
                opportunityStatusesForStatusId = result.Value.AsODataEnumerable();
                opportunityStatusesForStatusIdCount = result.Count;

                if (!object.Equals(opportunity.StatusId, null))
                {
                    var valueResult = await CrmDBService.GetOpportunityStatuses(filter: $"Id eq {opportunity.StatusId}");
                    var firstItem = valueResult.Value.FirstOrDefault();
                    if (firstItem != null)
                    {
                        opportunityStatusesForStatusIdValue = firstItem;
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
                opportunity.UserId = Security.User?.Id;
                
                await CrmDBService.CreateOpportunity(opportunity);
                DialogService.Close(opportunity);
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