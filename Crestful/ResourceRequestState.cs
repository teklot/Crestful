using Microsoft.AspNetCore.Http;

namespace Crestful;

/// <summary>
/// Carries the resource an endpoint filter has already read to the endpoint it wraps, so the pair
/// performs one lookup per request instead of two. Entries are keyed by resource type, which keeps
/// nested or concurrent requests isolated — <see cref="HttpContext.Items"/> is per-request.
/// </summary>
internal static class ResourceRequestState
{
    /// <summary>Records the resource read by the concurrency filter for the current request.</summary>
    public static void Stash<TResource>(HttpContext http, TResource resource) where TResource : class, IResource
        => http.Items[typeof(TResource)] = resource;

    /// <summary>
    /// Retrieves and removes the stashed resource, returning <c>null</c> when the filter did not run —
    /// which is the case when concurrency and auditing are both disabled for the resource.
    /// </summary>
    public static TResource? Take<TResource>(HttpContext http) where TResource : class, IResource
    {
        if (http.Items.TryGetValue(typeof(TResource), out var value) && value is TResource typed)
        {
            http.Items.Remove(typeof(TResource));
            return typed;
        }

        return null;
    }
}