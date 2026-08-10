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
    [Authorize]
    public partial class OpportunitiesController
    {
        partial void OnOpportunitiesRead(ref IQueryable<Opportunity> items)
        {
            var userManager = (UserManager<ApplicationUser>)HttpContext.RequestServices.GetService(typeof(UserManager<ApplicationUser>));

            var user = userManager.GetUserAsync(HttpContext.User).Result;
            if (user != null)
            {
                var roles = userManager.GetRolesAsync(user).Result;

                if (!roles.Contains("Sales Manager"))
                {
                    // Filter the opportunities by the current user's id
                    items = items.Where(item => item.UserId == user.Id);
                }
            }

            items = items.Include(item => item.User);
        }
    }
}