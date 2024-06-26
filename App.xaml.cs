using HNice.Model;
using HNice.Service;
using HNice.View;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Windows;

namespace HNice
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    //logging.AddFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log", "app.log"));
                })
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(context, services);
                })
                .Build();
        }

        private void ConfigureServices(HostBuilderContext context, IServiceCollection services) 
        {
            services.AddSingleton<ITcpInterceptorWorker, TcpInterceptorWorker>();
            services.AddSingleton<IPacketSplitter, PacketSplitter>();
            services.AddSingleton<MainWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e) 
        {
            await _host.StartAsync();
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            using (_host) 
            {
                await _host.StopAsync();
            }
            base.OnExit(e);
        }
    }

}
