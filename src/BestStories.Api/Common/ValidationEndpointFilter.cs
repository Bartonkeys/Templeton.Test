using FluentValidation;

namespace BestStories.Api.Common;

/// <summary>
/// Reusable endpoint filter that runs FluentValidation against the first argument
/// of type T and short-circuits with a 400 ValidationProblem on failure.
/// </summary>
public sealed class ValidationEndpointFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is not null)
        {
            var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                return Results.ValidationProblem(result.ToDictionary());
            }
        }

        return await next(context);
    }
}
