using MediatR;
using Microsoft.EntityFrameworkCore;
using ReusableAccess.Application.Completion;

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
            when (typeof(TRequest).Namespace ==
                  typeof(CompletionHandlers).Namespace)
        {
            throw new CompletionException(
                409, "اطلاعات هم‌زمان تغییر کرده؛ دوباره دریافت کنید.");
        }
    }
}