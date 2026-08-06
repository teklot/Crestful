using Crestful;
using FluentValidation;

namespace Crestful.Validation;

/// <summary>
/// Wraps a FluentValidation <see cref="IValidator{T}"/> so it participates in Crestful's
/// automatic request validation.
/// </summary>
public sealed class FluentValidationResourceValidator<TResource> : IResourceValidator
    where TResource : class, IResource
{
    private readonly IValidator<TResource> _validator;

    /// <summary>Creates a validator wrapper around the given <paramref name="validator"/>.</summary>
    public FluentValidationResourceValidator(IValidator<TResource> validator)
    {
        _validator = validator;
    }

    /// <inheritdoc />
    public bool CanValidate(Type resourceType) => resourceType == typeof(TResource);

    /// <inheritdoc />
    public async Task<ResourceValidationResult> ValidateAsync(ResourceValidationContext context, CancellationToken cancellationToken)
    {
        if (context.Resource is not TResource resource)
        {
            return ResourceValidationResult.Success();
        }

        var result = await _validator.ValidateAsync(resource, cancellationToken);
        if (result.IsValid)
        {
            return ResourceValidationResult.Success();
        }

        var errors = result.Errors
            .Select(e => new ResourceValidationError(e.PropertyName, e.ErrorMessage))
            .ToList();
        return new ResourceValidationResult(errors);
    }
}
