using CashCoach.Infrastructure.Persistence;
using FluentAssertions;

namespace CashCoach.Api.Tests.Persistence;

public class SqliteConnectionStringsTests
{
    private static readonly string BaseDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cashcoach-root"));

    [Fact]
    public void Relative_file_is_anchored_to_the_base_directory()
    {
        var resolved = SqliteConnectionStrings.Resolve("Data Source=cashcoach.db", BaseDirectory);

        resolved.Should().Be($"Data Source={Path.Combine(BaseDirectory, "cashcoach.db")}");
    }

    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=shared;Mode=Memory;Cache=Shared")]
    public void In_memory_databases_are_left_unchanged(string connectionString) =>
        SqliteConnectionStrings.Resolve(connectionString, BaseDirectory).Should().Be(connectionString);

    [Fact]
    public void Absolute_path_is_left_unchanged()
    {
        var connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), "other.db")}";

        SqliteConnectionStrings.Resolve(connectionString, BaseDirectory).Should().Be(connectionString);
    }
}
