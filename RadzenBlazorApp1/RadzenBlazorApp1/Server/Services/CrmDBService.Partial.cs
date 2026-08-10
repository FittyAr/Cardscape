using Radzen;
using System.Data;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

using RadzenBlazorApp1.Server.Data;
using RadzenBlazorApp1.Server.Models;
using RadzenBlazorApp1.Server.Models.CrmDB;
namespace RadzenBlazorApp1.Server
{
    public partial class CrmDBService
    {
        private readonly ApplicationIdentityDbContext identityDbContext;
        private readonly string userId;

        public CrmDBService(CrmDBContext context,
            NavigationManager navigationManager,
            IHttpContextAccessor httpContextAccessor,
            ApplicationIdentityDbContext identityDbContext)
        {
            this.context = context;
            this.navigationManager = navigationManager;
            this.identityDbContext = identityDbContext;

            userId = httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        partial void OnOpportunitiesRead(ref IQueryable<Opportunity> items)
        {
            var user = identityDbContext.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null && user.Name != "Admin")
            {
                items = items.Where(item => item.UserId == user.Id);
            }

            items = items.Include(item => item.User);
        }

        public async Task<MonthlyStats> GetMonthlyStats()
        {
            double wonOpportunities = context.Opportunities
                    .Include(opportunity => opportunity.OpportunityStatus)
                    .Where(opportunity => opportunity.OpportunityStatus.Name == "Won")
                    .Count();

            var totalOpportunities = context.Opportunities.Count();

            var ratio = wonOpportunities / totalOpportunities;

            var stats = context.Opportunities
                                .Include(opportunity => opportunity.OpportunityStatus)
                                .Where(opportunity => opportunity.OpportunityStatus.Name == "Won")
                                .ToList()
                                .GroupBy(opportunity => new DateTime(opportunity.CloseDate.Year, opportunity.CloseDate.Month, 1))
                                .Select(group => new MonthlyStats()
                                {
                                    Month = group.Key,
                                    Revenue = group.Sum(opportunity => opportunity.Amount),
                                    Opportunities = group.Count(),
                                    AverageDealSize = group.Average(opportunity => opportunity.Amount),
                                    Ratio = ratio
                                })
                                .OrderBy(deals => deals.Month)
                                .LastOrDefault();

            return await Task.FromResult(stats);
        }

        public async Task<IEnumerable<RevenueByCompany>> GetRevenueByCompany()
        {
            var result = context.Opportunities
                                .Include(opportunity => opportunity.Contact)
                                .ToList()
                                .GroupBy(opportunity => opportunity.Contact.Company)
                                .Select(group => new RevenueByCompany() {
                                        Company = group.Key,
                                        Revenue = group.Sum(opportunity => opportunity.Amount)
                                });

            return await Task.FromResult(result);
        }

        public async Task<IEnumerable<RevenueByEmployee>> GetRevenueByEmployee()
        {
             var result = context.Opportunities
                                .Include(opportunity => opportunity.User)
                                .ToList()
                                .GroupBy(opportunity => $"{opportunity.User.FirstName} {opportunity.User.LastName}")
                                .Select(group => new RevenueByEmployee() {
                                        Employee = group.Key,
                                        Revenue = group.Sum(opportunity => opportunity.Amount)
                                });

            return await Task.FromResult(result);
        }

        public async Task<IEnumerable<RevenueByMonth>> GetRevenueByMonth()
        {
             var result = context.Opportunities
                                .Include(opportunity => opportunity.OpportunityStatus)
                                .Where(opportunity => opportunity.OpportunityStatus.Name == "Won")
                                .ToList()
                                .GroupBy(opportunity => new DateTime(opportunity.CloseDate.Year, opportunity.CloseDate.Month, 1))
                                .Select(group => new RevenueByMonth() {
                                    Revenue = group.Sum(opportunity => opportunity.Amount),
                                    Month = group.Key
                                })
                                .OrderBy(deals => deals.Month);

            return await Task.FromResult(result);
        }
    }
}