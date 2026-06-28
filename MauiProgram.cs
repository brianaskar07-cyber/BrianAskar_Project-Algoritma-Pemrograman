using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
namespace FIRE
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if WINDOWS
Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("BorderlessEntry", (handler, view) =>
{
    if (view is not BorderlessEntry)
        return;

    handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
    handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);

    handler.PlatformView.Background =
        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

    handler.PlatformView.BorderBrush =
        new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

    handler.PlatformView.UseSystemFocusVisuals = false;

    handler.PlatformView.FocusVisualPrimaryThickness =
        new Microsoft.UI.Xaml.Thickness(0);

    handler.PlatformView.FocusVisualSecondaryThickness =
        new Microsoft.UI.Xaml.Thickness(0);
});
#endif

            return builder.Build();
        }
    }
}
