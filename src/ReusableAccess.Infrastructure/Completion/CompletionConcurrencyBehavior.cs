using MediatR;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Completion;
using ReusableAccess.Application.Errors;

namespace ReusableAccess.Infrastructure.Completion;

public sealed class CompletionConcurrencyBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        try
        {
            return await next();
        }
        catch (DbUpdateConcurrencyException)
            when (
                request is IConcurrencyConflictRequest ||

                typeof(TRequest).Namespace ==
                    typeof(CompletionHandlers).Namespace ||

                typeof(TRequest).Namespace ==
                    typeof(
                        ReusableAccess.Application.Users.SearchUsers
                    ).Namespace)
        {
            throw new UseCaseException(
                409,
                "اطلاعات هم‌زمان تغییر کرده؛ دوباره دریافت کنید.");
        }
    }
}