
using Soltec.WindowsServiceApp;
using System.Configuration.Internal;

public class Program
{
    static void Main(string[] args)
    {

        CreateHostBuilder(args)
            .Build()
            .Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .UseWindowsService(opt =>
            {
                opt.ServiceName = "Soltec_AppTransmision";
            })
            .ConfigureServices((context, services) =>
            {
                services.AddHostedService<Worker>();
            });

    }

}
