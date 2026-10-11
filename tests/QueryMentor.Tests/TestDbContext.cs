using Microsoft.EntityFrameworkCore;

namespace QueryMentor.Tests;

public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
    public List<Post> Posts { get; set; } = [];
}

public class Post
{
    public int Id { get; set; }
    public int BlogId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Blog? Blog { get; set; }
}

/// <summary>Keyless entity mapped to a view, so it has no table name.</summary>
public class BlogSummary
{
    public string Name { get; set; } = string.Empty;
    public int PostCount { get; set; }
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<BlogSummary> BlogSummaries => Set<BlogSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>().HasIndex(b => b.Url).IsUnique();
        modelBuilder.Entity<Post>().HasIndex(p => p.Title);
        modelBuilder.Entity<BlogSummary>().HasNoKey().ToView("BlogSummaries");
    }
}
