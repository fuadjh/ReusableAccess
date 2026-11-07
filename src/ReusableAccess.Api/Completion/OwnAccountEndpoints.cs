using MediatR;
using ReusableAccess.Application.Completion;
using ReusableAccess.Contracts;

namespace ReusableAccess.Api.Completion;

public static class OwnAccountEndpoints
{
public static void MapOwnAccountEndpoints(this WebApplication app)
{
app.MapPut("/api/account/me",
async (
UpdateOwnNamesInput input,
HttpContext context,
ISender sender,
CancellationToken ct) =>
{
var userId = CompletionApiExtensions.Subject(context);
var saved = await sender.Send(
new UpdateOwnNames(userId, input), ct);
return Results.Ok(new { data = saved });
})
.RequireAuthorization("AccessApi");
}
}