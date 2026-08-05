using System.ComponentModel.DataAnnotations;
using Crest;

namespace Crest.Validation;

/// <summary>
/// Validates resources using Data Annotations attributes (<c>[Required]</c>, <c>[StringLength]</c>, ...).
/// </summary>
public sealed class DataAnnotationsResourceValidator : IResourceValidator
{
    public bool CanValidate(Type resourceType) => true;

    public Task<ResourceValidationResult> ValidateAsync(ResourceValidationContext context, CancellationToken cancellationToken)
    {
        var results = new List<ValidationResult>();
        var validationContext = new ValidationContext(context.Resource, context.HttpContext.RequestServices, null);
        Validator.TryValidateObject(context.Resource, validationContext, results, validateAllProperties: true);

        var errors = results
            .Select(r => new ResourceValidationError(r.MemberNames.FirstOrDefault() ?? "resource", r.ErrorMessage ?? "The value is invalid."))
            .ToList();

        return Task.FromResult(new ResourceValidationResult(errors));
    }
}
