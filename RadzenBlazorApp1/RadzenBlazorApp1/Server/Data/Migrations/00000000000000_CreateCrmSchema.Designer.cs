using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace RadzenBlazorApp1.Server.Data.Migrations
{
    [DbContext(typeof(CrmDBContext))]
    [Migration("00000000000000_CreateCrmSchema")]
    partial class CreateCrmSchema
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                 .HasAnnotation("ProductVersion", "10.0.0")
                 .HasAnnotation("Sqlite:Autoincrement", true);

            modelBuilder.Entity("RadzenBlazorApp1.Models.CrmDB.Contact", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Email").IsRequired();
                b.Property<string>("FirstName").IsRequired();
                b.Property<string>("LastName").IsRequired();
                b.Property<string>("Company").IsRequired();
                b.Property<string>("Phone").IsRequired();

                b.HasKey("Id");

                b.ToTable("Contacts");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Models.CrmDB.Opportunity", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Name").IsRequired();
                b.Property<decimal>("Amount").IsRequired();
                b.Property<string>("UserId").IsRequired();
                b.Property<int>("StatusId").IsRequired();
                b.Property<int>("ContactId").IsRequired();
                b.Property<DateTime>("CloseDate").IsRequired();

                b.HasKey("Id");

                b.ToTable("Opportunities");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Models.CrmDB.OpportunityStatus", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Name").IsRequired();

                b.HasKey("Id");

                b.ToTable("OpportunityStatuses");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Models.CrmDB.CrmTask", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Title").IsRequired();
                b.Property<DateTime>("DueDate").IsRequired();
                b.Property<int>("TypeId").IsRequired();
                b.Property<int>("StatusId").IsRequired();
                b.Property<int>("OpportunityId").IsRequired();

                b.HasKey("Id");

                b.ToTable("Tasks");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Models.CrmDB.CrmTaskStatus", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Name").IsRequired();

                b.HasKey("Id");

                b.ToTable("TaskStatuses");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Models.CrmDB.TaskType", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Name").IsRequired();

                b.HasKey("Id");

                b.ToTable("TaskTypes");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Models.CrmDB.Opportunity", b =>
            {
                b.HasOne("RadzenBlazorApp1.Models.CrmDB.Contact")
                    .WithMany("Opportunities")
                    .HasForeignKey("ContactId")
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne("RadzenBlazorApp1.Models.OpportunityStatus")
                    .WithMany("Opportunities")
                    .HasForeignKey("StatusId")
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne("RadzenBlazorApp1.Models.ApplicationUser")
                    .WithMany("Opportunities")
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity("RadzenBlazorApp1.Models.CrmDB.CrmTask", b =>
            {
                b.HasOne("RadzenBlazorApp1.Models.CrmDB.TaskType")
                    .WithMany("Tasks")
                    .HasForeignKey("TaskId")
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne("RadzenBlazorApp1.Models.CrmDB.CrmTaskStatus")
                    .WithMany("Tasks")
                    .HasForeignKey("StatusId")
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne("RadzenBlazorApp1.Models.CrmDB.Opportunity")
                    .WithMany("Opportunities")
                    .HasForeignKey("OpportunityId")
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
