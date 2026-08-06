using Microsoft.AspNetCore.Http;

namespace Crestful;

/// <summary>
/// Standard ProblemDetails responses returned by the generated endpoints.
/// </summary>
internal static class ResourceErrors
{
    public static IResult InvalidKey(ResourceInfo info) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid resource key",
        detail: $"The provided key is not valid for resource '{info.Name}'.");

    public static IResult NotFound(ResourceInfo info) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Resource not found",
        detail: $"No '{info.Name}' resource exists with the specified key.");

    public static IResult InvalidBody() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid request body",
        detail: "The request body could not be deserialized into a valid resource.");

    public static IResult Conflict(ResourceInfo info) => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Resource conflict",
        detail: $"A '{info.Name}' resource with the specified key already exists.");

    public static IResult ValidationFailed(ResourceValidationResult result)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var group in result.Errors.GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "resource" : e.PropertyName))
        {
            errors[group.Key] = group.Select(e => e.Message).ToArray();
        }

        return Results.ValidationProblem(
            errors,
            statusCode: StatusCodes.Status400BadRequest,
            title: "One or more validation errors occurred.");
    }
}
