using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crestful;

/// <summary>
/// Validates a resource before it is created or updated. Validators are resolved from the
/// DI container (see <c>ResourceValidationService</c>) so feature packages such as
/// <c>Crestful.Validation</c> can plug in without the core depending on them.
/// </summary>
public interface IResourceValidator
{
    /// <summary>Whether this validator can validate the given resource type.</summary>
    bool CanValidate(Type resourceType);

    /// <summary>Validates a resource instance.</summary>
    Task<ResourceValidationResult> ValidateAsync(ResourceValidationContext context, CancellationToken cancellationToken);
}

/// <summary>Context passed to a validator.</summary>
public sealed class ResourceValidationContext
{
    /// <summary>Creates a validation context for the given request, resource, and metadata.</summary>
    public ResourceValidationContext(HttpContext httpContext, object resource, ResourceInfo resourceInfo)
    {
        HttpContext = httpContext;
        Resource = resource;
        ResourceInfo = resourceInfo;
    }

    /// <summary>The current HTTP request.</summary>
    public HttpContext HttpContext { get; }

    /// <summary>The resource instance to validate.</summary>
    public object Resource { get; }

    /// <summary>Metadata for the resource.</summary>
    public ResourceInfo ResourceInfo { get; }
}

/// <summary>Result of validating a resource.</summary>
public sealed class ResourceValidationResult
{
    private static readonly ResourceValidationResult Valid = new(Array.Empty<ResourceValidationError>());

    /// <summary>Creates a validation result with the given <paramref name="errors"/>.</summary>
    public ResourceValidationResult(IReadOnlyList<ResourceValidationError> errors)
    {
        Errors = errors;
    }

    /// <summary>A result with no errors.</summary>
    public static ResourceValidationResult Success() => Valid;

    /// <summary>The collected validation errors.</summary>
    public IReadOnlyList<ResourceValidationError> Errors { get; }

    /// <summary>Whether validation passed.</summary>
    public bool IsValid => Errors.Count == 0;
}

/// <summary>A single validation error.</summary>
public sealed record ResourceValidationError(string PropertyName, string Message);

/// <summary>
/// Runs every registered <see cref="IResourceValidator"/> that can validate the resource
/// and aggregates the results. Resolves validators per request so scoped validators are
/// not captured by this (singleton) service.
/// </summary>
public sealed class ResourceValidationService
{
    private readonly IServiceProvider _services;

    /// <summary>Creates a validation service that resolves validators from <paramref name="services"/>.</summary>
    public ResourceValidationService(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>Validates a resource with every applicable registered validator.</summary>
    public async Task<ResourceValidationResult> ValidateAsync(HttpContext httpContext, object resource, ResourceInfo resourceInfo)
    {
        var errors = new List<ResourceValidationError>();
        foreach (var validator in _services.GetServices<IResourceValidator>())
        {
            if (!validator.CanValidate(resourceInfo.ResourceType))
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ResourceValidationContext(httpContext, resource, resourceInfo),
                httpContext.RequestAborted);
            if (!result.IsValid)
            {
                errors.AddRange(result.Errors);
            }
        }

        return new ResourceValidationResult(errors);
    }
}
