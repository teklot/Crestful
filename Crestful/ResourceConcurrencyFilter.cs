using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Crestful;

/// <summary>Identifies which generated endpoint a <see cref="ResourceConcurrencyFilter{TResource}"/> guards.</summary>
internal enum ConcurrencyRole
{
    Get,
    Create,
    Update,
    Patch,
    Delete,
}

/// <summary>
/// Implements HTTP conditional request handling for a generated resource endpoint: <c>ETag</c> and
/// <c>Last-Modified</c> response headers, mandatory <c>If-Match</c> on writes, and <c>304 Not Modified</c>
/// for conditional <c>GET</c>. Reads the resource once and stashes it for the endpoint, so the pair
/// performs a single lookup.
/// </summary>
/// <remarks>
/// This filter is only attached to the endpoints Crestful generates. Handlers registered through
/// <see cref="ResourceRouteGroup"/> are not covered.
/// </remarks>
internal sealed class ResourceConcurrencyFilter<TResource> : IEndpointFilter
    where TResource : class, IResource
{
    private readonly ResourceInfo<TResource> _info;
    private readonly ConcurrencyRole _role;

    public ResourceConcurrencyFilter(ResourceInfo<TResource> info, ConcurrencyRole role)
    {
        _info = info;
        _role = role;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        if (_role != ConcurrencyRole.Create)
        {
            if (!_info.TryConvertKey(http.Request.RouteValues["id"]?.ToString(), out var key))
            {
                return await next(context);
            }

            var dataSource = ResourceEndpoint<TResource>.ResolveDataSource(http);
            var current = await dataSource.GetAsync(key!, http.RequestAborted);

            // Let the endpoint produce the 404 so error ownership stays in one place. A soft-deleted
            // resource is absent for HTTP purposes, so it must not satisfy a conditional request either.
            if (current is null || (_info.SoftDeleteEnabled && _info.GetDeletedAt(current).HasValue))
            {
                return await next(context);
            }

            ResourceRequestState.Stash(http, current);

            if (_role == ConcurrencyRole.Get)
            {
                if (IsNotModified(http, current))
                {
                    ApplyHeaders(http, current);
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }
            }
            else if (_info.ConcurrencyEnabled)
            {
                var ifMatch = http.Request.Headers.IfMatch.ToString();
                if (string.IsNullOrWhiteSpace(ifMatch))
                {
                    return ResourceErrors.PreconditionRequired(_info);
                }

                if (!ResourceConcurrency.IsSatisfied(ifMatch, _info.GetRowVersion(current)))
                {
                    return ResourceErrors.PreconditionFailed(_info);
                }
            }
        }

        var result = await next(context);
        ApplyHeadersFromResult(http, result);
        return result;
    }

    /// <summary>
    /// Evaluates the request validators against the current representation. Per RFC 9110,
    /// <c>If-None-Match</c> takes precedence over <c>If-Modified-Since</c>.
    /// </summary>
    private bool IsNotModified(HttpContext http, TResource resource)
    {
        var ifNoneMatch = http.Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            // Without a version token there is no entity tag to compare, so only the wildcard
            // ("the resource still exists") can be satisfied.
            return _info.ConcurrencyEnabled
                ? ResourceConcurrency.IsSatisfied(ifNoneMatch, _info.GetRowVersion(resource))
                : string.Equals(ifNoneMatch.Trim(), "*", StringComparison.Ordinal);
        }

        if (!ResourceConcurrency.TryParseHttpDate(http.Request.Headers.IfModifiedSince.ToString(), out var since))
        {
            return false;
        }

        var updatedAt = GetLastModified(resource);
        return updatedAt is not null && ResourceConcurrency.TruncateToSeconds(updatedAt.Value) <= since;
    }

    /// <summary>
    /// The last update time, or <c>null</c> when there is nothing to report. A default timestamp means
    /// the field was never populated — seeding a <c>DbContext</c> directly bypasses auditing, for
    /// instance — and a year-one date is not a usable cache validator.
    /// </summary>
    private DateTimeOffset? GetLastModified(TResource resource)
    {
        if (!_info.AuditingEnabled)
        {
            return null;
        }

        var updatedAt = _info.GetUpdatedAt(resource);
        return updatedAt is null || updatedAt.Value == DateTimeOffset.MinValue ? null : updatedAt;
    }

    /// <summary>
    /// Stamps the validators onto the response. Only successful results carrying a resource are stamped;
    /// problem and not-found responses are left alone.
    /// </summary>
    private void ApplyHeadersFromResult(HttpContext http, object? result)
    {
        if (result is not IValueHttpResult<TResource> valueResult || valueResult.Value is not TResource resource)
        {
            return;
        }

        // JsonHttpResult leaves StatusCode null when the status defaults to 200; problem and
        // not-found results never reach here because their value type is not TResource.
        if (result is IStatusCodeHttpResult statusResult
            && statusResult.StatusCode is { } statusCode
            && (statusCode < 200 || statusCode >= 300))
        {
            return;
        }

        ApplyHeaders(http, resource);
    }

    private void ApplyHeaders(HttpContext http, TResource resource)
    {
        var headers = http.Response.Headers;

        if (_info.ConcurrencyEnabled)
        {
            headers.ETag = ResourceConcurrency.FormatETag(_info.GetRowVersion(resource));
        }

        var lastModified = GetLastModified(resource);
        if (lastModified is not null)
        {
            headers.LastModified = ResourceConcurrency.TruncateToSeconds(lastModified.Value)
                .ToString("R", CultureInfo.InvariantCulture);
        }
    }
}