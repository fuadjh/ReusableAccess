using ReusableAccess.Api.Endpoints;

namespace ReusableAccess.Api;

public static class ApiPipelineExtensions
{
    public static WebApplication UseReusableAccessApi(
        this WebApplication app)
    {
        app.MapAccessProfilesEndpoint();
        app.MapAdminRolePermissionsEndpoint();
        app.MapAdminRolesEndpoints();
        app.MapWorkItemsEndpoint();

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.MapAdminScopesEndpoints();
        app.MapAdminBundlesEndpoints();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapRazorPages();

        return app;
    }
}