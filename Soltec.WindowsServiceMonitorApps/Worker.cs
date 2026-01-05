using BLL;
using Common.Api;
using Common.Entities;
using Newtonsoft.Json;
using System.Net.Http;
using Soltec.Common.LoggerFramework;
using System.Diagnostics;
using System.Reflection;
using AppBLL;
using System;

namespace Soltec.WindowsServiceApp
{
    public class Worker : BackgroundService
    {

        public IConfiguration Configuration;
        public static int TiempoEjecutaApp = 300000;
        public static string ConfigApiUrl;
        public static string userName;
        public static string password;
        public static string dominio;
        public Worker(IConfiguration configuration)
        {
            Configuration = configuration;

            ConfigApiUrl = Configuration.GetSection("AppConfig").GetSection("ApiUrl").Value;
            userName = Configuration.GetSection("AppConfig").GetSection("userName").Value;
            password = Configuration.GetSection("AppConfig").GetSection("password").Value;
            dominio = Configuration.GetSection("AppConfig").GetSection("dominio").Value;
        }
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            FileUtil.localLogPath = @"C:\sistemas\carga\Soltec.WindowsServiceMonitorApps\Logs\";
            Logger.Important($"Iniciando servicio: Soltec.WindowsServiceMonitorApps {Assembly.GetExecutingAssembly().GetName().Version}");

            while (!cancellationToken.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;
                try
                {
                    Logger.Important($"Ejecutando aplicación Soltec.WindowsServiceMonitorApps.exe");
                    await ValidateOpenAplication();
                    await Task.Delay(TiempoEjecutaApp, cancellationToken); // Espera 30 minutos
                                                                           //}
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al ejecutar el servicio de windows: {ex.Message}");
                    continue;
                }
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

        public async static Task ValidateOpenAplication()
        {
            try
            {
                Logger.Info("Cargando monitor de aplicaciones.");
                var getMonitorApps = RequestAppBLL.GetMonitorDeApps(ConfigApiUrl).Result;
                foreach (var file in getMonitorApps.List)
                {
                    if (IsProcessRunning($"{file.NombreProceso}"))
                    {
                        Logger.Info($"La aplicación {file.NombreEXE} se encuentra en ejecución .");
                    }
                    else
                    {
                        // Verifica que exista el archivo
                        if (!System.IO.File.Exists($"{file.Ruta}{file.NombreEXE}"))
                        {
                            Logger.Warning($"❌ No se encontró la aplicación en: " + $"{file.Ruta}{file.NombreEXE}");
                        } else {
                            try
                            {
                                
                                //var psi = new ProcessStartInfo
                                //{
                                //    FileName = $"{file.Ruta}{file.NombreEXE}",
                                //    UseShellExecute = true,  
                                //    Verb = "runas",           
                                //    WindowStyle = ProcessWindowStyle.Normal,
                                //    CreateNoWindow = false,
                                //};

                                try
                                {
                                    //UserSessionHelper.StartProcessAsActiveUser($"{file.Ruta}{file.NombreEXE}", "");
                                    PrivilegeHelper.EnablePrivilege("SeIncreaseQuotaPrivilege");
                                    PrivilegeHelper.EnablePrivilege("SeAssignPrimaryTokenPrivilege");
                                    var process = InteractiveProcessLauncher.LaunchInActiveSession($"{file.Ruta}{file.NombreEXE}", "");

                                    //Process.Start(psi);
                                    Logger.Info($"La aplicación {file.NombreEXE} se ha iniciado correctamente.");
                                }
                                catch (System.ComponentModel.Win32Exception ex)
                                {
                                    Logger.Error("Error o acceso denegado: " + ex.Message);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"⚠️ Error al iniciar la aplicación {file.NombreEXE}: \n{ex.Message}");
                            } 
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }


        static bool IsProcessRunning(string processName)
        {
            bool running = false;
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                foreach (var p in processes)
                {
                    if (p.ProcessName == processName)
                    {
                        running = true;
                    }
                }
                return running;
            }
            else
            {
                return running;
            }
        }


    }
}
