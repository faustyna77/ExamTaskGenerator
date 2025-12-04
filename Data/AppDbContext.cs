using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using ExamCreateApp.Models;

namespace ExamCreateApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<ExamTask> ExamTasks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // ExamTask configuration
        modelBuilder.Entity<ExamTask>(entity =>
        {
            entity.HasIndex(e => e.Level);
            entity.HasIndex(e => e.Year);
            entity.HasIndex(e => e.Subject);
        });
    }
}