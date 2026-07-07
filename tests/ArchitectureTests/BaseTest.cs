using System.Reflection;
using Application.Abstractions.Messaging;
using Domain;
using Infrastructure;
using SharedKernel;

namespace ArchitectureTests;

/// <summary>
/// Holds Assembly references for every layer the architecture tests inspect.
/// </summary>
public abstract class BaseTest
{
    protected static readonly Assembly SharedKernelAssembly = typeof(Entity).Assembly;
    protected static readonly Assembly DomainAssembly = typeof(SystemConstants).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(ICommand).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;
}
