using MudBlazor;
using MudBlazor.Services;

namespace ReusableAccess.Web.Ui;

public static class UiRegistration
{
    public static IServiceCollection AddReusableAccessUi(
        this IServiceCollection services)
    {
        services.AddMudServices(options =>
        {
            var snackbar = options.SnackbarConfiguration;
            snackbar.PositionClass = Defaults.Classes.Position.BottomLeft;
            snackbar.PreventDuplicates = true;
            snackbar.NewestOnTop = true;
            snackbar.MaxDisplayedSnackbars = 3;
            snackbar.VisibleStateDuration = 5000;
            snackbar.ShowCloseIcon = true;
            snackbar.ClearAfterNavigation = false;
        });

        services.AddScoped<AccessCatalogService>();
        return services;
    }
}