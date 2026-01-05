using AppBLL;
using AppCommon.Entities;
using BLL;
using Soltec.Common.LoggerFramework;
using System.Diagnostics;

//using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.ServiceProcess;

namespace Soltec.WindowsServiceOnLine
{
    public class Worker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly List<Task> _backgroundTasks = new();
        private readonly List<CancellationTokenSource> _cancellationTokens = new();

        private string ConfigApiUrl;
        private string RepositoryO;
        private string RepositoryC;
        private string StartPath;
        private string ApiUrlOrquestacion;

        //public static string urlAPI = string.Empty;
        public static int TiempoEjecutaApp = 300000;

        public static List<SPOS_SQLScripts> scriptsList = new();
        public static List<SPOS_SQLScripts> scriptsList2 = new();
        //private static readonly Random _rand = new Random();
        //private static readonly Random _rand2 = new Random();
        public static string Sucursal;
        public Worker(IConfiguration configuration)
        {
            _configuration = configuration;

            ConfigApiUrl = _configuration["AppConfig:ApiUrl"];
            RepositoryO = _configuration["AppConfig:RepositoryO"];
            RepositoryC = _configuration["AppConfig:RepositoryC"];
            StartPath = _configuration["AppConfig:StartPath"];
            ApiUrlOrquestacion = _configuration["AppConfig:ApiUrlOrquestacion"];
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            FileUtil.localLogPath = @"C:\sfspos\Soltec.WindowsServiceOnLine\Logs\";
            Logger.Important($"Iniciando servicio: WindowsServiceOnLine {Assembly.GetExecutingAssembly().GetName().Version}");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    //await CreaServicioWindowsServiceSQLite(@"C:\sfspos\Soltec.WindowsServiceSQLite\Soltec.WindowsServiceSQLite.exe");

                    var filePathSucursalObtenida = @"C:\sfspos\serviceApp\SucursalObtenida.txt";
                    try
                    {
                        var existFileSucursalObtenida = System.IO.File.Exists(filePathSucursalObtenida);

                        if (!existFileSucursalObtenida)
                        {
                            Logger.Info("El archivo SucursalObtenida.txt NO existe - Obteniendo Sucursal...");
                            Sucursal = await WorkerServiceBLL.GetSucursal();

                            if (!string.IsNullOrEmpty(Sucursal))
                            {
                                Logger.Warning($"Guardando sucursal {Sucursal} dentro del archivo SucursalObtenida.txt");
                                System.IO.File.WriteAllText(filePathSucursalObtenida, Sucursal);
                            }
                            else
                            {
                                Sucursal = "Desconocida";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Ocurrió un error {ex.Message}");
                    }


                    //try
                    //{
                    //    await RunTaskProcessTimeAsync(stoppingToken);
                    //    Logger.Important("Procesamiento por sucursal completado una vez. Iniciando el resto de tareas...");
                    //}
                    //catch (Exception ex)
                    //{
                    //    Logger.Error($"Ocurrió un error en RunTaskProcessTimeAsync, error: {ex.Message}");
                    //}

                    try
                    {
                        LaunchTask(token => RunTaskScriptsAsync(token, true), "Scripts Online", 20);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Ocurrió un error en RunTaskScriptsAsync, error: {ex.Message}");
                    }


                    await Task.WhenAny(_backgroundTasks);
                    lock (_backgroundTasks)
                    {
                        _backgroundTasks.RemoveAll(t => t.IsCompleted);
                    }
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                }

            }
            catch (Exception ex)
            {
                Logger.Error($"Error fatal en el servicio: {ex.Message}");
            }
        }


        private void LaunchTask(Func<CancellationToken, Task> taskFunc, string taskName, int minutos)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
            _cancellationTokens.Add(cts);
            var token = cts.Token;

            var task = Task.Run(async () =>
            {
                Logger.Important($"Iniciando tarea: {taskName}");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (ItPermittedSchedule())
                        {
                            await taskFunc(token);
                        }
                        else
                        {
                            Logger.Info($"Tarea '{taskName}' omitida por estar fuera del horario permitido.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error en tarea '{taskName}': {ex.Message}");
                    }

                    await Task.Delay(TimeSpan.FromMinutes(minutos), token);
                }
            }, token);

            _backgroundTasks.Add(task);
        }

        //private async Task RunTaskVersionUpdateAsync(CancellationToken token)
        //{
        //    Logger.Important("Actualizando número de versiones");
        //    //await RunTaskUrlValidateAsync(token);
        //    await SoltecAppBLL.StartUpAppsOnLineAsync(urlAPI);
        //}

        //private async Task RunTaskProcessTimeAsync(CancellationToken token)
        //{
        //    Logger.Important("Procesando transmisión por sucursal");
        //    try
        //    {
        //        TiempoEjecutaApp = await RequestAppBLL.GetConfiguracion(urlAPI);
        //    }
        //    catch (Exception ex)
        //    {
        //        TiempoEjecutaApp = 300000;
        //        Logger.Error($"Error al obtener tiempo de configuración: {ex.Message}");
        //    }
        //}

        private async Task RunTaskScriptsAsync(CancellationToken token, bool online)
        {
            Logger.Important($"Obteniendo scripts (Online={online})");

            var result = await RequestAppBLL.GetSQLScripts(online, ConfigApiUrl);
            if (result != null)
            {
                if (result.List != null)
                {
                    if (result.List.Count > 0)
                    {
                        if (online)
                            scriptsList = result.List;
                        else
                            scriptsList2 = result.List;

                        var list = online ? scriptsList : scriptsList2;

                        foreach (var script in list)
                        {
                            int tiempo = script.TiempoTransmision > 0
                                ? script.TiempoTransmision
                                : (int)TimeSpan.FromMilliseconds(TiempoEjecutaApp).TotalMinutes;

                            if (online)
                            {
                                if (script.MultiplesTablas)
                                {
                                    LaunchTask(token => ProcesarScriptOnlineMultiplesAsync(token, script), script.Nombre, tiempo);
                                }
                                else
                                {
                                    LaunchTask(token => ProcesarScriptOnlineAsync(token, script), script.Nombre, tiempo);
                                }
                            }
                            else
                            {
                                LaunchTask(token => ProcesarScriptOfflineAsync(token, script), script.Nombre, tiempo);
                            }
                        }
                    }
                }
            }
        }

        private async Task ProcesarScriptOnlineAsync(CancellationToken token, SPOS_SQLScripts script)
        {
            Logger.Important($"Ejecutando script online {script.Nombre}");
            await SoltecAppBLL.ProcesaScriptAsync(script, true, ConfigApiUrl);
        }

        private async Task ProcesarScriptOnlineMultiplesAsync(CancellationToken token, SPOS_SQLScripts script)
        {
            Logger.Important($"Ejecutando script online {script.Nombre}");
            await SoltecAppBLL.ProcesarScriptOnlineMultiplesSQLServerAsync(script, ConfigApiUrl);
        }

        private async Task ProcesarScriptOfflineAsync(CancellationToken token, SPOS_SQLScripts script)
        {
            Logger.Important($"Ejecutando script offline {script.Nombre}");
            await SoltecAppBLL.EjecutaScriptsAsync(RepositoryO, RepositoryC, StartPath, ApiUrlOrquestacion, script, false, ConfigApiUrl);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.Important("Deteniendo servicio y tareas...");
            foreach (var cts in _cancellationTokens)
                cts.Cancel();

            await Task.WhenAll(_backgroundTasks);
            Logger.Important("Servicio detenido.");
        }



        private bool ItPermittedSchedule()
        {
            var currentTime = DateTime.Now.TimeOfDay;
            var startTime = new TimeSpan(5, 0, 0);   // 5:00 AM
            var endTime = new TimeSpan(23, 0, 0);     // 11:00 PM
            return currentTime >= startTime && currentTime <= endTime;
        }


        public async static Task<bool> ServiceValidate()
        {
            string nombreServicio = "Soltec_AppTransmision";
            string nombreProceso = "Soltec.WindowsServiceApp";
            string rutaDestino = @"C:\sfspos\Soltec.WindowsServiceApp\Soltec.WindowsServiceApp.exe";
            string rutaNuevoExe = @"C:\sfspos\Soltec.WindowsServiceApp\Soltec.WindowsServiceApp.exe";

            try
            {
                Console.WriteLine("Deteniendo servicio...");
                ServiceController sc = new ServiceController(nombreServicio);

                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                    Console.WriteLine("Servicio detenido.");
                }
                else
                {
                    Console.WriteLine("El servicio ya estaba detenido.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al detener el servicio: " + ex.Message);
            }

            Console.WriteLine("Cerrando proceso colgado (si existe)...");
            foreach (var proc in Process.GetProcessesByName(nombreProceso))
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit();
                    Console.WriteLine("Proceso finalizado.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("No se pudo terminar el proceso: " + ex.Message);
                }
            }

            Thread.Sleep(1000); // Espera opcional

            Console.WriteLine("Reemplazando ejecutable...");
            try
            {
                File.Copy(rutaNuevoExe, rutaDestino, true);
                Console.WriteLine("Archivo reemplazado correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al copiar el nuevo archivo: " + ex.Message);
                return false;
            }

            Console.WriteLine("Iniciando servicio...");
            try
            {
                ServiceController sc = new ServiceController(nombreServicio);
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
                Console.WriteLine("Servicio iniciado correctamente.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al iniciar el servicio: " + ex.Message);
            }

            Console.WriteLine("✅ Actualización completada.");

            return true;
        }


        public async static Task<bool> CreaServicioWindowsServiceAppTransmision(string appPath)
        {
            var fileName = @"C:\sfspos\Soltec.WindowsServiceApp\Soltec.WindowsServiceApp.exe";
            var serviceName = "Soltec_AppTransmision";

            if (!File.Exists(fileName))
            {
                Logger.Error($"El archivo del servicio no existe: {fileName}");
                return false;
            }

            try
            {
                var servicio = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == serviceName);

                if (servicio == null)
                {
                    Logger.Warning($"El servicio {serviceName} no existe. Creándolo...");
                    await EjecutarComandoAsync($"create {serviceName} binPath= \"{appPath}\" start= auto");
                    Logger.Important($"Servicio {serviceName} creado correctamente.");
                    await EjecutarComandoAsync($"start {serviceName}");
                    Logger.Important($"Servicio {serviceName} iniciado correctamente.");
                    return true;
                }

                Logger.Info($"Estado actual del servicio {serviceName}: {servicio.Status}");

                if (servicio.Status == ServiceControllerStatus.Running)
                {
                    // Detecta si está bloqueado (opcional: tiempo excesivo en estado intermedio)
                    if (IsServiceStuck(servicio))
                    {
                        Logger.Warning($"El servicio {serviceName} parece estar bloqueado. Reiniciando...");
                        servicio.Stop();
                        servicio.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        servicio.Start();
                        servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                        Logger.Important($"Servicio {serviceName} reiniciado correctamente.");
                    }
                    else
                    {
                        Logger.Info($"El servicio {serviceName} está en ejecución.");
                    }
                }
                else if (servicio.Status == ServiceControllerStatus.Stopped)
                {
                    Logger.Info($"Iniciando servicio {serviceName}...");
                    servicio.Start();
                    servicio.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    Logger.Important($"Servicio {serviceName} iniciado correctamente.");
                }
                else if (servicio.Status == ServiceControllerStatus.StartPending || servicio.Status == ServiceControllerStatus.StopPending)
                {
                    Logger.Warning($"El servicio {serviceName} está en estado {servicio.Status}, esperando 10 segundos...");
                    await Task.Delay(10000); // Esperar
                    servicio.Refresh();

                    if (servicio.Status != ServiceControllerStatus.Running)
                    {
                        Logger.Warning($"El servicio {serviceName} no respondió. Intentando reinicio forzado...");
                        await EjecutarComandoAsync($"stop {serviceName}");
                        await Task.Delay(3000);
                        await EjecutarComandoAsync($"start {serviceName}");
                        Logger.Important($"Reinicio forzado del servicio {serviceName} ejecutado.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error al gestionar el servicio {serviceName}: {ex.Message}");
                return false;
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
                    Logger.Warning($"El proceso '{exeName}.exe' sigue activo aunque el servicio pueda no estarlo.");
                    foreach (var proc in procesos)
                    {
                        try
                        {
                            Logger.Warning($"Terminando proceso PID={proc.Id}...");
                            proc.Kill();
                            proc.WaitForExit(5000);
                            Logger.Info($"Proceso finalizado: PID={proc.Id}");
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
                    Logger.Important($"Servicio {serviceName} creado correctamente.");

                    await EjecutarComandoAsync($"start {serviceName}");
                    Logger.Important($"Servicio {serviceName} iniciado correctamente.");
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

    }
}
