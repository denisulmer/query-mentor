namespace QueryMentor.Tests;

public class CommandClassifierTests
{
    [Theory]
    [InlineData("SELECT \"b\".\"Id\" FROM \"Blogs\" AS \"b\"")]
    [InlineData("  \n\tselect 1")]
    [InlineData("WITH x AS (SELECT 1) SELECT * FROM x")]
    [InlineData("UPDATE [Blogs] SET [Name] = @p0")]
    [InlineData("DELETE FROM \"Blogs\"")]
    [InlineData("INSERT INTO \"Blogs\" (\"Name\") VALUES (@p0)")]
    [InlineData("SET IMPLICIT_TRANSACTIONS OFF;\nSET NOCOUNT ON;\nINSERT INTO [Blogs] ([Name]) OUTPUT INSERTED.[Id] VALUES (@p0);")]
    [InlineData("-- My tag from TagWith\n\nSELECT * FROM Blogs")]
    [InlineData("/* comment */ SELECT * FROM Blogs")]
    public void Queries_AreAnalyzable(string sql)
    {
        Assert.True(CommandClassifier.IsAnalyzable(sql));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-- only a comment")]
    [InlineData("CREATE TABLE \"Blogs\" (\"Id\" INTEGER NOT NULL)")]
    [InlineData("create index IX_Blogs_Url on Blogs (Url)")]
    [InlineData("ALTER TABLE [Blogs] ADD [Rating] int NOT NULL DEFAULT 0;")]
    [InlineData("DROP TABLE [Blogs]")]
    [InlineData("PRAGMA journal_mode = 'wal';")]
    [InlineData("-- tag\nCREATE TABLE x (id int)")]
    [InlineData("SELECT \"MigrationId\", \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";")]
    [InlineData("INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'1', N'8.0.0');")]
    [InlineData("SELECT COUNT(*) FROM \"sqlite_master\" WHERE \"type\" = 'table' AND \"rootpage\" IS NOT NULL;")]
    [InlineData("SELECT CASE WHEN EXISTS (SELECT * FROM [sys].[objects] o WHERE o.[type] = 'U') THEN 1 ELSE 0 END")]
    [InlineData("SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'x'")]
    [InlineData("SELECT * FROM pg_catalog.pg_class")]
    public void SchemaAndMigrationCommands_AreNotAnalyzable(string sql)
    {
        Assert.False(CommandClassifier.IsAnalyzable(sql));
    }
}
