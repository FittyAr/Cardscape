using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using RadzenBlazorApp1.Server.Data;
using RadzenBlazorApp1.Server.Models;
using RadzenBlazorApp1.Server.Models.CrmDB;
namespace RadzenBlazorApp1.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    public class ServerMethodsController : Controller
    {
        CrmDBService service;

        public ServerMethodsController(CrmDBService service)
        {
            this.service = service;
        }

        public async Task<IActionResult> GetMonthlyStats()
        {
            var result = await service.GetMonthlyStats();

            return Ok(System.Text.Json.JsonSerializer.Serialize(result, 
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null, ReferenceHandler = ReferenceHandler.IgnoreCycles }));
        }

        public async Task<IActionResult> GetRevenueByCompany()
        {
            var result = await service.GetRevenueByCompany();

            return Ok(System.Text.Json.JsonSerializer.Serialize(result,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null, ReferenceHandler = ReferenceHandler.IgnoreCycles }));
        }

        public async Task<IActionResult> GetRevenueByEmployee()
        {
            var result = await service.GetRevenueByEmployee();

            return Ok(System.Text.Json.JsonSerializer.Serialize(result,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null, ReferenceHandler = ReferenceHandler.IgnoreCycles }));
        }

        public async Task<IActionResult> GetRevenueByMonth()
        {
            var result = await service.GetRevenueByMonth();

            return Ok(System.Text.Json.JsonSerializer.Serialize(result,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null, ReferenceHandler = ReferenceHandler.IgnoreCycles }));
        }
    }
}
