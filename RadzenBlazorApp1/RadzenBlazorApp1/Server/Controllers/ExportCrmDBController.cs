using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Data;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Reflection;
using Microsoft.AspNetCore.Http;

using RadzenBlazorApp1.Server.Data;
using RadzenBlazorApp1.Server.Models;
using RadzenBlazorApp1.Server.Models.CrmDB;
namespace RadzenBlazorApp1.Server.Controllers
{
    public partial class ExportCrmDBController : ExportController
    {
        private readonly CrmDBContext context;
        private readonly CrmDBService service;

        public ExportCrmDBController(CrmDBContext context, CrmDBService service)
        {
            this.service = service;
            this.context = context;
        }

        [HttpGet("/export/CrmDB/contacts/csv")]
        [HttpGet("/export/CrmDB/contacts/csv(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportContactsToCSV(string fileName = null)
        {
            return ToCSV(ApplyQuery(await service.GetContacts(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/contacts/excel")]
        [HttpGet("/export/CrmDB/contacts/excel(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportContactsToExcel(string fileName = null)
        {
            return ToExcel(ApplyQuery(await service.GetContacts(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/opportunities/csv")]
        [HttpGet("/export/CrmDB/opportunities/csv(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportOpportunitiesToCSV(string fileName = null)
        {
            return ToCSV(ApplyQuery(await service.GetOpportunities(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/opportunities/excel")]
        [HttpGet("/export/CrmDB/opportunities/excel(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportOpportunitiesToExcel(string fileName = null)
        {
            return ToExcel(ApplyQuery(await service.GetOpportunities(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/opportunitystatuses/csv")]
        [HttpGet("/export/CrmDB/opportunitystatuses/csv(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportOpportunityStatusesToCSV(string fileName = null)
        {
            return ToCSV(ApplyQuery(await service.GetOpportunityStatuses(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/opportunitystatuses/excel")]
        [HttpGet("/export/CrmDB/opportunitystatuses/excel(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportOpportunityStatusesToExcel(string fileName = null)
        {
            return ToExcel(ApplyQuery(await service.GetOpportunityStatuses(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/tasks/csv")]
        [HttpGet("/export/CrmDB/tasks/csv(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportTasksToCSV(string fileName = null)
        {
            return ToCSV(ApplyQuery(await service.GetTasks(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/tasks/excel")]
        [HttpGet("/export/CrmDB/tasks/excel(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportTasksToExcel(string fileName = null)
        {
            return ToExcel(ApplyQuery(await service.GetTasks(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/taskstatuses/csv")]
        [HttpGet("/export/CrmDB/taskstatuses/csv(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportTaskStatusesToCSV(string fileName = null)
        {
            return ToCSV(ApplyQuery(await service.GetTaskStatuses(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/taskstatuses/excel")]
        [HttpGet("/export/CrmDB/taskstatuses/excel(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportTaskStatusesToExcel(string fileName = null)
        {
            return ToExcel(ApplyQuery(await service.GetTaskStatuses(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/tasktypes/csv")]
        [HttpGet("/export/CrmDB/tasktypes/csv(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportTaskTypesToCSV(string fileName = null)
        {
            return ToCSV(ApplyQuery(await service.GetTaskTypes(), Request.Query), fileName);
        }

        [HttpGet("/export/CrmDB/tasktypes/excel")]
        [HttpGet("/export/CrmDB/tasktypes/excel(fileName='{fileName}')")]
        public async Task<FileStreamResult> ExportTaskTypesToExcel(string fileName = null)
        {
            return ToExcel(ApplyQuery(await service.GetTaskTypes(), Request.Query), fileName);
        }
    }
}
