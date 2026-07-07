using Application.Abstractions.Messaging;
using NetArchTest.Rules;
using Shouldly;

namespace ArchitectureTests.Application;

public class CommandHandlerTests : BaseTest
{
    [Fact]
    public void CommandHandlers_Should_ImplementICommandHandlerInterface()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Should()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(BuildMessage(result,
            "Types ending with CommandHandler must implement ICommandHandler<TCommand> or ICommandHandler<TCommand,TResponse>"));
    }

    [Fact]
    public void CommandHandlers_Should_BeInternalSealed()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Should()
            .BeSealed()
            .And()
            .NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(BuildMessage(result,
            "CommandHandlers must be internal sealed (DI-only; not extension points)"));
    }

    private static string BuildMessage(TestResult result, string rule)
    {
        var failing = result.FailingTypeNames is null
            ? "(none reported)"
            : string.Join(", ", result.FailingTypeNames);
        return $"{rule}. Failing: {failing}";
    }
}
