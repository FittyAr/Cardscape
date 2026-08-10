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
    [Route("odata/CrmDB/OpportunityStatuses")]
    public partial class OpportunityStatusesController : ODataController
    {
        private CrmDBContext context;

        public OpportunityStatusesController(CrmDBContext context)
        {
            this.context = context;
        }

    
        [HttpGet]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IEnumerable<OpportunityStatus> GetOpportunityStatuses()
        {
            var items = this.context.OpportunityStatuses.AsQueryable<OpportunityStatus>();
            this.OnOpportunityStatusesRead(ref items);

            return items;
        }

        partial void OnOpportunityStatusesRead(ref IQueryable<OpportunityStatus> items);

        partial void OnOpportunityStatusGet(ref SingleResult<OpportunityStatus> item);

        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        [HttpGet("/odata/CrmDB/OpportunityStatuses(Id={Id})")]
        public SingleResult<OpportunityStatus> GetOpportunityStatus(int key)
        {
            var items = this.context.OpportunityStatuses.Where(i => i.Id == key);
            var result = SingleResult.Create(items);

            OnOpportunityStatusGet(ref result);

            return result;
        }
        partial void OnOpportunityStatusDeleted(OpportunityStatus item);
        partial void OnAfterOpportunityStatusDeleted(OpportunityStatus item);

        [HttpDelete("/odata/CrmDB/OpportunityStatuses(Id={Id})")]
        public IActionResult DeleteOpportunityStatus(int key)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }


                var item = this.context.OpportunityStatuses
                    .Where(i => i.Id == key)
                    .FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                this.OnOpportunityStatusDeleted(item);
                this.context.OpportunityStatuses.Remove(item);
                this.context.SaveChanges();
                this.OnAfterOpportunityStatusDeleted(item);

                return new NoContentResult();

            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnOpportunityStatusUpdated(OpportunityStatus item);
        partial void OnAfterOpportunityStatusUpdated(OpportunityStatus item);

        [HttpPut("/odata/CrmDB/OpportunityStatuses(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PutOpportunityStatus(int key, [FromBody]OpportunityStatus item)
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
                this.OnOpportunityStatusUpdated(item);
                this.context.OpportunityStatuses.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.OpportunityStatuses.Where(i => i.Id == key);
                ;
                this.OnAfterOpportunityStatusUpdated(item);
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        [HttpPatch("/odata/CrmDB/OpportunityStatuses(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PatchOpportunityStatus(int key, [FromBody]Delta<OpportunityStatus> patch)
        {
            try
            {
                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var item = this.context.OpportunityStatuses.Where(i => i.Id == key).FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                patch.Patch(item);

                this.OnOpportunityStatusUpdated(item);
                this.context.OpportunityStatuses.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.OpportunityStatuses.Where(i => i.Id == key);
                ;
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnOpportunityStatusCreated(OpportunityStatus item);
        partial void OnAfterOpportunityStatusCreated(OpportunityStatus item);

        [HttpPost]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult Post([FromBody] OpportunityStatus item)
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

                this.OnOpportunityStatusCreated(item);
                this.context.OpportunityStatuses.Add(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.OpportunityStatuses.Where(i => i.Id == item.Id);

                ;

                this.OnAfterOpportunityStatusCreated(item);

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
