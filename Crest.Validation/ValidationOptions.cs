using System.Reflection;

namespace Crest.Validation;

/// <summary>
/// Configuration for the validation package.
/// </summary>
public sealed class ValidationOptions
{
    /// <summary>Assemblies scanned for FluentValidation validators. Defaults to the calling
    /// and entry assemblies.</summary>
    public IList<Assembly> Assemblies { get; } = new List<Assembly>();

    /// <summary>Whether Data Annotations on resources are validated automatically. Defaults to
    /// <c>true</c>.</summary>
    public bool UseDataAnnotations { get; set; } = true;
}
