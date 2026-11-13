using Duty.Services;
using Duty.ViewModels;
using Microsoft.Extensions.Logging;
#if WINDOWS
using Microsoft.Maui.Handlers;
#endif

namespace Duty
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
#if WINDOWS
            RegisterWindowsFontMappings();
#endif

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("fa-solid-900.ttf", "FontAwesome");
                })
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
                    // Убираем стандартную рамку WinUI у Entry: поля рисуются внутри Border.
                    Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
                        "DutyBorderlessEntry",
                        (handler, _) =>
                        {
                            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                            handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
                            handler.PlatformView.Background = null;
                        });
#endif
                });

            builder.Services.AddSingleton<IDutyDataService>(_ => new JsonDutyDataService());
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddSingleton<AppShell>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

#if WINDOWS
        private const string FontAwesomeAlias = "FontAwesome";
        private const string FontAwesomeFile = "fa-solid-900.ttf";
        private const string FontAwesomeFamilyName = "Font Awesome 6 Free";

        private static readonly Microsoft.UI.Xaml.Media.FontFamily WindowsFontAwesome =
            new($"{FontAwesomeFile}#{FontAwesomeFamilyName}");

        /// <summary>
        /// В unpackaged-приложении Windows MAUI не подставляет alias FontAwesome.
        /// Указываем WinUI файл шрифта напрямую: "file.ttf#FamilyName".
        /// </summary>
        private static void RegisterWindowsFontMappings()
        {
            LabelHandler.Mapper.AppendToMapping("DutyFontAwesomeLabel", (handler, view) =>
            {
                if (view is Label label
                    && UsesFontAwesome(label.FontFamily)
                    && handler.PlatformView is Microsoft.UI.Xaml.Controls.TextBlock platformLabel)
                {
                    platformLabel.FontFamily = WindowsFontAwesome;
                }
            });

            ButtonHandler.Mapper.AppendToMapping("DutyFontAwesomeButton", (handler, view) =>
            {
                if (view is Button button
                    && UsesFontAwesome(button.FontFamily)
                    && handler.PlatformView is Microsoft.UI.Xaml.Controls.Button platformButton)
                {
                    platformButton.FontFamily = WindowsFontAwesome;
                }
            });
        }

        private static bool UsesFontAwesome(string? family) =>
            string.Equals(family, FontAwesomeAlias, StringComparison.OrdinalIgnoreCase)
            || string.Equals(family, FontAwesomeFamilyName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(family, "Font Awesome 6 Free Solid", StringComparison.OrdinalIgnoreCase);
#endif
    }
}
