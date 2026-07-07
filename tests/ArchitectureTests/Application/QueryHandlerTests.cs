using Application.Abstractions.Messaging;
using NetArchTest.Rules;
using Shouldly;

namespace ArchitectureTests.Application;

public class QueryHandlerTests : BaseTest
{
    [Fact]
    public void QueryHandlers_Should_ImplementIQueryHandlerInterface()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("QueryHandler")
            .Should()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(BuildMessage(result,
            "Types ending with QueryHandler must implement IQueryHandler<TQuery,TResponse>"));
    }

    [Fact]
    public void QueryHandlers_Should_BeInternalSealed()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("QueryHandler")
            .Should()
            .BeSealed()
            .And()
            .NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(BuildMessage(result,
            "QueryHandlers must be internal sealed (DI-only; not extension points)"));
    }

    private static string BuildMessage(TestResult result, string rule)
    {
        var failing = result.FailingTypeNames is null
            ? "(none reported)"
            : string.Join(", ", result.FailingTypeNames);
        return $"{rule}. Failing: {failing}";
    }
}
