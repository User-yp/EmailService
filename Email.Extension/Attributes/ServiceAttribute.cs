using Microsoft.Extensions.DependencyInjection;

namespace Email.Extension.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ServiceAttribute(ServiceLifetime LifeTime) : Attribute
{
    public ServiceLifetime LifeTime { get; private set; } = LifeTime;
}