using AppCommon.Api;
using AppCommon.Entities;
using AppDAL;
using BLL;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Soltec.Common.LoggerFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppBLL
{
    public class SoltecAppBLL
    {
        public static string Sucursal;
        public SoltecAppDAL SoltecAppDAL;
        public static ApiResponse<User> _apiResponse = new ApiResponse<User>();
        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Obtiene token para consumo de los servicios.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ApplicationException"></exception>
        public static async Task<ApiResponse<User>> Login(string apiUrl)
        {
            var _apiUrl = $"{apiUrl}/auth/Login";

            var apiResponse = new ApiResponse<User>();
            HttpResponseMessage response;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();

                response = await client.GetAsync(_apiUrl).ConfigureAwait(false);

                if ((int)response.StatusCode == 503)
                {
                    apiResponse.Success = false;
                    apiResponse.Message = response.ReasonPhrase;

                    return apiResponse;
                }

                apiResponse = JsonConvert.DeserializeObject<ApiResponse<User>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }

            if (!response.IsSuccessStatusCode)
                Logger.Error($"Error al obtener el token. {response.StatusCode}");

            return apiResponse;
        }

        public static async Task StartupApp(string apiUrl)
        {
            try
            {
                await EjecutaSFS();
                string rutaCarpeta = @"C:\sfspos\serviceUpdate";

                try
                {
                    if (Directory.Exists(rutaCarpeta))
                    {
                        string[] archivos = Directory.GetFiles(rutaCarpeta);

                        foreach (string archivo in archivos)
                        {
                            if (archivo.ToUpper().EndsWith(".ZIP") || archivo.ToUpper().EndsWith(".BAT"))
                            {
                                System.IO.File.Delete(archivo);
                                Logger.Info($"Archivo eliminado: {archivo}");
                            }
                        }

                        Logger.Info("Todos los archivos han sido eliminados.");
                    }
                    else
                    {
                        Logger.Info("La carpeta no existe.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info("Error al eliminar archivos: " + ex.Message);
                }

                _apiResponse = Login(apiUrl).Result;
                if (_apiResponse == null)
                    return;

                var getVersionesApp = await RequestAppBLL.GetVersionesApp(_apiResponse.Result.Token, apiUrl);
                Logger.Info($"Se encontro el siguiente listado de versiones {getVersionesApp.List.Count}:\n{JsonConvert.SerializeObject(getVersionesApp.List)}");

                if (getVersionesApp.List == null || getVersionesApp.List.Count == 0)
                {
                    Logger.Warning("No se logro obtener el listado de versiones las aplicaciones.");
                }
                else
                {
                    foreach (var versionApp in getVersionesApp.List)
                    {

                        var isUpdateApp = false;

                        //if (versionApp.NombreSistema == "SoltecCronApp")
                        //    isUpdateApp = await UpdateCron(versionApp);
                        //else
                            isUpdateApp = await IsUpdateApp(versionApp);

                        if (isUpdateApp)
                            await RequestAppBLL.DownloadFileZIP(_apiResponse.Result.Token, apiUrl, versionApp.NombrePaquete, versionApp.PathDestinoPaquete);

                        else
                            Logger.Info($"No se requiere descargar version para la aplicacion: {versionApp.NombreSistema}");
                    }
                    await RequestAppBLL.DownloadFileZIP(_apiResponse.Result.Token, apiUrl, "ReiniciaServicioTransmision.bat", "C:\\sfspos\\serviceUpdate");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error {ex.Message}.");
            }
        }

        //public static async Task EjecutaScripts(string apiUrl, string host, string user, string pwd,
        //                                        string repositoryO, string repositoryC, string _startPath, string _apiUrlOrquestacion)
        //{
        //    var result = false;
        //    try
        //    {
        //        //ExecuteAplication();
        //        Logger.Info("Se inicia proceso para la obtencion de informacion de la venta en la sucursal.");
        //        try
        //        {
        //            await ProcesaScripts(_apiResponse.Result.Token, false, apiUrl, repositoryO, repositoryC, ConfigApiUrl);
        //            await ComprimeAndUploadJSONAsync(_apiResponse.Result.Token, "O", repositoryO, repositoryC, _startPath, _apiUrlOrquestacion,"");
        //            await ComprimeAndUploadJSONAsync(_apiResponse.Result.Token, "C", repositoryO, repositoryC, _startPath, _apiUrlOrquestacion,"");
        //            result = true;
        //        }
        //        catch (Exception ex)
        //        {
        //            Logger.Error($"Ocurrió un error al procesar los scripts. {ex.Message}");
        //            result = false;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error($"Ocurrio un error {ex.Message}.");
        //        result = false;
        //    }
        //}



        public static async Task EjecutaScriptsAsync( string repositoryO, 
                                                string repositoryC, 
                                                string _startPath, 
                                                string _apiUrlOrquestacion,
                                                SPOS_SQLScripts script, bool isOnLine, string ConfigApiUrl)
        {
            var result = false;
            try
            {
                Logger.Info("Se inicia proceso para la obtencion de informacion de la venta en la sucursal.");
                try
                {
                    await ProcesaScript(script, repositoryO, repositoryC, isOnLine, ConfigApiUrl);
                    if (!script.EsCatalogo)
                        await ComprimeAndUploadJSONAsync(_apiResponse.Result.Token, "O", repositoryO, repositoryC, _startPath, _apiUrlOrquestacion, script.Nombre);
                    else
                        await ComprimeAndUploadJSONAsync(_apiResponse.Result.Token, "C", repositoryO, repositoryC, _startPath, _apiUrlOrquestacion, script.Nombre);
                    result = true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al procesar los scripts. {ex.Message}");
                    result = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error {ex.Message}.");
                result = false;
            }
        }


        public async static Task EjecutaSFS()
        {
            try
            {
                var nombreProcesoEjecucion = "sfspos";
                var pathDirectory = @"C:\sfspos\";
                var nombreProcesoActualizador = "sfs.exe";
                var sfsposProceso = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(nombreProcesoEjecucion));
                OpenAplication(@pathDirectory + "" + nombreProcesoActualizador, nombreProcesoEjecucion);

            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrió un error al abrir el SFS.EXE {ex.Message}");
            }
        }

        public async static Task AbrirAplicacionSfspos(string pathDirectory, string nombreProcesoActualizador, string nombreProcesoEjecucion)
        {
            Logger.Info($"Sucursal: {Sucursal} - Inicia proceso para ejecutar la aplicacion {nombreProcesoActualizador}.");

            try
            {
                Logger.Warning($"Sucursal: {Sucursal} - Ejecutando proceso: {pathDirectory}{nombreProcesoActualizador}");
                ProcessHandler.CreateProcessAsUser($"{pathDirectory}{nombreProcesoActualizador}", "");

                Logger.Info($"Sucursal: {Sucursal} - Verifica proceso en ejecucion: {nombreProcesoEjecucion}");
                var procesos = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(nombreProcesoEjecucion));
                if (procesos.Length >= 1)
                {
                    Logger.Warning($"Sucursal: {Sucursal} - La aplicacion {nombreProcesoEjecucion} YA se ha puesto en ejecucion.");
                }
                else
                {
                    Logger.Warning($"Sucursal: {Sucursal} - La aplicacion {nombreProcesoEjecucion} NO se ha puesto en ejecucion.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error sucursal: {Sucursal} - Error al inicar el proceso sfspos");
            }
        }

        public static async Task StartupAppOnLineSales(string apiUrl)
        {
            try
            {
                _apiResponse = Login(apiUrl).Result;
                Logger.Info("Se valida la sucursal en linea y se actualizan versiónes.");
                try
                {
                    await SucursalEnLinea(_apiResponse.Result.Token, apiUrl);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al consumir el API para actualizar la versiones de las Apps de las sucursales");
                }

                try
                {
                    Logger.Info("Se inicia proceso para la obtencion de informacion de la venta en la sucursal en linea.");
                    await ProcesaScripts(_apiResponse.Result.Token, true, apiUrl, "", "", "");

                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al procesar los scripts ON LINE.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un al obtener las versiones {ex.Message}.");
            }
        }


        public static async Task StartUpAppsOnLineAsync(string apiUrl)
        {
            try
            {
                _apiResponse = Login(apiUrl).Result;
                Logger.Info("Se valida la sucursal en linea y se actualizan versiónes.");
                try
                {
                    await SucursalEnLinea(_apiResponse.Result.Token, apiUrl);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrió un error al consumir el API para actualizar la versiones de las Apps de las sucursales");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un al obtener las versiones {ex.Message}.");
            }
        }


        
        public static async Task ProcesaScriptAsync(SPOS_SQLScripts script, bool isOnLine, string ConfigApiUrl)
        {
            try
            {
                Logger.Info($"Procesando el script {script.Nombre} - {script.SQLScript}.");
                await ProcesaScript(script, "", "", isOnLine, ConfigApiUrl);
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un al procesar el script {script.Nombre} {ex.Message}.");
            }
        }

        public static async Task ProcesarScriptOnlineMultiplesSQLServerAsync(SPOS_SQLScripts script, string ConfigApiUrl)
        {
            try
            {
                
                var data = await SoltecAppDAL.DataScriptMultipleSQLServer(script);
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
                    //if (withData)
                    //{
                        var response = await SoltecAppBLL.ProcesaScriptSQLServerMultiple(script, jsonString, ConfigApiUrl, withData);
                        if (response)
                        {
                            foreach (var table in data)
                            {
                                Logger.Info($"Proceso {script.Nombre} ejecutado correctamente.");
                            }
                        }
                        else
                            Logger.Info($"Proceso {script.Nombre} NO se ejecutó correctamente.");
                    //}
                    //else
                    //{
                    //    Logger.Info($"No se encontraron registros {script.Nombre} para este script.");
                    //}
                }
                else
                    Logger.Info($"No se encontraron registros {script.Nombre} para este script.");

            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error al consultar el script: {script.Nombre}\n{script.SQLScript}\n{ex.Message}");
            }
        }


        public static async Task SucursalEnLinea(string token, string _apiUrl)
        {
            string v2 = @"C:\sfspos\serviceApp\Soltec.AppTransmision.exe";
            string v4 = @"C:\sfspos\Soltec.WindowsServiceOnLine\Soltec.WindowsServiceOnLine.exe";
            string v5 = @"C:\sfspos\Soltec.WindowsServiceApp\Soltec.WindowsServiceApp.exe";
            string v6 = @"C:\Sfspos\Soltec.WindowsServiceSQLite\Soltec.WindowsServiceSQLite.exe";

            FileVersionInfo versionInfo1 = null;
            FileVersionInfo versionInfo2 = null;
            FileVersionInfo versionInfo3 = null;
            FileVersionInfo versionInfo4 = null;
            FileVersionInfo versionInfo5 = null;
            FileVersionInfo versionInfo6 = null;

            //if (System.IO.File.Exists(v1))
            //    versionInfo1 = FileVersionInfo.GetVersionInfo(v1);
            if (System.IO.File.Exists(v2))
                versionInfo2 = FileVersionInfo.GetVersionInfo(v2);
            //if (System.IO.File.Exists(v3))
            //    versionInfo3 = FileVersionInfo.GetVersionInfo(v3);
            if (System.IO.File.Exists(v4))
                versionInfo4 = FileVersionInfo.GetVersionInfo(v4);
            if (System.IO.File.Exists(v5))
                versionInfo5 = FileVersionInfo.GetVersionInfo(v5);
            if (System.IO.File.Exists(v6))
                versionInfo6 = FileVersionInfo.GetVersionInfo(v6);

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
                else
                {
                    Logger.Info($"El archivo SucursalObtenida.txt Si existe - leyendo contenido...");
                    var contentFile = System.IO.File.ReadAllText(filePathSucursalObtenida);
                    Logger.Info($"Contenido extraido del archivo SucursalObtenida.txt: {contentFile}");

                    Sucursal = contentFile;
                }

                if (!string.IsNullOrEmpty(Sucursal) && Sucursal != "Desconocida")
                {
                    Logger.Info("Consultando informacion desde el servicio Web API.");
                    var isOnline = await RequestAppBLL.IsOnline(token, _apiUrl);

                    if (isOnline.Success)
                    {
                        var requestMarkOnline = await RequestAppBLL.MarkOnLine(Sucursal,
                            (versionInfo2 == null) ? "" : versionInfo2.FileVersion,
                            (versionInfo1 == null) ? "" : versionInfo1.FileVersion,
                            (versionInfo3 == null) ? "" : versionInfo3.FileVersion,
                            (versionInfo4 == null) ? "" : versionInfo4.FileVersion,
                            (versionInfo5 == null) ? "" : versionInfo5.FileVersion,
                            (versionInfo6 == null) ? "" : versionInfo6.FileVersion,
                            token, _apiUrl);
                        if (requestMarkOnline.Success)
                            Logger.Info($"Sucursal: {Sucursal} - La sucursal se actualizo fecha y hora correctamente.");
                        else
                            Logger.Error($"Sucursal: {Sucursal} - Error: {requestMarkOnline.Message}.");
                    }
                    else
                    {
                        Logger.Error($"Error: {isOnline.Message} - El servicio NO encuentra en linea. Obteniendo procesos de manera local.");

                        //await WorkerServiceBLL.VerificaStatusProcesos(Sucursal, LstLocalServicioProcesos, ServicioConfig);
                    }
                }
                else
                {
                    Logger.Warning($"No se logro obtener la sucursal durante la ejecucion del servicio. Continuando con el proceso. Tomando valores por default");

                    //await WorkerServiceBLL.VerificaStatusProcesos(Sucursal, LstLocalServicioProcesos, ServicioConfig);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error al procesar los servicios ON LINE: {ex.Message}");
            }
        }


        public static async Task ComprimeAndUploadJSONAsync(string token, string type, string repositoryO, string repositoryC, string _startPath, string _apiUrlOrquestacion, string nombre)
        {
            var numeroSucursal = await GetSucursalServicio();
            if (type.ToUpper() == "O")
            {
                if (Directory.Exists($"{repositoryO}"))
                {
                    var zipPath = "";
                    if (string.IsNullOrEmpty(numeroSucursal))
                        numeroSucursal = "00000";

                    if (!string.IsNullOrEmpty(numeroSucursal))
                    {
                        var startFolder = $"{repositoryO}";
                        var startPath = $"{_startPath}";
                        var apiUrlOrquestacion = $"{_apiUrlOrquestacion}";

                        zipPath = $"{numeroSucursal}-Operativas-{nombre}.zip";

                        DirectoryInfo dir = new DirectoryInfo(startFolder);
                        var fileList = dir.GetFiles("*.JSON", SearchOption.AllDirectories);

                        if (fileList.Length > 0)
                        {
                            if (System.IO.File.Exists(startPath + zipPath))
                                System.IO.File.Delete(startPath + zipPath);

                            ZipFile.CreateFromDirectory(startFolder, startPath + zipPath);
                            var upload = await RequestAppBLL.UploadFileZIP(token, startPath + zipPath, apiUrlOrquestacion);
                            if (!upload.Success)
                                Logger.Error($"Ocurrió un error al enviar su archivo: {startPath + zipPath}");
                            else
                                Logger.Important($"Se envió correctamente el archivo: {startPath + zipPath}");
                        }

                        foreach (var file in fileList)
                            System.IO.File.Delete(file.FullName);
                    }
                    else
                    {
                        Logger.Info($"No existe el Número de Sucursal para comprimir los archivos .JSON");
                    }
                }
            }
            else
            {
                if (Directory.Exists($"{repositoryC}"))
                {
                    var zipPath = "";
                    if (string.IsNullOrEmpty(numeroSucursal))
                        numeroSucursal = "00000";

                    if (!string.IsNullOrEmpty(numeroSucursal))
                    {
                        var startFolder = $"{repositoryC}";
                        var startPath = $"{_startPath}";
                        var apiUrlOrquestacion = $"{_apiUrlOrquestacion}";

                        zipPath = $"{numeroSucursal}-Catalogos-{nombre}.zip";

                        DirectoryInfo dir = new DirectoryInfo(startFolder);
                        var fileList = dir.GetFiles("*.JSON", SearchOption.AllDirectories);

                        if (fileList.Length > 0)
                        {
                            if (System.IO.File.Exists(startPath + zipPath))
                                System.IO.File.Delete(startPath + zipPath);

                            ZipFile.CreateFromDirectory(startFolder, startPath + zipPath);
                            var upload = await RequestAppBLL.UploadFileZIP(token, startPath + zipPath, apiUrlOrquestacion);
                            if (!upload.Success)
                                Logger.Error($"Ocurrió un error al enviar su archivo: {startPath + zipPath}");
                            else
                                Logger.Important($"Se envió correctamente el archivo: {startPath + zipPath}");
                        }

                        foreach (var file in fileList)
                            System.IO.File.Delete(file.FullName);
                    }
                    else
                    {
                        Logger.Info($"No existe el Número de Sucursal para comprimir los archivos .JSON");
                    }
                }
            }
        }

        public static async Task<bool> IsUpdateApp(VersionesApp versionApp)
        {
            Logger.Info($"Comparado numero de version para el sistema: {versionApp.NombreSistema}");

            bool isUpdate = false;
            FileVersionInfo versionInfo = null;
            string path = string.Empty;

            if (versionApp.NombreSistema == "AppTransmision")
            {
                path = @"C:\sfspos\serviceApp\Soltec.AppTransmision.exe";

                if (System.IO.File.Exists(path))
                {
                    versionInfo = FileVersionInfo.GetVersionInfo(path);
                }
            }
            else if (versionApp.NombreSistema == "AppOnLineSales")
            {
                path = @"C:\sfspos\AppOnLineSales\Soltec.AppOnLineSales.exe";

                if (System.IO.File.Exists(path))
                {
                    versionInfo = FileVersionInfo.GetVersionInfo(path);
                }
            }
            else if (versionApp.NombreSistema == "WindowsServiceOnLine")
            {
                path = @"C:\sfspos\Soltec.WindowsServiceOnLine\Soltec.WindowsServiceOnLine.exe";

                if (System.IO.File.Exists(path))
                {
                    versionInfo = FileVersionInfo.GetVersionInfo(path);
                }
            }
            else if (versionApp.NombreSistema == "WindowsServiceApp")
            {
                path = @"C:\sfspos\Soltec.WindowsServiceApp\Soltec.WindowsServiceApp.exe";

                if (System.IO.File.Exists(path))
                {
                    versionInfo = FileVersionInfo.GetVersionInfo(path);
                }
            }
            else if (versionApp.NombreSistema == "WindowsServiceSQLite")
            {
                path = @"C:\sfspos\Soltec.WindowsServiceSQLite\Soltec.WindowsServiceSQLite.exe";

                if (System.IO.File.Exists(path))
                {
                    versionInfo = FileVersionInfo.GetVersionInfo(path);
                }
            }
            if (!Directory.Exists(@"C:\sfspos\Soltec.WindowsServiceOnLine"))
                Directory.CreateDirectory(@"C:\sfspos\Soltec.WindowsServiceOnLine");
            if (!Directory.Exists(@"C:\sfspos\Soltec.WindowsServiceApp"))
                Directory.CreateDirectory(@"C:\sfspos\Soltec.WindowsServiceApp");
            if (!Directory.Exists(@"C:\sfspos\serviceUpdate"))
                Directory.CreateDirectory(@"C:\sfspos\serviceUpdate");
            if (!Directory.Exists(@"C:\sfspos\Soltec.WindowsServiceSQLite"))
                Directory.CreateDirectory(@"C:\sfspos\Soltec.WindowsServiceSQLite");

            var version2 = new Version();
            var pathArchivoCompare = string.Empty;
            if (versionInfo != null)
            {
                if (versionApp.NombreSistema == "WindowsServiceOnLine")
                {
                    version2 = new Version(versionInfo.FileVersion);
                    pathArchivoCompare = versionApp.PathArchivoEXE;
                }
                else if (versionApp.NombreSistema == "WindowsServiceApp")
                {
                    version2 = new Version(versionInfo.FileVersion);
                    pathArchivoCompare = versionApp.PathArchivoEXE;
                }
                else if (versionApp.NombreSistema == "WindowsServiceSQLite")
                {
                    version2 = new Version(versionInfo.FileVersion);
                    pathArchivoCompare = versionApp.PathArchivoEXE;
                }
                else
                {
                    if (new Version(versionInfo.FileVersion).Revision >= 15)
                    {
                        version2 = new Version(versionInfo.FileVersion);
                        pathArchivoCompare = versionApp.PathArchivoEXE;
                    }
                    else
                    {
                        var contentJson = System.IO.File.ReadAllText(pathArchivoCompare);
                        var appSettings = JsonConvert.DeserializeObject<AppSettings>(contentJson);
                        pathArchivoCompare = versionApp.PathArchivoConfig;
                        version2 = new Version(appSettings.AppConfig.Version);//version installed in machine
                    }
                }
            }

            if (System.IO.File.Exists(pathArchivoCompare))
            {
                var version1 = new Version(versionApp.VersionSistema);
                var comparison = version2.CompareTo(version1);
                if (comparison > 0)
                {
                    Logger.Info($"La version DB {version1} es mayor que la version instalada {version2}");
                }
                else if (comparison < 0)
                {
                    Logger.Info($"La version DB {version1} es menor que la version instalada {version2}");
                    isUpdate = true;
                }
                else
                {
                    Logger.Info($"La version DB {version1} es igual a la version instalada {version2}");
                }
            }
            else
            {
                Logger.Warning($"El archivo de configuracion para la aplicacion {versionApp.NombreSistema} no existe. Descargando actualizacion.");
                isUpdate = true;
            }

            return isUpdate;
        }

        public static async Task<bool> DescargaNuevaLiberacion(VersionesApp versionApp, string host, string user, string pwd)
        {
            Logger.Warning($"Inicia proceso de descarga para liberacion de sistema: {versionApp.NombreSistema}");
            var pathNombrePaquete = $"{versionApp.PathDestinoPaquete}\\{versionApp.NombrePaquete}";
            var serverArchivo = $"/service/{versionApp.NombrePaquete}";
            var ftpFullpath = host + serverArchivo;

            if (!string.IsNullOrEmpty(pwd))
                pwd = Encoding.UTF8.GetString((Convert.FromBase64String(pwd)));
            else
                throw new Exception("No existe un password");
            using (WebClient request = new WebClient())
            {
                try
                {
                    request.Credentials = new NetworkCredential(user, pwd);
                    byte[] fileData = request.DownloadData(ftpFullpath);

                    using (FileStream file = System.IO.File.Create(pathNombrePaquete))
                    {
                        file.Write(fileData, 0, fileData.Length);
                        file.Close();
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ocurrio un error al descargar el paquete de nueva liberacion. {ex.Message}");
                    return false;
                }
            }
        }

        public static async Task IniciaActualizacion(string host, string user, string pwd, string token, string apiUrl)
        {
            Logger.Info("✅Iniciando proceso de actualizacion para paquetes zip descargados.");

            try
            {
                var existZipFiles = Directory.GetFiles(@"C:\sfspos\serviceUpdate", "*.zip");
                Logger.Info($"Se encontraron {existZipFiles.Count()} archivo(s) zip para descomprimir.");

                if (existZipFiles.Length > 0)
                {
                    Logger.Info("✅Se actualizaron los Zips descargados y se ejecutó el Cron.");

                    var psi = new ProcessStartInfo
                    {
                        FileName = @"C:\sfspos\serviceUpdate\ReiniciaServicioTransmision.bat",
                        UseShellExecute = false,     // Necesario para usar 'runas'
                        Verb = "runas",             // Esto ejecuta como administrador
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
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
        }

        public async static Task ExecuteAplication()
        {
            await OpenAplication(@"C:\sfspos\Soltec.Cron.App\Soltec.Cron.App.exe", "Soltec.Cron.App");
        }

        public async static Task<bool> OpenAplication(string appPath, string nameExe)
        {
            Logger.Info($"INICIA PROCESO PARA EJECUTAR APLICACIONES. {appPath}, nombre del proceso_ {nameExe}");

            // Verifica si ya está corriendo
            if (IsProcessRunning(nameExe))
            {
                Logger.Info($"La aplicación SFSPOS se encontraba en ejecución {appPath}.");
            }
            else
            {
                // Verifica que exista el archivo
                if (!System.IO.File.Exists(appPath))
                {
                    Logger.Warning($"❌ No se encontró la aplicación en: " + appPath);
                    return false;
                }

                try
                {
                    if (appPath.ToUpper().Contains("SFS"))
                    {
                        ProcessHandler.CreateProcessAsUser(appPath, "");
                    }
                    else
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
                }
                catch (Exception ex)
                {
                    //Logger.Error($"⚠️ Error al iniciar la aplicación SFS.exe: {appPath} \n{ex.Message}");
                }
            }

            return true;
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
                        //CloseApplication(processName);
                        //Thread.Sleep(500);
                        ////Process.Start("explorer.exe");
                    }
                }
                return running;
            }
            else
            {
                return running;
            }
        }



        const uint WM_CLOSE = 0x0010;

        static void CloseApplication(string exeName)
        {
            string processName = exeName;
            foreach (var proc in Process.GetProcessesByName(processName))
            {
                PostMessage(proc.MainWindowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static async Task ProcesaScripts(string token, bool isOnLine, string apiUrl, string repositoryO, string repositoryC, string ConfigApiUrl)
        {
            if (string.IsNullOrEmpty(token))
                return;

            var numeroSucursal = await GetSucursalServicio();
            var scripts = await RequestAppBLL.GetSPOS_SQLScripts(apiUrl, token, numeroSucursal, isOnLine);

            if (scripts.List == null)
            {
                Logger.Warning($"No se encontraron querys para ser consultados.");
            }
            else
            {
                Logger.Info($"Se encontraron {scripts.Count} querys para ser consultados.");

                foreach (var script in scripts.List)
                {
                    try
                    {
                        if (!isOnLine)
                        {
                            //script.SQLScript = @"--Productos
                            //					SELECT * FROM Producto;
                            //					--ProcesosTarjeta
                            //					SELECT * FROM ProcesosTarjeta;
                            //					--ClienteTipo
                            //					SELECT * FROM Cliente_Tipo;";
                            if (!script.EsCatalogo)
                            {
                                if (script.MultiplesTablas)
                                {
                                    //var data = await SoltecAppDAL.DataScriptJSON(script.SQLScript, script.ValorIncrementoDecremento);
                                    //var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data, Formatting.Indented);
                                    //if (!Directory.Exists($"{repositoryO}"))
                                    //    Directory.CreateDirectory($"{repositoryO}");
                                    //if (jsonString != "[]")
                                    //    System.IO.File.WriteAllText($"{repositoryO}{script.Nombre}.JSON", jsonString);
                                }
                                else
                                {
                                    var data = await SoltecAppDAL.DataScript(script.SQLScript, script.ValorIncrementoDecremento);
                                    var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                                    if (!Directory.Exists($"{repositoryO}"))
                                        Directory.CreateDirectory($"{repositoryO}");
                                    if (jsonString != "[]")
                                        System.IO.File.WriteAllText($"{repositoryO}{script.Nombre}.JSON", jsonString);
                                }
                            }
                            else
                            {
                                if (script.MultiplesTablas)
                                {
                                    //var data = await SoltecAppDAL.DataScriptJSON(script.SQLScript, script.ValorIncrementoDecremento);
                                    //var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data, Formatting.Indented);
                                    //if (!Directory.Exists($"{repositoryC}"))
                                    //    Directory.CreateDirectory($"{repositoryC}");

                                    //System.IO.File.WriteAllText($"{repositoryC}{script.Nombre}.JSON", jsonString);
                                }
                                else
                                {
                                    var data = await SoltecAppDAL.DataScript(script.SQLScript, script.ValorIncrementoDecremento);
                                    var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                                    if (!Directory.Exists($"{repositoryC}"))
                                        Directory.CreateDirectory($"{repositoryC}");

                                    System.IO.File.WriteAllText($"{repositoryC}{script.Nombre}.JSON", jsonString);
                                }
                            }

                        }
                        else
                        {
                            try
                            {
                                // Ventas o procesos en linea.
                                var data = await SoltecAppDAL.DataScript(script.SQLScript, script.ValorIncrementoDecremento);
                                Logger.Info(script.SQLScript);
                                Logger.Info(Newtonsoft.Json.JsonConvert.SerializeObject(data));

                                var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                                if (jsonString != "[]")
                                {
                                    var response = await RequestAppBLL.OnLineSales(jsonString, script.Nombre, numeroSucursal, ConfigApiUrl);

                                    if (response.Success)
                                        Logger.Info($"El proceso en linea {script.Nombre} se ejecutó correctamente: {numeroSucursal}");
                                    else
                                        Logger.Info($"El proceso en linea {script.Nombre} NO se ejecutó correctamente: {numeroSucursal}");
                                }
                                else
                                    Logger.Info($"No se encontraron registros {script.Nombre} para este script.");
                            }
                            catch (Exception e)
                            {
                                Logger.Error($"Ocurrio un error al consultar el script: {script.Nombre}\n{script.SQLScript}\n{e.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Ocurrio un error al consultar el script: {script.Nombre}\n{script.SQLScript}\n{ex.Message}");
                    }
                }
            }
        }

        public static async Task ProcesaScript(SPOS_SQLScripts script, string repositoryO, string repositoryC, bool isOnLine, string ConfigApiUrl)
        {
            try
            {
                var numeroSucursal = await GetSucursalServicio();
                if (!isOnLine)
                {
                    if (!script.EsCatalogo)
                    {
                        if (script.MultiplesTablas)
                        {
                            //var data = await SoltecAppDAL.DataScriptMultipleSQLServer(script.SQLScript);
                            //var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data, Formatting.Indented);
                            //if (!Directory.Exists($"{repositoryO}"))
                            //    Directory.CreateDirectory($"{repositoryO}");
                            //if (jsonString != "[]")
                            //    System.IO.File.WriteAllText($"{repositoryO}{script.Nombre}.JSON", jsonString);
                        }
                        else
                        {
                            var data = await SoltecAppDAL.DataScript(script.SQLScript, script.ValorIncrementoDecremento);
                            var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data, Formatting.Indented);
                            if (!Directory.Exists($"{repositoryO}"))
                                Directory.CreateDirectory($"{repositoryO}");
                            if (jsonString != "[]")
                                System.IO.File.WriteAllText($"{repositoryO}{script.Nombre}.JSON", jsonString);
                        }
                    }
                    else
                    {
                        if (script.MultiplesTablas)
                        {
                            //var data = await SoltecAppDAL.DataScriptJSON(script.SQLScript, script.ValorIncrementoDecremento);
                            //var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data, Formatting.Indented);
                            //if (!Directory.Exists($"{repositoryC}"))
                            //    Directory.CreateDirectory($"{repositoryC}");

                            //System.IO.File.WriteAllText($"{repositoryC}{script.Nombre}.JSON", jsonString);
                        }
                        else
                        {
                            var data = await SoltecAppDAL.DataScript(script.SQLScript, script.ValorIncrementoDecremento);
                            var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data, Formatting.Indented);
                            if (!Directory.Exists($"{repositoryC}"))
                                Directory.CreateDirectory($"{repositoryC}");

                            System.IO.File.WriteAllText($"{repositoryC}{script.Nombre}.JSON", jsonString);
                        }
                    }
                }
                else
                {
                    try
                    {

                        // Ventas o procesos en linea.
                        var data = await SoltecAppDAL.DataScript(script.SQLScript, script.ValorIncrementoDecremento);
                        Logger.Info(script.SQLScript);
                        Logger.Info(Newtonsoft.Json.JsonConvert.SerializeObject(data));

                        var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                        if (jsonString != "[]")
                        {
                            var response = await RequestAppBLL.OnLineSales(jsonString, script.Nombre, numeroSucursal, ConfigApiUrl);

                            if (response.Success)
                                Logger.Info($"El proceso en linea {script.Nombre} se ejecutó correctamente: {numeroSucursal}");
                            else
                                Logger.Info($"El proceso en linea {script.Nombre} NO se ejecutó correctamente: {numeroSucursal}");
                        }
                        if (jsonString == "[]")
                            Logger.Info($"No se encontraron registros {script.Nombre} para este script, se registro la venta en 0");
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Ocurrio un error al consultar el script: {script.Nombre}\n{script.SQLScript}\n{e.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error al consultar el script: {script.Nombre}\n{script.SQLScript}\n{ex.Message}");
            }
        }

        //public static async Task ProcesarScriptOnlineMultiplesAsync(string token, SPOS_SQLScripts script, string apiUrl, string repositoryO, string repositoryC, bool isOnLine)
        //{
        //    try
        //    {
        //        var numeroSucursal = await GetSucursalServicio();

        //        var data = await SoltecAppDAL.DataScriptMultipleSQLServer(script);
        //        var jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        //        if (data.ToList().Count > 0)
        //        {
        //            bool withData = false;
        //            foreach (var table in data)
        //            {
        //                if (table.Value is IEnumerable<object> lista && lista.Any())
        //                {
        //                    withData = true;
        //                    break;
        //                }
        //            }
        //            if (withData)
        //            {
        //                var response = await SoltecAppBLL.ProcesaScriptSQLiteMultiple(script, apiUrl, jsonString);
        //                if (response)
        //                {
        //                    foreach (var table in data)
        //                    {
        //                        Logger.Info($"Proceso {script.Nombre} ejecutado correctamente.");
        //                    }
        //                }
        //                else
        //                    Logger.Info($"Proceso {script.Nombre} NO se ejecutó correctamente.");
        //            }
        //            else
        //            {
        //                Logger.Info($"No se encontraron registros {script.Nombre} para este script.");
        //            }
        //        }
        //        else
        //            Logger.Info($"No se encontraron registros {script.Nombre} para este script.");

        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error($"Ocurrio un error al consultar el script: {script.Nombre}\n{script.SQLScript}\n{ex.Message}");
        //    }
        //}

        public static async Task<bool> ProcesaScriptSQLite(SPOS_SQLScripts script, string jsonString, string ConfigApiUrl)
        {
            try
            {
                //var token = string.Empty;
                //if (_apiResponse.Result == null)
                //{
                //    _apiResponse = Login(apiUrl).Result;
                //    token = _apiResponse.Result.Token;
                //} else
                //    token = _apiResponse.Result.Token;

                //if (!string.IsNullOrEmpty(token))
                //{
                    var numeroSucursal = await GetSucursalServicio();
                    {
                        try
                        {
                            if (jsonString != "[]")
                            {
                                var response = await RequestAppBLL.OnLineSales(jsonString, script.Nombre, numeroSucursal, ConfigApiUrl);

                                if (response.Success)
                                    Logger.Info($"El proceso {script.Nombre} se ejecutó correctamente: {numeroSucursal}");
                                else
                                    Logger.Info($"El proceso {script.Nombre} NO se ejecutó correctamente: {numeroSucursal}");
                            }
                            else
                                Logger.Info($"No se encontraron registros {script.Nombre} para este script.");

                            return true;
                        }
                        catch (Exception e)
                        {
                            Logger.Error($"Ocurrio un error al consultar el script: {script.Nombre}\n{script.SQLScript}\n{e.Message}");
                            return false;
                        }
                    }
                //} else
                //{
                //    return false;
                //}

            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error al procesar SQLite: {script.Nombre}\n{script.SQLScript}\n{ex.Message}");
                return false;
            }
        }


        //public static async Task<bool> ProcesaScriptSQLiteMultiple(SPOS_SQLScripts script, string ConfigApiUrl, string jsonString, string error)
        //{
        //    try
        //    {
        //            var numeroSucursal = await GetSucursalServicio();
        //            {
        //                try
        //                {
        //                    //if (jsonString != "[]")
        //                    //{
        //                    var response = await RequestAppBLL.OnLineSalesMultiple(jsonString, script.Nombre, numeroSucursal, ConfigApiUrl, script.IdSucursal, error);

        //                    if (response.Success)
        //                    {
        //                        Logger.Info($"El proceso {script.Nombre} se ejecutó correctamente: {numeroSucursal}");
        //                        return true;
        //                    }
        //                    else
        //                    {
        //                        Logger.Warning($"El proceso {script.Nombre} NO se ejecutó correctamente: {numeroSucursal}, {response.Message}");
        //                        return false;
        //                    }
                            
        //                }
        //                catch (Exception e)
        //                {
        //                    Logger.Error($"Ocurrio un error al consultar el script: {script.Nombre}\n{script.SQLScript}\n{e.Message}");
        //                    return false;
        //                }
        //            }
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error($"Ocurrio un error al procesar SQLite: {script.Nombre}\n{script.SQLScript}\n{ex.Message}");
        //        return false;
        //    }
        //}

        public static async Task<bool> ProcesaScriptSQLServerMultiple(SPOS_SQLScripts script, string jsonString, string ConfigApiUrl, bool conDatos)
        {
            try
            {
                var numeroSucursal = await GetSucursalServicio();
                {
                    try
                    {
                        var response = await RequestAppBLL.OnLineSalesSQLServerMultiple(jsonString, script.Nombre, numeroSucursal, script.IdSucursal, ConfigApiUrl, conDatos);

                        if (response.Success)
                            Logger.Info($"El proceso {script.Nombre} se ejecutó correctamente: {numeroSucursal}");
                        else
                            Logger.Info($"El proceso {script.Nombre} NO se ejecutó correctamente: {numeroSucursal}");

                        return true;
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Ocurrio un error al consultar el script: {script.Nombre}\n{script.SQLScript}\n{e.Message}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrio un error al procesar SQLite: {script.Nombre}\n{script.SQLScript}\n{ex.Message}");
                return false;
            }
        }




        public static async Task<string> GetSucursalServicio()
        {
            var getSucusalServicio = "";

            var filePathSucursalObtenida = @"C:\sfspos\serviceApp\SucursalObtenida.txt";
            var existFileSucursalObtenida = System.IO.File.Exists(filePathSucursalObtenida);

            if (!existFileSucursalObtenida)
            {
                Logger.Info(@"El archivo SucursalObtenida.txt NO existe en el directorio C:\sfspos\serviceApp\");
            }
            else
            {
                Logger.Info(@"El archivo SucursalObtenida.txt SI existe en el directorio C:\sfspos\serviceApp\ leyendo contenido...");
                getSucusalServicio = System.IO.File.ReadAllText(filePathSucursalObtenida);
            }

            return getSucusalServicio;
        }

    }
}
