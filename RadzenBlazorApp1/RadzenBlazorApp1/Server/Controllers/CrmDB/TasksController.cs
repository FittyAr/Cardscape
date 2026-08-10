using System;
using System.Net;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using RadzenBlazorApp1.Server.Data;
using RadzenBlazorApp1.Server.Models;
using RadzenBlazorApp1.Server.Models.CrmDB;
namespace RadzenBlazorApp1.Server.Controllers.CrmDB
{
    [Route("odata/CrmDB/Tasks")]
    public partial class TasksController : ODataController
    {
        private CrmDBContext context;

        public TasksController(CrmDBContext context)
        {
            this.context = context;
        }

    
        [HttpGet]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IEnumerable<CrmTask> GetTasks()
        {
            var items = this.context.Tasks.AsQueryable<CrmTask>();
            this.OnTasksRead(ref items);

            return items;
        }

        partial void OnTasksRead(ref IQueryable<CrmTask> items);

        partial void OnTaskGet(ref SingleResult<CrmTask> item);

        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        [HttpGet("/odata/CrmDB/Tasks(Id={Id})")]
        public SingleResult<CrmTask> GetCrmTask(int key)
        {
            var items = this.context.Tasks.Where(i => i.Id == key);
            var result = SingleResult.Create(items);

            OnTaskGet(ref result);

            return result;
        }
        partial void OnTaskDeleted(CrmTask item);
        partial void OnAfterTaskDeleted(CrmTask item);

        [HttpDelete("/odata/CrmDB/Tasks(Id={Id})")]
        public IActionResult DeleteCrmTask(int key)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }


                var item = this.context.Tasks
                    .Where(i => i.Id == key)
                    .FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                this.OnTaskDeleted(item);
                this.context.Tasks.Remove(item);
                this.context.SaveChanges();
                this.OnAfterTaskDeleted(item);

                return new NoContentResult();

            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnTaskUpdated(CrmTask item);
        partial void OnAfterTaskUpdated(CrmTask item);

        [HttpPut("/odata/CrmDB/Tasks(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PutCrmTask(int key, [FromBody]CrmTask item)
        {
            try
            {
                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (item == null || (item.Id != key))
                {
                    return BadRequest();
                }
                this.OnTaskUpdated(item);
                this.context.Tasks.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.Tasks.Where(i => i.Id == key);
                Request.QueryString = Request.QueryString.Add("$expand", "Opportunity,TaskStatus,TaskType");
                this.OnAfterTaskUpdated(item);
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        [HttpPatch("/odata/CrmDB/Tasks(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PatchCrmTask(int key, [FromBody]Delta<CrmTask> patch)
        {
            try
            {
                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var item = this.context.Tasks.Where(i => i.Id == key).FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                patch.Patch(item);

                this.OnTaskUpdated(item);
                this.context.Tasks.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.Tasks.Where(i => i.Id == key);
                Request.QueryString = Request.QueryString.Add("$expand", "Opportunity,TaskStatus,TaskType");
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnTaskCreated(CrmTask item);
        partial void OnAfterTaskCreated(CrmTask item);

        [HttpPost]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult Post([FromBody] CrmTask item)
        {
            try
            {
                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (item == null)
                {
                    return BadRequest();
                }

                this.OnTaskCreated(item);
                this.context.Tasks.Add(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.Tasks.Where(i => i.Id == item.Id);

                Request.QueryString = Request.QueryString.Add("$expand", "Opportunity,TaskStatus,TaskType");

                this.OnAfterTaskCreated(item);

                return new ObjectResult(SingleResult.Create(itemToReturn))
                {
                    StatusCode = 201
                };
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }
    }
}
