namespace MovieNightPicker.Web.Services;

/// <summary>
/// Outcome of an API call routed through <see cref="ApiCall"/>: either a
/// <see cref="Value"/> (on success) or a human-friendly <see cref="Error"/>
/// message (when the call failed). Exactly one is non-null.
/// </summary>
public sealed record ApiResult<T>(T? Value, string? Error)
{
    /// <summary>True when the call succeeded (no error message).</summary>
    public bool Succeeded => Error is null;

    /// <summary>Wrap a successful result.</summary>
    public static ApiResult<T> Ok(T? value) => new(value, null);

    /// <summary>Wrap a failure with a user-facing message.</summary>
    public static ApiResult<T> Fail(string error) => new(default, error);
}

/// <summary>
/// Lightweight wrapper for Blazor page → API calls. Catches the network/transport
/// failures (<see cref="HttpRequestException"/>, <see cref="TaskCanceledException"/>)
/// that would otherwise bubble up as unhandled Blazor exceptions and break the page,
/// returning a friendly error message instead. Pages render that message via
/// <c>ApiErrorBoundary</c>.
/// </summary>
/// <remarks>
/// Reusable: any page can route a <c>GetFromJsonAsync</c>/<c>PostAsJsonAsync</c> call
/// (or a feature-client method) through <see cref="RunAsync{T}"/> to get graceful
/// failure handling for free.
/// </remarks>
public static class ApiCall
{
    private const string DefaultMessage =
        "Couldn't reach the server. Check your connection and try again.";

    /// <summary>
    /// Execute <paramref name="call"/>, returning its value on success or a friendly
    /// error message if the request fails or is cancelled.
    /// </summary>
    public static async Task<ApiResult<T>> RunAsync<T>(
        Func<Task<T?>> call, string? failureMessage = null)
    {
        try
        {
            return ApiResult<T>.Ok(await call());
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Fail(failureMessage ?? DefaultMessage);
        }
        catch (TaskCanceledException)
        {
            return ApiResult<T>.Fail(failureMessage ?? DefaultMessage);
        }
    }
}
