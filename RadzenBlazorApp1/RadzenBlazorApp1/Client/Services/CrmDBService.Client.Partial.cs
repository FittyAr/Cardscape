using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Radzen;
using RadzenBlazorApp1.Server.Models;
using RadzenBlazorApp1.Server.Models.CrmDB;

namespace RadzenBlazorApp1.Client
{
    public partial class CrmDBService
    {
        string baseUrl = "api/servermethods";
        public async Task<MonthlyStats> GetMonthlyStats()
        {
            var url = $"{navigationManager.BaseUri}{baseUrl}/getmonthlystats";

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<MonthlyStats>(response);
        }

        public async Task<IEnumerable<RevenueByCompany>> GetRevenueByCompany()
        {
            var url = $"{navigationManager.BaseUri}{baseUrl}/getrevenuebycompany";

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<IEnumerable<RevenueByCompany>>(response);
        }

        public async Task<IEnumerable<RevenueByEmployee>> GetRevenueByEmployee()
        {
            var url = $"{navigationManager.BaseUri}{baseUrl}/getrevenuebyemployee";

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<IEnumerable<RevenueByEmployee>>(response);
        }

        public async Task<IEnumerable<RevenueByMonth>> GetRevenueByMonth()
        {
            var url = $"{navigationManager.BaseUri}{baseUrl}/getrevenuebymonth";

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

            return await Radzen.HttpResponseMessageExtensions.ReadAsync<IEnumerable<RevenueByMonth>>(response);
        }
    }
}