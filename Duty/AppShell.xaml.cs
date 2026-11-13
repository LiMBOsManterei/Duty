using Microsoft.Extensions.DependencyInjection;

namespace Duty
{
    public partial class AppShell : Shell
    {
        public AppShell(IServiceProvider services)
        {
            InitializeComponent();

            Items.Add(new ShellContent
            {
                Title = "Главная",
                Route = "MainPage",
                Content = services.GetRequiredService<MainPage>(),
            });
        }
    }
}
