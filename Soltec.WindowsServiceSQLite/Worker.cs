using AppBLL;
using AppCommon.Entities;
using Soltec.Common.LoggerFramework;
using Soltec.WindowsServiceSQLite.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Reflection;
using System.ServiceProcess;

namespace Soltec.WindowsServiceSQLite
{
    public class Worker : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly List<Task> _backgroundTasks = new();
        private readonly List<CancellationTokenSource> _cancellationTokens = new();
        private static string ConfigApiUrl;
        //public static string urlAPI = string.Empty;
        public static int TiempoEjecutaApp = 10;

        public static List<SPOS_SQLScripts> scriptsList = new();
        public static List<SPOS_SQLScripts> scriptsList2 = new();
        //private static readonly Random _rand = new Random();
        //private static readonly Random _rand2 = new Random();
        private static string Sucursal = string.Empty;
        public Worker(IConfiguration configuration)
        {
            _configuration = configuration;

            ConfigApiUrl = _configuration["AppConfig:ApiUrl"];
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            FileUtil.localLogPath = @"C:\sfspos\Soltec.WindowsServiceSQLite\Logs\";
            Logger.Important($"Iniciando servicio: WindowsServiceSQLite {Assembly.GetExecutingAssembly().GetName().Version}");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    //await CreaServicioWindowsServiceOnLine(@"C:\sfspos\Soltec.WindowsServiceOnLine\Soltec.WindowsServiceOnLine.exe");

                    //CreateEnvironmentVariable();

                    var filePathSucursalObtenida = @"C:\sfspos\serviceApp\SucursalObtenida.txt";
                    var existeSucursal = System.IO.File.Exists(filePathSucursalObtenida);

                    if (existeSucursal)
                    {
                        Sucursal = System.IO.File.ReadAllText(filePathSucursalObtenida);
                    }

                    try
                    {
                        LaunchTask(token => RunTaskScriptsAsync(token), "Scripts Online", 10);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
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

                        //await taskFunc(token);
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

        //private async Task CreateEnvironmentVariable()
        //{
        //    const string newPathEntry = @"C:\Sfspos\Soltec.WindowsServiceSQLite\SQLite";
        //    if (!System.IO.File.Exists(newPathEntry + "\\sqlite3.exe"))
        //        return;

        //    try
        //    {
        //        string? pathVar = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);

        //        if (string.IsNullOrEmpty(pathVar))
        //        {
        //            pathVar = newPathEntry;
        //        }
        //        else if (pathVar.IndexOf(newPathEntry, StringComparison.OrdinalIgnoreCase) < 0)
        //        {
        //            pathVar += ";" + newPathEntry;
        //            Environment.SetEnvironmentVariable("PATH", pathVar, EnvironmentVariableTarget.Machine);
        //            Logger.Important("Ruta agregada al PATH del sistema.");
        //            await CreateDatabase();
        //        }
        //        else
        //        {
        //            Logger.Info("La ruta ya está en el PATH.");
        //            await CreateDatabase();
        //            return;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error("Error al modificar la variable de entorno: " + ex.Message);
        //    }
        //}

        //public static async Task CreateDatabase()
        //{
        //    string folderPath = @"C:\Sfspos\Soltec.WindowsServiceSQLite\SQLite\DB";
        //    string dbFilePath = Path.Combine(folderPath, "DBSoltec.db");

        //    try
        //    {
        //        // Crear el directorio si no existe
        //        if (!Directory.Exists(folderPath))
        //            Directory.CreateDirectory(folderPath);

        //        // Verificar si ya existe
        //        if (!File.Exists(dbFilePath))
        //        {
        //            SQLiteConnection.CreateFile(dbFilePath);
        //            Logger.Info("Base de datos creada en: " + dbFilePath);
        //        }
        //        else
        //        {
        //            Logger.Info("La base de datos ya existe en: " + dbFilePath);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error("Error al crear la base de datos: " + ex.Message);
        //    }
        //}

        //private async Task RunTaskUrlValidateAsync(CancellationToken token)
        //{
        //    Logger.Important("Validando URL del Servicio");
        //    //await SetURLAsync();
        //}

        public static async Task<string> GetSucursalServicio()
        {
            var getSucusalServicio = "";

            var filePathSucursalObtenida = @"C:\sfspos\serviceApp\SucursalObtenida.txt";
            var existFileSucursalObtenida = System.IO.File.Exists(filePathSucursalObtenida);

            if (!existFileSucursalObtenida)
            {
                Logger.Info(@"El archivo SucursalObtenida.txt NO existe en el directorioC:\sfspos\serviceApp\");
            }
            else
            {
                Logger.Info(@"El archivo SucursalObtenida.txt SI existe en el directorio C:\sfspos\serviceApp\ leyendo contenido...");
                getSucusalServicio = System.IO.File.ReadAllText(filePathSucursalObtenida);
            }

            return getSucusalServicio;
        }


        private async Task RunTaskScriptsAsync(CancellationToken token)
        {

            var numeroSucursal = await GetSucursalServicio();
            Logger.Important($"Obteniendo scripts para la sucursal. {numeroSucursal}");
            var result = await RequestAppBLL.GetSQLScriptsSQLite(ConfigApiUrl, numeroSucursal);

            if (result != null)
            {
                if (result.List != null)
                {
                    if (result.List.Count > 0)
                    {
                        Logger.Important($"Se obtuvieron los siguientes scripts {result.List.Count} para la sucursal. {numeroSucursal}");
                        scriptsList = result.List;
                        foreach (var script in scriptsList)
                        {
                            int tiempo = script.TiempoTransmision > 0
                                ? script.TiempoTransmision
                                : TiempoEjecutaApp;


                            LaunchTask(token => ProcesarScriptAsync(token, script), script.Nombre, tiempo);
                        }
                    }
                }
            }
        }

        public static async Task ProcesarScriptAsync(CancellationToken token, SPOS_SQLScripts script)
        {
            try
            {

                if (script != null)
                {
                    if (!string.IsNullOrEmpty(script.ScriptTable))
                    {
                        //if (endProcess2)
                        //{
                        //    endProcess = false;
                        await Common.Data.DataScriptSQLServerToSQLite(script);
                        //endProcess = true;
                        Logger.Important($"Ejecutando script SQL Server a SQLite: {script.Nombre}");
                        //} else
                        //{
                        //    Logger.Important($"Esperando a que termine el proceso - Envío a la BD centralizada");
                        //}
                    }
                    else
                    {
                        //if (endProcess)
                        //{
                        //    endProcess2 = false;
                        Logger.Important($"Ejecutando script para el envío a la BD centralizada: {script.Nombre}");
                        if (!script.MultiplesTablas)
                        {
                            var data = await Common.Data.DataScript(script);
                            var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                            if (jsonString != "[]")
                            {
                                var response = await SoltecAppBLL.ProcesaScriptSQLite(script, jsonString, ConfigApiUrl);
                                if (response)
                                {
                                    //Common.Data.UpdteDataSend(script.Nombre, data, false);
                                    Logger.Info($"Proceso {script.Nombre} ejecutado correctamente.");
                                }
                                else
                                    Logger.Info($"Proceso {script.Nombre} NO se ejecutó correctamente.");
                            }
                            else
                                Logger.Info($"No se encontraron registros {script.Nombre} para este script.");
                        }
                        else
                        {
                            try
                            {
                                var data = await Common.Data.DataScriptMultipleSQLServer(script, Sucursal);
                                var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                                if (data.ToList().Count > 0)
                                {
                                    bool withData = false;
                                    foreach (var table in data)
                                    {
                                        if (table.Value is IEnumerable<object> lista && lista.Any())
                                        {
                                            withData = true;
                                            break;
                                        }
                                    }
                                    if (withData)
                                    {
                                        var response = await SoltecAppBLL.ProcesaScriptSQLiteMultiple(script, ConfigApiUrl, jsonString, "");
                                        if (response)
                                        {
                                            foreach (var table in data)
                                            {
                                                Logger.Info($"Proceso {script.Nombre} ejecutado correctamente. {response}");
                                            }
                                        }
                                        else
                                            Logger.Info($"Proceso {script.Nombre} NO se ejecutó correctamente. {response}");
                                    }
                                    else
                                    {
                                        Logger.Info($"No se encontraron registros {script.Nombre} para este script.");
                                    }
                                }
                                else
                                    Logger.Info($"No se encontraron registros {script.Nombre} para este script.");

                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Se encontró un error al procesar su script SQLite: {ex.Message}");

                                var response = await SoltecAppBLL.ProcesaScriptSQLiteMultiple(script, ConfigApiUrl, "", ex.Message);
                                if (response)
                                {
                                    Logger.Info($"Proceso {script.Nombre} ejecutado correctamente.");
                                }
                                else
                                    Logger.Info($"Proceso {script.Nombre} NO se ejecutó correctamente.");
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

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.Important("Deteniendo servicio y tareas...");
            foreach (var cts in _cancellationTokens)
                cts.Cancel();

            await Task.WhenAll(_backgroundTasks);
            Logger.Important("Servicio detenido.");
        }

        //private async Task SetURLAsync()
        //{
        //    try
        //    {
        //        if (ConfigApiUrl.Contains("|"))
        //        {
        //            var urls = ConfigApiUrl.Split('|').ToList();

        //            // Mezcla aleatoriamente usando instancia estática
        //            urls = urls.OrderBy(x => _rand2.Next()).ToList();

        //            foreach (var url in urls)
        //            {
        //                try
        //                {
        //                    var isOnline = await RequestAppBLL.IsOnline(url);
        //                    if (isOnline.Success)
        //                    {
        //                        Logger.Important($"URL ACTIVA SELECCIONADA: {url}");
        //                        urlAPI = url;
        //                        return;
        //                    }
        //                    else
        //                    {
        //                        Logger.Warning($"URL inactiva: {url}");
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    Logger.Error($"Error al verificar URL {url}: {ex.Message}");
        //                }
        //            }

        //            Logger.Error("Ninguna URL activa fue encontrada.");
        //        }
        //        else
        //        {
        //            urlAPI = ConfigApiUrl;
        //            Logger.Info($"Usando única URL configurada: {urlAPI}");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error($"Error en SetURLAsync: {ex.Message}");
        //    }
        //}

        private bool ItPermittedSchedule()
        {
            var currentTime = DateTime.Now.TimeOfDay;
            var startTime = new TimeSpan(6, 0, 0);   // 6:00 AM
            var endTime = new TimeSpan(23, 59, 0);     // 11:00 PM
            return currentTime >= startTime && currentTime <= endTime;
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

    }
}
