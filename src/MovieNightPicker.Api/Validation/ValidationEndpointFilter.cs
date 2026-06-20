using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MovieNightPicker.Api.Validation;

/// <summary>
/// Endpoint filter that runs DataAnnotations validation on the request DTO of type
/// <typeparamref name="T"/> before the handler executes. Minimal APIs do NOT auto-run
/// DataAnnotations, so without this the <c>[Range]</c>/<c>[Required]</c> attributes on
/// request records are purely decorative. On invalid input it short-circuits with an
/// RFC7807 <see cref="ValidationProblem"/>; otherwise the handler runs unchanged.
/// </summary>
public sealed class ValidationEndpointFilter<T> : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Find the first argument of the DTO type (its position varies with the
        // handler's other parameters, e.g. the route id / services).
        var model = context.Arguments.OfType<T>().FirstOrDefault();
        if (model is not null)
        {
            var results = new List<ValidationResult>();
            var validationContext = new ValidationContext(model);

            if (!Validator.TryValidateObject(model, validationContext, results, validateAllProperties: true))
            {
                var errors = results
                    .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : new[] { string.Empty })
                        .Select(member => (Member: member, r.ErrorMessage)))
                    .GroupBy(e => e.Member, e => e.ErrorMessage ?? "Invalid value.")
                    .ToDictionary(g => g.Key, g => g.ToArray());

                return TypedResults.ValidationProblem(errors);
            }
        }

        return await next(context);
    }
}

/// <summary>
/// Fluent helpers for attaching <see cref="ValidationEndpointFilter{T}"/> to a route.
/// Reusable: any endpoint with a DataAnnotations-decorated DTO can adopt this with a
/// single <c>.WithRequestValidation&lt;TDto&gt;()</c> call.
/// </summary>
public static class ValidationEndpointFilterExtensions
{
    /// <summary>Runs DataAnnotations validation on the <typeparamref name="T"/> request body.</summary>
    public static RouteHandlerBuilder WithRequestValidation<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter<ValidationEndpointFilter<T>>();
}
