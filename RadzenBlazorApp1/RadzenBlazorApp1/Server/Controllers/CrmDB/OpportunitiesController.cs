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
    [Route("odata/CrmDB/Opportunities")]
    public partial class OpportunitiesController : ODataController
    {
        private CrmDBContext context;

        public OpportunitiesController(CrmDBContext context)
        {
            this.context = context;
        }

    
        [HttpGet]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IEnumerable<Opportunity> GetOpportunities()
        {
            var items = this.context.Opportunities.AsQueryable<Opportunity>();
            this.OnOpportunitiesRead(ref items);

            return items;
        }

        partial void OnOpportunitiesRead(ref IQueryable<Opportunity> items);

        partial void OnOpportunityGet(ref SingleResult<Opportunity> item);

        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        [HttpGet("/odata/CrmDB/Opportunities(Id={Id})")]
        public SingleResult<Opportunity> GetOpportunity(int key)
        {
            var items = this.context.Opportunities.Where(i => i.Id == key);
            var result = SingleResult.Create(items);

            OnOpportunityGet(ref result);

            return result;
        }
        partial void OnOpportunityDeleted(Opportunity item);
        partial void OnAfterOpportunityDeleted(Opportunity item);

        [HttpDelete("/odata/CrmDB/Opportunities(Id={Id})")]
        public IActionResult DeleteOpportunity(int key)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }


                var item = this.context.Opportunities
                    .Where(i => i.Id == key)
                    .FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                this.OnOpportunityDeleted(item);
                this.context.Opportunities.Remove(item);
                this.context.SaveChanges();
                this.OnAfterOpportunityDeleted(item);

                return new NoContentResult();

            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnOpportunityUpdated(Opportunity item);
        partial void OnAfterOpportunityUpdated(Opportunity item);

        [HttpPut("/odata/CrmDB/Opportunities(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PutOpportunity(int key, [FromBody]Opportunity item)
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
                this.OnOpportunityUpdated(item);
                this.context.Opportunities.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.Opportunities.Where(i => i.Id == key);
                Request.QueryString = Request.QueryString.Add("$expand", "Contact,OpportunityStatus");
                this.OnAfterOpportunityUpdated(item);
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        [HttpPatch("/odata/CrmDB/Opportunities(Id={Id})")]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult PatchOpportunity(int key, [FromBody]Delta<Opportunity> patch)
        {
            try
            {
                if(!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var item = this.context.Opportunities.Where(i => i.Id == key).FirstOrDefault();

                if (item == null)
                {
                    return BadRequest();
                }
                patch.Patch(item);

                this.OnOpportunityUpdated(item);
                this.context.Opportunities.Update(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.Opportunities.Where(i => i.Id == key);
                Request.QueryString = Request.QueryString.Add("$expand", "Contact,OpportunityStatus");
                return new ObjectResult(SingleResult.Create(itemToReturn));
            }
            catch(Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return BadRequest(ModelState);
            }
        }

        partial void OnOpportunityCreated(Opportunity item);
        partial void OnAfterOpportunityCreated(Opportunity item);

        [HttpPost]
        [EnableQuery(MaxExpansionDepth=10,MaxAnyAllExpressionDepth=10,MaxNodeCount=1000)]
        public IActionResult Post([FromBody] Opportunity item)
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

                this.OnOpportunityCreated(item);
                this.context.Opportunities.Add(item);
                this.context.SaveChanges();

                var itemToReturn = this.context.Opportunities.Where(i => i.Id == item.Id);

                Request.QueryString = Request.QueryString.Add("$expand", "Contact,OpportunityStatus");

                this.OnAfterOpportunityCreated(item);

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
