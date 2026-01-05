using AppBLL;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json.Linq;
using Soltec.Common.LoggerFramework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Soltec.AppOnLineSales
{
	public class Program
	{
		public static void Main(string[] args)
		{

			FileUtil.localLogPath = @"C:\sfspos\AppOnLineSales\Logs\"; 
			Logger.Info($"Ejecutando aplicacion AppOnLineSales");

			try
			{
				Logger.Info($"Iniciando proceso para Ventas en Linea");
                try
				{
					if (Settings1.Default.apiUrl.Contains("|"))
					{
						foreach (var url in Settings1.Default.apiUrl.Split('|'))
						{
							try
							{
								var isOnline = RequestAppBLL.IsOnline(url).GetAwaiter().GetResult();

								if (isOnline.Success)
								{
									Logger.Info($"La {url} respondió correctamente");
									SoltecAppBLL.StartupAppOnLineSales(url);
									break;
								}
								else
								{
									Logger.Error($"Error: la {url} no se encuentra en linea.");
								}

							}
							catch (Exception ex)
							{
								Logger.Error($"Error: la {url} no se encuentra en linea. {ex.StackTrace}");
							}
						}
					}
					else
					{
						//var url = "http://trasmision.itsoltec.com:8083/api";
						SoltecAppBLL.StartupAppOnLineSales(Settings1.Default.apiUrl);
					}

				}
				catch (Exception ex)
				{
					Logger.Error($"Ocurrió un error al ejecutar el StartupAppOnLineSales. {ex.Message}");
				}

				//// Ejecuta CRON
				try
				{
					ValidaTareas();
					Logger.Info("Se ejecutó correctamente la depuración de tareas programadas.");
				}
				catch (Exception ex)
				{
					Logger.Error($"Ocurrió un error al ejecutar el CRON. {ex.Message}");
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"Ocurrio un error en la aplicacion - {ex.Message}");
			}
		}


		public async static void ValidaTareas()
		{
			string taskName = "App on line sales";

			Logger.Info("✅Reinicia o desbloquea tareas.");
			try
			{
				using (TaskService ts = new TaskService())
				{
					TaskFolder folder = ts.GetFolder(@"\");
					folder.DeleteTask(taskName, false);
					Logger.Info("Tarea Aplicación App On Line Sales ya no existe ya no existe como taréa.");
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"Ocurrió un error al procesar la tarea programada: {ex.Message}");
			}


			// APP TRANSMISION
			try
			{
				taskName = "Service App";

				using (TaskService ts = new TaskService())
				{
					TaskFolder folder = ts.GetFolder(@"\");
					folder.DeleteTask(taskName, false);
					Logger.Info("Tarea Aplicación Service App ya no existe ya no existe como taréa.");
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"Ocurrió un error al procesar la tarea programada: {ex.Message}");
			}

			// CRON
			try
			{
				taskName = "CRON";
				using (TaskService ts = new TaskService())
				{
					TaskFolder folder = ts.GetFolder(@"\");
					folder.DeleteTask(taskName, false);
					Logger.Info("Tarea Aplicación CRON ya no existe ya no existe como taréa.");
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"Ocurrió un error al procesar la tarea programada: {ex.Message}");
			}


			// SFSPOS
			try
			{
				taskName = "Aplicación SFSPOS";
				using (TaskService ts = new TaskService())
				{
					TaskFolder folder = ts.GetFolder(@"\");
					folder.DeleteTask(taskName, false);
					Logger.Info("Tarea Aplicación SFSPOS ya no existe ya no existe como taréa.");
				}
			}
			catch (Exception ex)
			{
				Logger.Error($"Ocurrió un error al procesar la tarea programada: {ex.Message}");
			}
			Logger.Info("✅Termina Reinicio o desbloqueo de tareas..");
		}

		}
}
