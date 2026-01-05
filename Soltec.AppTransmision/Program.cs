using AppBLL;
using Soltec.Common.LoggerFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Soltec.AppTransmision
{
	class Program
	{
        public static string urlAPI = string.Empty;


        static async Task Main(string[] args)
		{
			FileUtil.localLogPath = @"C:\sfspos\serviceApp\Logs\";
			Logger.Info($"Ejecutando aplicacion");
			try
			{

				Logger.Info($"Iniciando proceso");

                await SetURLAsync();
                await SoltecAppBLL.StartupApp(urlAPI);

				await EjecutarProcesoAsync();

				await CreaServicioWindowsServiceOnLine(@"C:\sfspos\Soltec.WindowsServiceOnLine\Soltec.WindowsServiceOnLine.exe");
				await CreaServicioWindowsServiceAppTransmision(@"C:\sfspos\Soltec.WindowsServiceApp\Soltec.WindowsServiceApp.exe");
                await CreaServicioWindowsServiceSQLite(@"C:\sfspos\Soltec.WindowsServiceSQLite\Soltec.WindowsServiceSQLite.exe");
            }
			catch (Exception ex)
			{
				Logger.Error($"Ocurrio un error en la aplicacion {ex.Message}");
			}
		}

        static async Task SetURLAsync()
        {
            try
            {

                Random _rand2 = new Random();
                if (Settings1.Default.apiUrl.Contains("|"))
                {
                    var urls = Settings1.Default.apiUrl.Split('|').ToList();

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
                    urlAPI = Settings1.Default.apiUrl;
                    Logger.Info($"Usando única URL configurada: {urlAPI}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en SetURLAsync: {ex.Message}");
            }
        }

        //      public async static Task<bool> CreaServicioWindowsServiceOnLine(string appPath)
        //{
        //	var fileName = @"C:\sfspos\Soltec.WindowsServiceOnLine\Soltec.WindowsServiceOnLine.exe";
        //	var serviceName = "Soltec_OnLineSales";
        //	if (File.Exists(fileName))
        //	{
        //		ServiceController servicio = ServiceController.GetServices()
        //			.FirstOrDefault(s => s.ServiceName == serviceName);

        //		if (servicio != null)
        //		{
        //			Logger.Info($"Estado actual del servicio {serviceName}: {servicio.Status}");
        //			if (servicio.Status == ServiceControllerStatus.Stopped)
        //			{
        //				try
        //				{
        //					Logger.Info($"Iniciando servicio {serviceName}...");
        //					servicio.Start();
        //					servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
        //					Logger.Info($"Servicio {serviceName} iniciado con éxito.");
        //				}
        //				catch (Exception ex)
        //				{
        //					Logger.Error($"Error al iniciar el servicio {serviceName}: " + ex.Message);
        //				}
        //			}
        //			else
        //			{
        //				Logger.Info($"El servicio {serviceName} ya está en ejecución.");
        //			}
        //		}

        //		if (servicio == null)
        //		{
        //			await EjecutarComandoAsync($"create {serviceName} binPath= \"{appPath}\" start= auto");
        //			Logger.Important($"Se creó el servicio de windows: {serviceName} correctamente.");
        //			// Iniciar el servicio
        //			await EjecutarComandoAsync($"start {serviceName}");
        //			Logger.Important($"Se inició el servicio de windows: {serviceName} correctamente.");
        //		}

        //	}

        //	return true;
        //}

        public async static Task<bool> CreaServicioWindowsServiceOnLine(string appPath)
        {
            var fileName = @"C:\sfspos\Soltec.WindowsServiceOnLine\Soltec.WindowsServiceOnLine.exe";
            var serviceName = "Soltec_OnLineSales";

            if (!File.Exists(fileName))
            {
                Logger.Error($"El archivo del servicio no existe: {fileName}");
                return false;
            }

            // 🧠 Verificar si el proceso .exe está corriendo aunque el servicio esté detenido
            try
            {
                string exeName = Path.GetFileNameWithoutExtension(fileName);
                var procesos = Process.GetProcessesByName(exeName);

                if (procesos.Length > 0)
                {
                    Logger.Warning($"⚠️ El proceso '{exeName}.exe' sigue activo aunque el servicio pueda no estarlo.");
                    foreach (var proc in procesos)
                    {
                        try
                        {
                            Logger.Warning($"⛔ Terminando proceso PID={proc.Id}...");
                            proc.Kill();
                            proc.WaitForExit(5000);
                            Logger.Info($"✅ Proceso finalizado: PID={proc.Id}");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error al finalizar proceso PID={proc.Id}: {ex.Message}");
                        }
                    }
                }

                var servicio = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == serviceName);

                if (servicio == null)
                {
                    Logger.Warning($"El servicio {serviceName} no existe. Creándolo...");
                    await EjecutarComandoAsync($"create {serviceName} binPath= \"{appPath}\" start= auto");
                    Logger.Important($"✅ Servicio {serviceName} creado correctamente.");

                    await EjecutarComandoAsync($"start {serviceName}");
                    Logger.Important($"✅ Servicio {serviceName} iniciado correctamente.");
                    return true;
                }

                servicio.Refresh();
                Logger.Info($"Estado actual del servicio {serviceName}: {servicio.Status}");

                if (servicio.Status == ServiceControllerStatus.Running)
                {
                    Logger.Info($"El servicio {serviceName} está en ejecución. Reiniciando...");
                    servicio.Stop();
                    servicio.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));

                    servicio.Start();
                    servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    Logger.Important($"✅ Servicio {serviceName} reiniciado correctamente.");
                }
                else if (servicio.Status == ServiceControllerStatus.Stopped)
                {
                    Logger.Info($"El servicio {serviceName} está detenido. Iniciando...");
                    servicio.Start();
                    servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    Logger.Important($"✅ Servicio {serviceName} iniciado correctamente.");
                }
                else if (servicio.Status == ServiceControllerStatus.StartPending || servicio.Status == ServiceControllerStatus.StopPending)
                {
                    Logger.Warning($"⏳ El servicio {serviceName} está en estado {servicio.Status}. Esperando 10 segundos...");
                    await Task.Delay(10000);
                    servicio.Refresh();

                    if (servicio.Status != ServiceControllerStatus.Running)
                    {
                        Logger.Warning($"⚠️ El servicio no respondió. Intentando reinicio forzado...");
                        await EjecutarComandoAsync($"stop {serviceName}");
                        await Task.Delay(3000);
                        await EjecutarComandoAsync($"start {serviceName}");
                        Logger.Important($"✅ Reinicio forzado del servicio {serviceName} ejecutado.");
                    }
                }
                else
                {
                    Logger.Warning($"Estado inesperado del servicio: {servicio.Status}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"❌ Error al gestionar el servicio {serviceName}: {ex.Message}");
                return false;
            }

            return true;
        }


        private static bool IsServiceStuck(ServiceController servicio)
        {
            try
            {
                servicio.Refresh();
                return servicio.Status == ServiceControllerStatus.StartPending || servicio.Status == ServiceControllerStatus.StopPending;
            }
            catch
            {
                return false;
            }
        }

        public async static Task<bool> CreaServicioWindowsServiceAppTransmision(string appPath)
		{
			var fileName = @"C:\sfspos\Soltec.WindowsServiceApp\Soltec.WindowsServiceApp.exe";
			var serviceName = "Soltec_AppTransmision";
			if (File.Exists(fileName))
			{
				ServiceController servicio = ServiceController.GetServices()
					.FirstOrDefault(s => s.ServiceName == serviceName);

				if (servicio != null)
				{
					Logger.Info($"Estado actual del servicio: {servicio.Status}");
					if (servicio.Status == ServiceControllerStatus.Stopped)
					{
						try
						{
							Logger.Info($"Iniciando servicio {serviceName}...");
							servicio.Start();
							servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
							Logger.Info($"Servicio {serviceName} iniciado con éxito.");
						}
						catch (Exception ex)
						{
							Logger.Error($"Error al iniciar el servicio {serviceName}: " + ex.Message);
						}
					}
					else
					{
						Logger.Info($"El servicio ya está en ejecución {serviceName}.");
					}
				}

				if (servicio == null)
				{
					await EjecutarComandoAsync($"create {serviceName} binPath= \"{appPath}\" start= auto");
					Logger.Important($"Se creó el servicio de windows: {serviceName} correctamente.");
					// Iniciar el servicio
					await EjecutarComandoAsync($"start {serviceName}");
					Logger.Important($"Se inició el servicio de windows: {serviceName} correctamente.");
				}

			}

			return true;
		}

        public async static Task<bool> CreaServicioWindowsServiceSQLite(string appPath)
        {
            var fileName = @"C:\sfspos\Soltec.WindowsServiceSQLite\Soltec.WindowsServiceSQLite.exe";
            var serviceName = "Soltec_ServiceSQLite";

            if (!File.Exists(fileName))
            {
                Logger.Warning("No se encontró el ejecutable del servicio.");
                return false;
            }

            // Verificar si el proceso aún está corriendo aunque el servicio esté detenido
            string exeName = Path.GetFileNameWithoutExtension(fileName);
            var procesos = Process.GetProcessesByName(exeName);

            if (procesos.Length > 0)
            {
                Logger.Warning($"⚠️ El proceso '{exeName}.exe' sigue activo aunque el servicio pueda estar detenido.");
                foreach (var proc in procesos)
                {
                    try
                    {
                        Logger.Warning($"⛔ Terminando proceso PID={proc.Id}...");
                        proc.Kill();
                        proc.WaitForExit(5000);
                        Logger.Info($"✅ Proceso terminado: {proc.Id}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error al finalizar el proceso {proc.Id}: {ex.Message}");
                    }
                }
            }

            var servicio = ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName == serviceName);

            if (servicio != null)
            {
                servicio.Refresh();
                Logger.Info($"Estado actual del servicio '{serviceName}': {servicio.Status}");

                try
                {
                    if (servicio.Status == ServiceControllerStatus.Stopped)
                    {
                        Logger.Info("El servicio está detenido. Iniciando...");
                        servicio.Start();
                        servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                        Logger.Info("Servicio iniciado correctamente.");
                    }
                    else if (servicio.Status == ServiceControllerStatus.Running)
                    {
                        Logger.Info("El servicio ya está en ejecución. Reiniciando...");
                        servicio.Stop();
                        servicio.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));

                        servicio.Start();
                        servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                        Logger.Info("Servicio reiniciado correctamente.");
                    }
                    else
                    {
                        Logger.Warning($"El servicio está en estado inesperado: {servicio.Status}. Intentando reiniciar...");
                        servicio.Stop();
                        servicio.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));

                        servicio.Start();
                        servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                        Logger.Info("Servicio reiniciado desde estado inesperado.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error al manejar el servicio '{serviceName}': {ex.Message}");
                    return false;
                }
            }
            else
            {
                Logger.Info("El servicio no existe. Creándolo...");
                await EjecutarComandoAsync($"create {serviceName} binPath= \"{appPath}\" start= auto");
                Logger.Important($"Servicio creado correctamente: {serviceName}");

                await EjecutarComandoAsync($"start {serviceName}");
                Logger.Important($"Servicio iniciado correctamente: {serviceName}");
            }

            return true;
        }



        static async Task EjecutarComandoAsync(string argumentos)
		{
			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = "sc",
				Arguments = argumentos,
				Verb = "runas", // Ejecutar como administrador
				UseShellExecute = true,
				WindowStyle = ProcessWindowStyle.Hidden
			};

			try
			{
				Process.Start(psi);
				//using (var proceso = Process.Start(psi))
				//{
				//	// No esperes al proceso, porque "sc start" es muy rápido
				//	await Task.Delay(1000);
				//}
			}
			catch (Exception ex)
			{
				Logger.Error($"Error ejecutando '{argumentos}': {ex.Message}");
			}
		}


		public static async Task<bool> EjecutarProcesoAsync()
		{

				Logger.Info("Iniciando proceso de actualizacion para paquetes zip descargados.");

				try
				{
					var existZipFiles = Directory.GetFiles(@"C:\sfspos\serviceUpdate", "*.zip");
					Logger.Info($"Se encontraron {existZipFiles.Count()} archivo(s) zip para descomprimir.");

					if (existZipFiles.Length > 0)
					{
						Logger.Info("Se actualizaron los Zips descargados y se ejecutó el Cron.");

						var psi = new ProcessStartInfo
						{
							FileName = @"C:\sfspos\serviceUpdate\ReiniciaServicioTransmision.bat",
							UseShellExecute = false,     // Necesario para usar 'runas'
							Verb = "runas",             // Esto ejecuta como administrador
							WindowStyle = ProcessWindowStyle.Hidden,
						};

						try
						{
							Process.Start(psi);
						}
						catch (System.ComponentModel.Win32Exception ex)
						{
							Logger.Error("Error o acceso denegado: " + ex.Message);
						}

					}
					else
					{
						Logger.Info("No se encontraron archivos zip para una actualizacion.");
					}
				}
				catch (Exception ex)
				{
					Logger.Error($"Ocurrió un error al realizar la actualizacion de los sistemas. {ex.Message}");
				}

			return true;
		
	}
	}
}
