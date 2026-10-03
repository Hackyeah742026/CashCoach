using FluentAssertions;

namespace CashCoach.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Test_project_runs() => true.Should().BeTrue();
}
