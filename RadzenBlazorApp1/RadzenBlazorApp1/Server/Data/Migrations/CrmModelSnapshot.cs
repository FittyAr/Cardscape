using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

using RadzenBlazorApp1.Server.Models;
using RadzenBlazorApp1.Server.Models.CrmDB;
namespace RadzenBlazorApp1.Server.Data.Migrations
{
    [DbContext(typeof(CrmDBContext))]
    partial class CrmModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                 .HasAnnotation("ProductVersion", "10.0.0")
                 .HasAnnotation("Sqlite:Autoincrement", true);

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.ApplicationRole", b =>
            {
                b.Property<string>("Id");

                b.Property<string>("ConcurrencyStamp")
                    .IsConcurrencyToken();

                b.Property<string>("Name")
                    .HasAnnotation("MaxLength", 256);

                b.Property<string>("NormalizedName")
                    .HasAnnotation("MaxLength", 256);

                b.HasKey("Id");

                b.HasIndex("NormalizedName")

                    .IsUnique()
                    .HasDatabaseName("RoleNameIndex")
                        .HasFilter("[NormalizedName] IS NOT NULL");

                b.ToTable("AspNetRoles", null, t =>
                {
                    t.ExcludeFromMigrations();

                    t.HasTrigger("AspNetRoles_Trigger");
                });
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.ApplicationUser", b =>
            {
                b.Property<string>("Id");

                b.Property<int>("AccessFailedCount");

                b.Property<string>("ConcurrencyStamp")
                    .IsConcurrencyToken();

                b.Property<string>("Email")
                    .HasAnnotation("MaxLength", 256);

                b.Property<bool>("EmailConfirmed");

                b.Property<bool>("LockoutEnabled");

                b.Property<DateTimeOffset?>("LockoutEnd");

                b.Property<string>("NormalizedEmail")
                    .HasAnnotation("MaxLength", 256);

                b.Property<string>("NormalizedUserName")
                    .HasAnnotation("MaxLength", 256);

                b.Property<string>("PasswordHash");

                b.Property<string>("PhoneNumber");

                b.Property<bool>("PhoneNumberConfirmed");

                b.Property<string>("SecurityStamp");

                b.Property<bool>("TwoFactorEnabled");

                b.Property<string>("UserName")
                    .HasAnnotation("MaxLength", 256);

                b.Property<string>("FirstName");

                b.Property<string>("LastName");

                b.Property<string>("Picture");

                b.HasKey("Id");

                b.HasIndex("NormalizedEmail")
                    .HasDatabaseName("EmailIndex");

                b.HasIndex("NormalizedUserName")

                    .IsUnique()
                    .HasDatabaseName("UserNameIndex")
                        .HasFilter("[NormalizedUserName] IS NOT NULL");

                b.ToTable("AspNetUsers", null, t =>
                {
                    t.ExcludeFromMigrations();

                    t.HasTrigger("AspNetUsers_Trigger");
                });
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.Contact", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Email").IsRequired();
                b.Property<string>("FirstName");
                b.Property<string>("LastName");
                b.Property<string>("Company");
                b.Property<string>("Phone");

                b.HasKey("Id");

                b.ToTable("Contacts", t =>
                {
                    t.HasTrigger("Contacts_Trigger");
                });
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.Opportunity", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Name").IsRequired();
                b.Property<decimal>("Amount");
                b.Property<string>("UserId").IsRequired();
                b.Property<int>("StatusId");
                b.Property<int>("ContactId");
                b.Property<DateTime>("CloseDate");

                b.HasKey("Id");
                b.HasIndex("ContactId");
                b.HasIndex("StatusId");
                b.HasIndex("UserId");

                b.ToTable("Opportunities", t =>
                {
                    t.HasTrigger("Opportunities_Trigger");
                });
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.OpportunityStatus", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Name");

                b.HasKey("Id");

                b.ToTable("OpportunityStatuses", t =>
                {
                    t.HasTrigger("OpportunityStatuses_Trigger");
                });
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.CrmTask", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Title");
                b.Property<DateTime>("DueDate");
                b.Property<int>("TypeId");
                b.Property<int>("StatusId");
                b.Property<int>("OpportunityId");

                b.HasKey("Id");

                b.HasIndex("OpportunityId");

                b.HasIndex("StatusId");

                b.HasIndex("TypeId");

                b.ToTable("Tasks", t =>
                {
                    t.HasTrigger("Tasks_Trigger");
                });
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.TaskType", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Name");

                b.HasKey("Id");

                b.ToTable("TaskTypes");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.CrmTaskStatus", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Name");

                b.HasKey("Id");

                b.ToTable("TaskStatuses");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.Opportunity", b =>
            {
                b.HasOne("RadzenBlazorApp1.Server.Models.CrmDB.Contact", "Contact")
                    .WithMany("Opportunities")
                    .HasForeignKey("ContactId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("RadzenBlazorApp1.Server.Models.CrmDB.OpportunityStatus", "OpportunityStatus")
                    .WithMany("Opportunities")
                    .HasForeignKey("StatusId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("RadzenBlazorApp1.Server.Models.ApplicationUser", "User")
                    .WithMany("Opportunities")
                    .HasForeignKey("UserId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("Contact");

                b.Navigation("OpportunityStatus");

                b.Navigation("User");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.CrmTask", b =>
            {
                b.HasOne("RadzenBlazorApp1.Server.Models.CrmDB.TaskType", "TaskType")
                    .WithMany("Tasks")
                    .HasForeignKey("TypeId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.HasOne("RadzenBlazorApp1.Server.Models.CrmDB.CrmTaskStatus", "TaskStatus")
                    .WithMany("Tasks")
                    .HasForeignKey("StatusId")
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne("RadzenBlazorApp1.Server.Models.CrmDB.Opportunity", "Opportunity")
                    .WithMany("Tasks")
                    .HasForeignKey("OpportunityId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                b.Navigation("Opportunity");

                b.Navigation("TaskStatus");

                b.Navigation("TaskType");
            });

             modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.Contact", b =>
            {
                b.Navigation("Opportunities");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.CrmTaskStatus", b =>
            {
                b.Navigation("Tasks");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.Opportunity", b =>
            {
                b.Navigation("Tasks");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.OpportunityStatus", b =>
            {
                b.Navigation("Opportunities");
            });

            modelBuilder.Entity("RadzenBlazorApp1.Server.Models.CrmDB.TaskType", b =>
            {
                b.Navigation("Tasks");
            });
        }
    }
}
