using Cardscape.Domain.Activities;
using Cardscape.Domain.Boards;
using Cardscape.Domain.Cards;
using Cardscape.Domain.Comments;
using Cardscape.Domain.Labels;
using Cardscape.Domain.Lists;
using Cardscape.Domain.Members;
using Cardscape.Domain.Notifications;
using Cardscape.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Cardscape.Infrastructure.Persistence;

/// <summary>
/// The Cardscape database context. The runtime configuration
/// selects the provider (SQLite, PostgreSQL, MariaDB) via
/// <c>Program.cs</c>.
/// </summary>
public sealed class CardscapeDbContext(DbContextOptions<CardscapeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardList> Lists => Set<BoardList>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
