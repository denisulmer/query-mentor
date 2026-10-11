using Microsoft.EntityFrameworkCore;

namespace QueryMentor.Tests;

public class QuerySchemaExtractorTests
{
    [Theory]
    [InlineData("SELECT \"b\".\"Id\" FROM \"Blogs\" AS \"b\"", null, "Blogs")]
    [InlineData("SELECT [b].[Id] FROM [Blogs] AS [b]", null, "Blogs")]
    [InlineData("SELECT [b].[Id] FROM [dbo].[Blogs] AS [b]", "dbo", "Blogs")]
    [InlineData("SELECT b.Id FROM dbo.Blogs b", "dbo", "Blogs")]
    [InlineData("SELECT * FROM [My Db].[dbo].[Blog Posts]", "dbo", "Blog Posts")]
    [InlineData("SELECT `b`.`Id` FROM `Blogs` AS `b`", null, "Blogs")]
    [InlineData("select * from blogs", null, "blogs")]
    [InlineData("UPDATE [Blogs] SET [Name] = @p0", null, "Blogs")]
    [InlineData("INSERT INTO \"Blogs\" (\"Name\") VALUES (@p0)", null, "Blogs")]
    [InlineData("DELETE FROM \"Blogs\" WHERE \"Id\" = @p0", null, "Blogs")]
    [InlineData("MERGE [Blogs] USING (VALUES (@p0)) AS i ([Name]) ON 1=0", null, "Blogs")]
    [InlineData("SELECT [b] . [Id] FROM [dbo] . [Blogs]", "dbo", "Blogs")]
    public void ParseTableReferences_FindsTable(string sql, string? expectedSchema, string expectedName)
    {
        var reference = Assert.Single(QuerySchemaExtractor.ParseTableReferences(sql));

        Assert.Equal(expectedSchema, reference.Schema);
        Assert.Equal(expectedName, reference.Name);
    }

    [Fact]
    public void ParseTableReferences_FindsJoinedTablesOnce()
    {
        const string sql = """
            SELECT [b].[Name], [p].[Title]
            FROM [Blogs] AS [b]
            INNER JOIN [Posts] AS [p] ON [b].[Id] = [p].[BlogId]
            LEFT JOIN [blogs] AS [b2] ON [b2].[Id] = [p].[BlogId]
            """;

        var references = QuerySchemaExtractor.ParseTableReferences(sql);

        Assert.Equal(["Blogs", "Posts"], references.Select(r => r.Name));
    }

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("SELECT [DateFrom], [JoinedOn] FROM (SELECT 1 AS [DateFrom]) AS [x]")]
    [InlineData("")]
    public void ParseTableReferences_IgnoresNonTables(string sql)
    {
        Assert.Empty(QuerySchemaExtractor.ParseTableReferences(sql));
    }

    [Fact]
    public void GetTableDefinitions_DescribesColumnsKeysAndIndexes()
    {
        using var context = CreateTestDbContext();

        var table = Assert.Single(QuerySchemaExtractor.GetTableDefinitions(context.Model, "SELECT * FROM [dbo].[Posts] AS [p]"));

        Assert.Equal("Posts", table.TableName);
        Assert.Equal(["Id"], table.PrimaryKeyColumns);
        Assert.Contains(table.Columns, c => c.ColumnName == "Title" && c.DataType == "TEXT" && !c.IsNullable);
        var index = Assert.Single(table.Indexes, i => i.Columns.SequenceEqual(["Title"]));
        Assert.Equal("IX_Posts_Title", index.IndexName); // the database name, not IIndex.Name (which is null)
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void GetTableDefinitions_DoesNotFailOnEntitiesWithoutTable()
    {
        // BlogSummary is mapped to a view, so IEntityType.GetTableName() returns null.
        using var context = CreateTestDbContext();

        var tables = QuerySchemaExtractor.GetTableDefinitions(context.Model, "SELECT * FROM Posts JOIN Blogs ON 1=1 JOIN BlogSummaries ON 1=1");

        Assert.Equal(["Posts", "Blogs"], tables.Select(t => t.TableName));
    }

    [Fact]
    public void GetTableDefinitions_IncludesColumnsOfDerivedAndOwnedTypes()
    {
        using var context = new InheritanceDbContext();

        var table = Assert.Single(QuerySchemaExtractor.GetTableDefinitions(context.Model, "SELECT * FROM [zoo].[Animals]"));

        var columns = table.Columns.Select(c => c.ColumnName).ToList();
        Assert.Contains("Name", columns);
        Assert.Contains("Breed", columns); // Dog
        Assert.Contains("Lives", columns); // Cat
        Assert.Contains("Owner_City", columns); // owned type
    }

    [Fact]
    public void GetTableDefinitions_IgnoresTablesInOtherSchemas()
    {
        using var context = new InheritanceDbContext();

        Assert.Empty(QuerySchemaExtractor.GetTableDefinitions(context.Model, "SELECT * FROM [other].[Animals]"));
    }

    private static TestDbContext CreateTestDbContext()
        => new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite("DataSource=:memory:").Options);

    private abstract class Animal
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Address Owner { get; set; } = new();
    }

    private sealed class Dog : Animal
    {
        public string? Breed { get; set; }
    }

    private sealed class Cat : Animal
    {
        public int Lives { get; set; }
    }

    private sealed class Address
    {
        public string? City { get; set; }
    }

    private sealed class InheritanceDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite("DataSource=:memory:");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Animal>().ToTable("Animals", "zoo").OwnsOne(a => a.Owner);
            modelBuilder.Entity<Dog>();
            modelBuilder.Entity<Cat>();
        }
    }
}
