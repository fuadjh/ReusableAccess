using MudBlazor;

namespace ReusableAccess.Web.Ui;

public static class AppTheme
{
    // نمونهٔ مستقل برای هر Layout؛ تنظیمات ظاهری با نشست احراز هویت مخلوط نمی‌شوند.
    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#8559DC",
            Secondary = "#329BAE",
            Background = "#EEF0FB",
            Surface = "#FAF9FE",
            TextPrimary = "#28233C",
            TextSecondary = "#68647C",
            AppbarBackground = "#F8F7FD",
            AppbarText = "#28233C",
            DrawerBackground = "#F8F7FD",
            DrawerText = "#28233C"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#BC99FF",
            Secondary = "#7CD3DD",
            Background = "#111426",
            Surface = "#1C1F37",
            TextPrimary = "#EEEAF9",
            TextSecondary = "#B4AEC8",
            AppbarBackground = "#1C1F37",
            AppbarText = "#EEEAF9",
            DrawerBackground = "#1C1F37",
            DrawerText = "#EEEAF9"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Vazirmatn", "Tahoma", "sans-serif"],
                FontSize = ".9rem",
                LineHeight = "1.85",
                LetterSpacing = "0"
            },
            H5 = new H5Typography
            {
                FontSize = "1.5rem",
                FontWeight = "700",
                LetterSpacing = "0",
                LineHeight = "1.7"
            },
            H6 = new H6Typography
            {
                FontSize = "1.1rem",
                FontWeight = "700",
                LetterSpacing = "0",
                LineHeight = "1.8"
            },
            Button = new ButtonTypography
            {
                FontWeight = "700",
                LetterSpacing = "0",
                TextTransform = "none"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "16px",
            DrawerWidthRight = "250px"
        }
    };
}