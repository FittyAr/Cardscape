using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

using RadzenBlazorApp1.Server.Models.CrmDB;
namespace RadzenBlazorApp1.Server.Data
{
    public partial class CrmDBContext : DbContext
    {
        public CrmDBContext()
        {
        }

        public CrmDBContext(DbContextOptions<CrmDBContext> options) : base(options)
        {
        }

        partial void OnModelBuilding(ModelBuilder builder);

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Opportunity>()
              .HasOne(i => i.Contact)
              .WithMany(i => i.Opportunities)
              .HasForeignKey(i => i.ContactId)
              .HasPrincipalKey(i => i.Id);

            builder.Entity<Opportunity>()
              .HasOne(i => i.OpportunityStatus)
              .WithMany(i => i.Opportunities)
              .HasForeignKey(i => i.StatusId)
              .HasPrincipalKey(i => i.Id);

            builder.Entity<CrmTask>()
              .HasOne(i => i.Opportunity)
              .WithMany(i => i.Tasks)
              .HasForeignKey(i => i.OpportunityId)
              .HasPrincipalKey(i => i.Id);

            builder.Entity<CrmTask>()
              .HasOne(i => i.TaskStatus)
              .WithMany(i => i.Tasks)
              .HasForeignKey(i => i.StatusId)
              .HasPrincipalKey(i => i.Id);

            builder.Entity<CrmTask>()
              .HasOne(i => i.TaskType)
              .WithMany(i => i.Tasks)
              .HasForeignKey(i => i.TypeId)
              .HasPrincipalKey(i => i.Id);

            builder.Entity<Opportunity>()
              .Property(p => p.CloseDate)
              .HasColumnType("datetime");

            builder.Entity<CrmTask>()
              .Property(p => p.DueDate)
              .HasColumnType("datetime");

            this.OnModelBuilding(builder);
        }

        public DbSet<Contact> Contacts { get; set; }

        public DbSet<Opportunity> Opportunities { get; set; }

        public DbSet<OpportunityStatus> OpportunityStatuses { get; set; }

        public DbSet<CrmTask> Tasks { get; set; }

        public DbSet<CrmTaskStatus> TaskStatuses { get; set; }

        public DbSet<TaskType> TaskTypes { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Conventions.Add(_ => new BlankTriggerAddingConvention());
        }
    }
}