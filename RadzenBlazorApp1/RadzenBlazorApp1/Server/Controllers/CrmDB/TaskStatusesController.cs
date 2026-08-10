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
    [Route("odata/CrmDB/TaskStatuses")]
    public partial class TaskStatusesController : ODataController
    {
        private CrmDBContext context;

        public TaskStatusesController(CrmDBContext context)
        {
            this.context = context;
        }

    
        [HttpGet]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IEnumerable<CrmTaskStatus> GetTaskStatuses()
        {
            var items = this.context.TaskStatuses.AsQueryable<CrmTaskStatus>();
            this.OnTaskStatusesRead(ref items);

            return items;
        }

        partial void OnTaskStatusesRead(ref IQueryable<CrmTaskStatus> items);

        partial void OnTaskStatusGet(ref SingleResult<CrmTaskStatus> item);

        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        [HttpGet("/odata/CrmDB/TaskStatuses(Id={Id})")]
        public SingleResult<CrmTaskStatus> GetCrmTaskStatus(int key)
        {
            var items = this.context.TaskStatuses.Where(i => i.Id == key);
            var result = SingleResult.Create(items);

            OnTaskStatusGet(ref result);

            return result;
        }
        partial void OnTaskStatusDeleted(CrmTaskStatus item);
        partial void OnAfterTaskStatusDeleted(CrmTaskStatus item);

        [HttpDelete("/odata/CrmDB/TaskStatuses(Id={Id})")]
        public IActionResult DeleteCrmTaskStatus(int key)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }


                var item = this.context.TaskStatuses
                    .Where(i => i.Id == key)
                    .FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                this.OnTaskStatusDeleted(item);
                this.context.TaskStatuses.Remove(item);
                this.context.SaveChanges();
                this.OnAfterTaskStatusDeleted(item);

                return new NoContentResult();

            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnTaskStatusUpdated(CrmTaskStatus item);
        partial void OnAfterTaskStatusUpdated(CrmTaskStatus item);

        [HttpPut("/odata/CrmDB/TaskStatuses(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PutCrmTaskStatus(int key, [FromBody]CrmTaskStatus item)
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
                this.OnTaskStatusUpdated(item);
                this.context.TaskStatuses.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.TaskStatuses.Where(i => i.Id == key);
                ;
                this.OnAfterTaskStatusUpdated(item);
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        [HttpPatch("/odata/CrmDB/TaskStatuses(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PatchCrmTaskStatus(int key, [FromBody]Delta<CrmTaskStatus> patch)
        {
            try
            {
                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var item = this.context.TaskStatuses.Where(i => i.Id == key).FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                patch.Patch(item);

                this.OnTaskStatusUpdated(item);
                this.context.TaskStatuses.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.TaskStatuses.Where(i => i.Id == key);
                ;
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnTaskStatusCreated(CrmTaskStatus item);
        partial void OnAfterTaskStatusCreated(CrmTaskStatus item);

        [HttpPost]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult Post([FromBody] CrmTaskStatus item)
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

                this.OnTaskStatusCreated(item);
                this.context.TaskStatuses.Add(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.TaskStatuses.Where(i => i.Id == item.Id);

                ;

                this.OnAfterTaskStatusCreated(item);

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
