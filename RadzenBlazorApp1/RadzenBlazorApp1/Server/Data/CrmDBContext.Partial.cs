using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

using RadzenBlazorApp1.Server.Models;
using RadzenBlazorApp1.Server.Models.CrmDB;
namespace RadzenBlazorApp1.Server.Data
{
    public partial class CrmDBContext
    {
        UserManager<ApplicationUser> userManager;
        public CrmDBContext(DbContextOptions<CrmDBContext> options, UserManager<ApplicationUser> userManager) : base(options)
        {
            this.userManager = userManager;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder.ConfigureWarnings(warnings =>
                        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }

        public async Task Seed()
        {
            var users = userManager.Users.ToList();

            OpportunityStatuses.AddRange(new OpportunityStatus[] { 
                new OpportunityStatus { Name = "Active" },
                new OpportunityStatus { Name = "Won" },
                new OpportunityStatus { Name = "Lost" },
                new OpportunityStatus { Name = "Inactive" }
            });

            TaskStatuses.AddRange(new CrmTaskStatus[] {
                new CrmTaskStatus { Name = "Not Started" },
                new CrmTaskStatus { Name = "In Progress" },
                new CrmTaskStatus { Name = "Complete" }
            });

            TaskTypes.AddRange(new TaskType[] {
                new TaskType { Name = "Email" },
                new TaskType { Name = "Call" },
                new TaskType { Name = "Online Meeting" }
            });

            Contacts.AddRange(new Contact[] {
                new Contact 
                {
                    Email = "emily.dawson@sample.com",
                    FirstName = "Emily",
                    LastName = "Dawson",
                    Phone = "+1 (555) 123-4567",
                    Company = "Tech Solutions Inc."
                },
                new Contact
                {
                    Email = "michael.roberts@sample.com",
                    FirstName = "Michael",
                    LastName = "Roberts",
                    Phone = "+1 (555) 123-4567",
                    Company = "Global Enterprises"
                },
                new Contact
                {
                    Email = "sophia.mitchell@sample.com",
                    FirstName = "Sophia",
                    LastName = "Mitchell",
                    Phone = "+1 (555) 123-4567",
                    Company = "Creative Agency"
                }
            });

            await SaveChangesAsync();

            var rnd = new Random();

            Opportunities.AddRange(new Opportunity[] {
                new Opportunity
                {
                    ContactId = Contacts.ToList()[0].Id,
                    StatusId = OpportunityStatuses.ToList()[0].Id,
                    Amount = 25000,
                    CloseDate = new DateTime(2023, 11, 30),
                    Name = "Website redesign project for Tech Solutions Inc.",
                    UserId = users[rnd.Next(users.Count - 1)].Id
                },
                new Opportunity
                {
                    ContactId = Contacts.ToList()[1].Id,
                    StatusId = OpportunityStatuses.ToList()[1].Id,
                    Amount = 50000,
                    CloseDate = new DateTime(2023, 10, 15),
                    Name = "ERP system implementation for Global Enterprises",
                    UserId = users[rnd.Next(users.Count - 1)].Id
                },
                new Opportunity
                {
                    ContactId = Contacts.ToList()[2].Id,
                    StatusId = OpportunityStatuses.ToList()[2].Id,
                    Amount = 30000,
                    CloseDate = new DateTime(2023, 9, 20),
                    Name = "Marketing campaign for Creative Agency",
                    UserId = users[rnd.Next(users.Count - 1)].Id
                }
            });

            await SaveChangesAsync();

            Tasks.AddRange(new CrmTask[] {
                new CrmTask 
                {
                    OpportunityId = Opportunities.ToList()[0].Id,
                    StatusId = TaskStatuses.ToList()[0].Id,
                    TypeId = TaskTypes.ToList()[0].Id,
                    DueDate = new DateTime(2023, 11, 15),
                    Title = "Send proposal for website redesign"
                },
                new CrmTask
                {
                    OpportunityId = Opportunities.ToList()[1].Id,
                    StatusId = TaskStatuses.ToList()[1].Id,
                    TypeId = TaskTypes.ToList()[1].Id,
                    DueDate = new DateTime(2023, 10, 1),
                    Title = "Follow up on ERP system implementation"
                },
                new CrmTask
                {
                    OpportunityId = Opportunities.ToList()[2].Id,
                    StatusId = TaskStatuses.ToList()[2].Id,
                    TypeId = TaskTypes.ToList()[2].Id,
                    DueDate = new DateTime(2023, 9, 10),
                    Title = "Schedule meeting for marketing campaign"
                }
            });

            await SaveChangesAsync();
        }

        partial void OnModelBuilding(ModelBuilder builder)
        {
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.ToTable("AspNetUsers", tableBuilder => tableBuilder.ExcludeFromMigrations());
                entity.Ignore(u => u.Roles);
            });

            builder.Entity<ApplicationRole>(entity =>
            {
                entity.ToTable("AspNetRoles", tableBuilder => tableBuilder.ExcludeFromMigrations());
                entity.Ignore(r => r.Users);
            });

        }
    }
}