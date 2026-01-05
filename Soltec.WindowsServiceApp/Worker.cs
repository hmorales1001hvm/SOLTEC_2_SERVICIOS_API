using BLL;
using Common.Api;
using Common.Entities;
using Newtonsoft.Json;
using System.Net.Http;
using Soltec.Common.LoggerFramework;
using System.Diagnostics;
using System.Reflection;
using AppBLL;
using static Dapper.SqlMapper;

namespace Soltec.WindowsServiceApp
{
    public class Worker : BackgroundService
    {

        public IConfiguration Configuration;
        public static int TiempoEjecutaApp = 300000;
        public static string urlAPI = string.Empty;
        public static string ConfigApiUrl;
        public Worker(IConfiguration configuration)
        {
            Configuration = configuration;
            ConfigApiUrl = Configuration.GetSection("AppConfig").GetSection("ApiUrl").Value;

        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            FileUtil.localLogPath = @"C:\sfspos\Soltec.WindowsServiceApp\Logs\";
            Logger.Important($"Iniciando servicio: Soltec.WindowsServiceApp {Assembly.GetExecutingAssembly().GetName().Version}");

            while (!cancellationToken.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;
                try
                {
                    Logger.Important($"Ejecutando aplicación Soltec.AppTransmision.exe");
                    //await GetProcessTime();
                    await ExecuteAplication();
                    await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al ejecutar el servicio de windows: {ex.Message}");
                    continue;
                }
            }
        }

        public async static Task GetProcessTime()
        {
            try
            {

                Logger.Info("Obteniendo parametros de la sucursal para la transmisión.");
                await SetURLAsync();
                var tiempoEjecutaApp = RequestAppBLL.GetConfiguracionAppTransmision(urlAPI).Result;
                if (tiempoEjecutaApp != 0)
                {
                    TiempoEjecutaApp = tiempoEjecutaApp;
                }
            }
            catch (Exception ex)
            {

            }
        }

        static async Task SetURLAsync()
        {
            try
            {
                Random _rand2 = new Random();
                if (ConfigApiUrl.Contains("|"))
                {
                    var urls = ConfigApiUrl.Split('|').ToList();

                    // Mezcla aleatoriamente usando instancia estática
                    urls = urls.OrderBy(x => _rand2.Next()).ToList();

                    foreach (var url in urls)
                    {
                        try
                        {
                            var isOnline = await RequestAppBLL.IsOnline(url);
                            if (isOnline.Success)
                            {
                                Logger.Important($"URL ACTIVA SELECCIONADA: {url}");
                                urlAPI = url;
                                return;
                            }
                            else
                            {
                                Logger.Warning($"URL inactiva: {url}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error al verificar URL {url}: {ex.Message}");
                        }
                    }

                    Logger.Error("Ninguna URL activa fue encontrada.");
                }
                else
                {
                    urlAPI = ConfigApiUrl;
                    Logger.Info($"Usando única URL configurada: {urlAPI}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en SetURLAsync: {ex.Message}");
            }
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }
        static bool IsProcessRunning(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }
        public async static Task ExecuteAplication()
        {
            await OpenAplication(@"C:\sfspos\serviceApp\Soltec.AppTransmision.exe", "Soltec.AppTransmision");
        }

        public async static Task<bool> OpenAplication(string appPath, string nameExe)
        {
            Logger.Info($"Ejecutando aplicación {appPath}.");

            // Verifica si ya está corriendo
            if (IsProcessRunning(nameExe))
            {
                Logger.Info($"La aplicación {appPath} ya está en ejecución.");
                return true;
            }

            // Verifica que exista el archivo
            if (!System.IO.File.Exists(appPath))
            {
                Logger.Warning($"❌ No se encontró la aplicación {appPath}");
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = appPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };

                Process.Start(psi);
                Logger.Info($"✅ Aplicación iniciada en modo administrador {appPath}.");
            }
            catch (Exception ex)
            {
                Logger.Error($"⚠️ Error al iniciar la aplicación: {appPath} \n{ex.Message}");
            }

            return true;
        }

    }
}
