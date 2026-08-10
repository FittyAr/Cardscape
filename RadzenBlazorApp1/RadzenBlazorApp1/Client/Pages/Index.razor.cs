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
    public partial class Index
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
        protected SecurityService Security { get; set; }

        [Inject]
        protected CrmDBService Service { get; set; }

        MonthlyStats monthlyStats;
        IEnumerable<RevenueByCompany> revenueByCompany;
        IEnumerable<RevenueByMonth> revenueByMonth;
        IEnumerable<RevenueByEmployee> revenueByEmployee;

        IEnumerable<Opportunity> getOpportunitiesResult;
        IEnumerable<CrmTask> getTasksResult;
        int getOpportunitiesResultCount;
        int getTasksResultCount;

        protected override async Task OnInitializedAsync()
        {
            monthlyStats = await Service.GetMonthlyStats();
            revenueByCompany = await Service.GetRevenueByCompany();
            revenueByMonth = await Service.GetRevenueByMonth();
            revenueByEmployee = await Service.GetRevenueByEmployee();
        }

        protected async Task getOpportunitiesResultLoadData(LoadDataArgs args)
        {
            var filters = args.Filters.Select(f => new CompositeFilterDescriptor()
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
                                });
            try
            {

                var result = await Service.GetOpportunities(filter: filters.ToODataFilterString<Opportunity>(LogicalFilterOperator.And, FilterCaseSensitivity.Default), orderby: $"{args.OrderBy}", top: args.Top, skip: args.Skip, count:args.Top != null && args.Skip != null, expand: "Contact,OpportunityStatus");
                getOpportunitiesResult = result.Value.AsODataEnumerable();
                getOpportunitiesResultCount = result.Count;
                }
            catch (Exception)
            {
                NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error", Detail = "Unable to load" });
            }
        }


        protected async Task getTasksResultLoadData(LoadDataArgs args)
        {
             var filters = args.Filters.Select(f => new CompositeFilterDescriptor()
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
                                });
            try
            {

                var result = await Service.GetTasks(filter: filters.ToODataFilterString<CrmTask>(LogicalFilterOperator.And, FilterCaseSensitivity.Default), orderby: $"{args.OrderBy}", top: args.Top, skip: args.Skip, count:args.Top != null && args.Skip != null, expand: "Opportunity($expand=User,Contact),TaskStatus,TaskType");
                getTasksResult = result.Value.AsODataEnumerable();
                getTasksResultCount = result.Count;
                }
            catch (Exception)
            {
                NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error", Detail = "Unable to load" });
            }
        }
    }
}