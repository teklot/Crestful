using System.ComponentModel.DataAnnotations;
using Crest;
using FluentValidation;

namespace Crest.Tests;

public sealed class Device : IResource
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string? Name { get; set; }

    public string? Model { get; set; }

    public bool IsActive { get; set; } = true;

    public int Quantity { get; set; }
}

public sealed class GuidResource : IResource
{
    public Guid Id { get; set; }

    public string? Label { get; set; }
}

public sealed class StringKeyResource : IResource
{
    [Key]
    public string Code { get; set; } = string.Empty;

    public string? Label { get; set; }
}

public sealed class DeviceIdResource : IResource
{
    public int DeviceId { get; set; }

    public string? Label { get; set; }
}

public sealed class NoKeyResource : IResource
{
    public int SomeValue { get; set; }
}

public sealed class Order : IResource
{
    public int Id { get; set; }

    public string? Reference { get; set; }
}

public sealed class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.Reference).NotEmpty().MaximumLength(3);
    }
}

public sealed class DeviceHook : IResourceHook<Device>
{
    private readonly List<string> _log;

    public DeviceHook(List<string> log)
    {
        _log = log;
    }

    public Task BeforeCreateAsync(CreateContext<Device> context)
    {
        _log.Add("di:before-create");
        return Task.CompletedTask;
    }

    public Task AfterCreateAsync(CreateContext<Device> context)
    {
        _log.Add("di:after-create");
        return Task.CompletedTask;
    }

    public Task BeforeDeleteAsync(DeleteContext<Device> context)
    {
        _log.Add("di:before-delete");
        return Task.CompletedTask;
    }

    public Task AfterDeleteAsync(DeleteContext<Device> context)
    {
        _log.Add("di:after-delete");
        return Task.CompletedTask;
    }

    public Task BeforeSaveAsync(ResourceHookContext context)
    {
        _log.Add("di:before-save");
        return Task.CompletedTask;
    }

    public Task AfterSaveAsync(ResourceHookContext context)
    {
        _log.Add("di:after-save");
        return Task.CompletedTask;
    }
}

public sealed class ThrowingHook : IResourceHook<Device>
{
    public Task BeforeCreateAsync(CreateContext<Device> context)
        => throw new InvalidOperationException("boom");
}
