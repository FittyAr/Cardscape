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
    [Route("odata/CrmDB/TaskTypes")]
    public partial class TaskTypesController : ODataController
    {
        private CrmDBContext context;

        public TaskTypesController(CrmDBContext context)
        {
            this.context = context;
        }

    
        [HttpGet]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IEnumerable<TaskType> GetTaskTypes()
        {
            var items = this.context.TaskTypes.AsQueryable<TaskType>();
            this.OnTaskTypesRead(ref items);

            return items;
        }

        partial void OnTaskTypesRead(ref IQueryable<TaskType> items);

        partial void OnTaskTypeGet(ref SingleResult<TaskType> item);

        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        [HttpGet("/odata/CrmDB/TaskTypes(Id={Id})")]
        public SingleResult<TaskType> GetTaskType(int key)
        {
            var items = this.context.TaskTypes.Where(i => i.Id == key);
            var result = SingleResult.Create(items);

            OnTaskTypeGet(ref result);

            return result;
        }
        partial void OnTaskTypeDeleted(TaskType item);
        partial void OnAfterTaskTypeDeleted(TaskType item);

        [HttpDelete("/odata/CrmDB/TaskTypes(Id={Id})")]
        public IActionResult DeleteTaskType(int key)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }


                var item = this.context.TaskTypes
                    .Where(i => i.Id == key)
                    .FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                this.OnTaskTypeDeleted(item);
                this.context.TaskTypes.Remove(item);
                this.context.SaveChanges();
                this.OnAfterTaskTypeDeleted(item);

                return new NoContentResult();

            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnTaskTypeUpdated(TaskType item);
        partial void OnAfterTaskTypeUpdated(TaskType item);

        [HttpPut("/odata/CrmDB/TaskTypes(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PutTaskType(int key, [FromBody]TaskType item)
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
                this.OnTaskTypeUpdated(item);
                this.context.TaskTypes.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.TaskTypes.Where(i => i.Id == key);
                ;
                this.OnAfterTaskTypeUpdated(item);
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        [HttpPatch("/odata/CrmDB/TaskTypes(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PatchTaskType(int key, [FromBody]Delta<TaskType> patch)
        {
            try
            {
                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var item = this.context.TaskTypes.Where(i => i.Id == key).FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                patch.Patch(item);

                this.OnTaskTypeUpdated(item);
                this.context.TaskTypes.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.TaskTypes.Where(i => i.Id == key);
                ;
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnTaskTypeCreated(TaskType item);
        partial void OnAfterTaskTypeCreated(TaskType item);

        [HttpPost]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult Post([FromBody] TaskType item)
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

                this.OnTaskTypeCreated(item);
                this.context.TaskTypes.Add(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.TaskTypes.Where(i => i.Id == item.Id);

                ;

                this.OnAfterTaskTypeCreated(item);

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
