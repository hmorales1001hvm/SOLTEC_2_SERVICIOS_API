using AppCommon.Api;
using AppCommon.Entities;
using AppCommon.Venta;
using BLL;
using Common.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Utilities;
using Soltec.AppCommon.Venta;
using Soltec.Common.LoggerFramework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace AppBLL
{
    public class RequestAppBLL
    {
        public IConfiguration Configuration;
        private static readonly Random _rand = new Random();
        private static readonly Random _rand2 = new Random();
        static string ver1 = string.Empty, ver2 = string.Empty, ver3 = string.Empty, ver4 = string.Empty, ver5 = string.Empty, ver6 = string.Empty;
        public static string urlAPI = string.Empty;

        public RequestAppBLL(IConfiguration configuration)
        {
            Configuration = configuration;
        }


        public static async Task<ApiResponse<VersionesApp>> GetVersionesApp(string token, string _apiUrl)
        {
            try
            {
                var apiUrl = $"{_apiUrl}/transmision/getVersionesAppV2";

                var apiResponse = new ApiResponse<VersionesApp>();
                HttpResponseMessage response;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    response = client.GetAsync(apiUrl).Result;

                    if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;

                        return apiResponse;
                    }

                    apiResponse = JsonConvert.DeserializeObject<ApiResponse<VersionesApp>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
                //if (!response.IsSuccessStatusCode)
                //    throw new ApplicationException();
                return apiResponse;
            }
            catch (Exception ex)
            {
                throw;
            }

        }

        public static async Task<ApiResponse<MonitorDeApps>> GetMonitorDeApps(string _apiUrl)
        {
            try
            {
                var apiUrl = $"{_apiUrl}/transmision/getMonitorDeApps";

                var apiResponse = new ApiResponse<MonitorDeApps>();
                HttpResponseMessage response;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    response = client.GetAsync(apiUrl).Result;

                    if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;

                        return apiResponse;
                    }

                    apiResponse = JsonConvert.DeserializeObject<ApiResponse<MonitorDeApps>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
                
                return apiResponse;
            }
            catch (Exception ex)
            {
                throw;
            }

        }

        
        public static async Task<ApiResponse> MarkOnLine(string sucursal, string version1, string version2, string version3, string version4, string version5, string version6, string token, string _apiUrl)
        {
            var apiUrl = $"{_apiUrl}/transmision/markOnLineV2";

            var statusTransmision = new MarkOnlineBody
            {
                Sucursal = sucursal,
                Version1 = version1,
                Version2 = version2,
                Version3 = version3,
                Version4 = version4,
                Version5 = version5,
                Version6 = version6
            };

            var apiResponse = new ApiResponse();
            HttpResponseMessage response;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var content = new StringContent(JsonConvert.SerializeObject(statusTransmision), Encoding.UTF8, "application/json");

                response = client.PostAsync(apiUrl, content).Result;

                if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                {
                    apiResponse.Success = false;
                    apiResponse.Message = response.ReasonPhrase;

                    return apiResponse;
                }

                apiResponse = JsonConvert.DeserializeObject<ApiResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
            return apiResponse;
        }

        public static async Task<int> GetConfiguracion(string _apiUrl)
        {
            var sucursal = string.Empty;
            var tiempoEjecutaApp = 0;
            var apiResponse = new ApiResponse<ServicioConfig>();
            HttpResponseMessage response;
            //try
            //{
            var filePathSucursalObtenida = @"C:\sfspos\serviceApp\SucursalObtenida.txt";
            var existFileSucursalObtenida = System.IO.File.Exists(filePathSucursalObtenida);
            var contentFile = System.IO.File.ReadAllText(filePathSucursalObtenida);
            sucursal = contentFile;

            if (!string.IsNullOrEmpty(sucursal) && sucursal != "Desconocida")
            {

                var apiUrl = $"{_apiUrl}/transmision/getConfiguracion/{sucursal}";
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    response = await client.GetAsync(apiUrl).ConfigureAwait(false);

                    if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;

                        return tiempoEjecutaApp;
                    }

                    apiResponse = JsonConvert.DeserializeObject<ApiResponse<ServicioConfig>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    tiempoEjecutaApp = apiResponse.Result.TiempoEjecutaApp;
                }
            }
            //}catch(Exception ex)
            //{

            //}
            //if (!response.IsSuccessStatusCode)
            //    throw new ApplicationException();
            return tiempoEjecutaApp;
        }

        public static async Task<int> GetConfiguracionAppTransmision(string _apiUrl)
        {
            var sucursal = string.Empty;
            var tiempoEjecutaApp = 0;
            var apiResponse = new ApiResponse<ServicioConfig>();
            HttpResponseMessage response;
            //try
            //{
            var filePathSucursalObtenida = @"C:\sfspos\serviceApp\SucursalObtenida.txt";
            var existFileSucursalObtenida = System.IO.File.Exists(filePathSucursalObtenida);
            var contentFile = System.IO.File.ReadAllText(filePathSucursalObtenida);
            sucursal = contentFile;

            if (!string.IsNullOrEmpty(sucursal) && sucursal != "Desconocida")
            {

                var apiUrl = $"{_apiUrl}/transmision/getConfiguracion/{sucursal}";
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    response = await client.GetAsync(apiUrl).ConfigureAwait(false);

                    if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;

                        return tiempoEjecutaApp;
                    }

                    apiResponse = JsonConvert.DeserializeObject<ApiResponse<ServicioConfig>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    tiempoEjecutaApp = apiResponse.Result.TiempoVerificaProceso;
                }
            }
            return tiempoEjecutaApp;
        }

        public static async Task<ApiResponse> IsOnline(string _apiUrl)
        {
            var apiUrl = $"{_apiUrl}/transmision/isOnline";

            var apiResponse = new ApiResponse();
            HttpResponseMessage response;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                response = client.GetAsync(apiUrl).Result;

                if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                {
                    apiResponse.Success = false;
                    apiResponse.Message = response.ReasonPhrase;

                    return apiResponse;
                }

                apiResponse = JsonConvert.DeserializeObject<ApiResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }

            if (!response.IsSuccessStatusCode)
                Logger.Error($"No se pudo procesar: IsOnline\n{response.StatusCode}");

            return apiResponse;
        }

        public static async Task<ApiResponse> IsOnline(string token, string _apiUrl)
        {
            var apiUrl = $"{_apiUrl}/transmision/isOnlineV2";

            var apiResponse = new ApiResponse();
            HttpResponseMessage response;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                response = client.GetAsync(apiUrl).Result;

                if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                {
                    apiResponse.Success = false;
                    apiResponse.Message = response.ReasonPhrase;

                    return apiResponse;
                }

                apiResponse = JsonConvert.DeserializeObject<ApiResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }

            if (!response.IsSuccessStatusCode)
                Logger.Error($"No se pudo procesar: IsOnline\n{response.StatusCode}");

            return apiResponse;
        }



        public static async Task<ApiResponse<AppCommon.Entities.SPOS_SQLScripts>> GetSPOS_SQLScripts(string _apiUrl, string token, string numeroSucursal, bool isOnLine)
        {
            var apiResponse = new ApiResponse<AppCommon.Entities.SPOS_SQLScripts>();
            try
            {
                if (string.IsNullOrEmpty(numeroSucursal))
                    numeroSucursal = "00000";
                var apiUrl = $"{_apiUrl}/venta/getSPOS_SQLScripts/{numeroSucursal}/{isOnLine}";
                HttpResponseMessage response;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    response = client.GetAsync(apiUrl).Result;

                    if ((int)response.StatusCode == 400)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;

                        return apiResponse;
                    }
                    else if ((int)response.StatusCode == 503)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;

                        return apiResponse;
                    }
                    else if ((int)response.StatusCode == 401)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;

                        return apiResponse;
                    }

                    apiResponse = JsonConvert.DeserializeObject<ApiResponse<AppCommon.Entities.SPOS_SQLScripts>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }

                return apiResponse;
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrió un error: GetSPOS_SQLScripts.\n{ex.Message}");
                return apiResponse;
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

        public static async Task<ApiResponse<AppCommon.Entities.SPOS_SQLScripts>> GetSQLScripts(bool isOnLine, string ConfigApiUrl)
        {
            var apiResponse = new ApiResponse<AppCommon.Entities.SPOS_SQLScripts>();
            
            try
            {
                await SetURLAsync(ConfigApiUrl);

                if (!string.IsNullOrEmpty(urlAPI))
                {
                    var numeroSucursal = await GetSucursalServicio();
                    if (string.IsNullOrEmpty(numeroSucursal))
                        return apiResponse;

                    var apiUrl = $"{urlAPI}/venta/getSQLScripts/{isOnLine}/{numeroSucursal}";
                    HttpResponseMessage response;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Clear();
                        response = client.GetAsync(apiUrl).Result;

                        if ((int)response.StatusCode == 400)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        else if ((int)response.StatusCode == 503)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        else if ((int)response.StatusCode == 401)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        apiResponse = JsonConvert.DeserializeObject<ApiResponse<AppCommon.Entities.SPOS_SQLScripts>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    }
                }
                else
                {
                    Logger.Warning("No se encontraron servicios disponibles por el momento.");
                }
                return apiResponse;
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrió un error: GetSPOS_SQLScripts.\n{ex.Message}");
                return apiResponse;
            }
        }


        public static async Task<ApiResponse<AppCommon.Entities.SPOS_SQLScripts>> GetSQLScriptsSQLite(string ConfigApiUrl, string numeroSucursal)
        {
            var apiResponse = new ApiResponse<AppCommon.Entities.SPOS_SQLScripts>();
            try
            {
                await SetURLAsync(ConfigApiUrl);

                if (!string.IsNullOrEmpty(urlAPI))
                {

                    var apiUrl = $"{urlAPI}/venta/getSQLScriptsSQLite/{numeroSucursal}";
                    HttpResponseMessage response;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Clear();
                        response = client.GetAsync(apiUrl).Result;

                        if ((int)response.StatusCode == 400)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        else if ((int)response.StatusCode == 503)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        else if ((int)response.StatusCode == 401)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }

                        apiResponse = JsonConvert.DeserializeObject<ApiResponse<AppCommon.Entities.SPOS_SQLScripts>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    }
                }
                else
                {
                    Logger.Warning("No se encontraron servicios disponibles por el momento.");
                }

                return apiResponse;
            }
            catch (Exception ex)
            {
                Logger.Error($"Ocurrió un error: GetSPOS_SQLScripts.\n{ex.Message}");
                return apiResponse;
            }
        }


        public static async Task<ApiResponse> OnLineSales(string json, string nombre, string numeroSucursal, string ConfigApiUrl)
        {
            var apiResponse = new ApiResponse();
            try
            {
                await SetURLAsync(ConfigApiUrl);
                if (!string.IsNullOrEmpty(urlAPI))
                {
                    var apiUrl = $"{urlAPI}/venta/onLineSales";

                    var ventaEnLinea = new ProcesosOnLine
                    {
                        Sucursal = numeroSucursal,
                        Json = json,
                        NombreProceso = nombre,
                    };

                    HttpResponseMessage response;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Clear();
                        //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                        var content = new StringContent(JsonConvert.SerializeObject(ventaEnLinea), Encoding.UTF8, "application/json");

                        response = client.PostAsync(apiUrl, content).Result;

                        if ((int)response.StatusCode == 400)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        else if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        Logger.Info(await response.Content.ReadAsStringAsync());
                        apiResponse = JsonConvert.DeserializeObject<ApiResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    }
                }
                else
                {
                    Logger.Warning("No se encontraron servicios disponibles por el momento.");
                }
                return apiResponse;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en: OnLineSales.\n{ex.Message}");
                return apiResponse;
            }
        }


        //public static async Task<ApiResponse> OnLineSalesMultiple(string json, string nombre, string numeroSucursal, string ConfigApiUrl, int IdSucursal, string error)
        //{
        //    var apiResponse = new ApiResponse();
        //    try
        //    {
        //        await SetURLAsync(ConfigApiUrl);
        //        if (!string.IsNullOrEmpty(urlAPI))
        //        {
        //            var apiUrl = $"{urlAPI}/venta/onLineSalesMultiple";

        //            var ventaEnLinea = new ProcesosOnLine
        //            {
        //                Sucursal = numeroSucursal,
        //                Json = json,
        //                NombreProceso = nombre,
        //                IdSucursal = IdSucursal,
        //                Ver1 = error,
        //                Ver2 ="",
        //                Ver3="",
        //                Ver4="",
        //                Ver5="",
        //                Ver6="",
        //                ConDatos=true
        //            };

        //            HttpResponseMessage response;

        //            using (var client = new HttpClient())
        //            {
        //                client.DefaultRequestHeaders.Clear();
        //                var content = new StringContent(JsonConvert.SerializeObject(ventaEnLinea), Encoding.UTF8, "application/json");
        //                response = client.PostAsync(apiUrl, content).Result;

        //                if ((int)response.StatusCode == 400)
        //                {
        //                    apiResponse.Success = false;
        //                    apiResponse.Message = response.ReasonPhrase;

        //                    return apiResponse;
        //                }
        //                else if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
        //                {
        //                    apiResponse.Success = false;
        //                    apiResponse.Message = response.ReasonPhrase;

        //                    return apiResponse;
        //                } 

        //                Logger.Info(await response.Content.ReadAsStringAsync());
        //                apiResponse = JsonConvert.DeserializeObject<ApiResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        //            }
        //        }
        //        else
        //        {
        //            Logger.Warning("No se encontraron servicios disponibles por el momento.");
        //        }
        //        return apiResponse;
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Error($"Error en: onLineSalesMultiple.\n{ex.Message}");
        //        return apiResponse;
        //    }
        //}


        private static async Task SetURLAsync(string ConfigApiUrl)
        {
            try
            {
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
                    //await SoltecAppBLL.StartUpAppsOnLineAsync(urlAPI);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en SetURLAsync: {ex.Message}");
            }
        }

        public static async Task SucursalEnLinea()
        {
            ver1 = string.Empty;
            ver2 = string.Empty;
            ver3 = string.Empty;
            ver4 = string.Empty;
            ver5 = string.Empty;
            ver6 = string.Empty;

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

            ver2 = (versionInfo2 == null) ? "" : versionInfo2.FileVersion;
            ver1 = (versionInfo1 == null) ? "" : versionInfo1.FileVersion;
            ver3 = (versionInfo3 == null) ? "" : versionInfo3.FileVersion;
            ver4 = (versionInfo4 == null) ? "" : versionInfo4.FileVersion;
            ver5 = (versionInfo5 == null) ? "" : versionInfo5.FileVersion;
            ver6 = (versionInfo6 == null) ? "" : versionInfo6.FileVersion;
        }


        public static async Task<ApiResponse> OnLineSalesSQLServerMultiple(string json, string nombre, string numeroSucursal, int IdSucursal, string ConfigApiUrl, bool conDatos)
        {
            var apiResponse = new ApiResponse();
            try
            {
                await SetURLAsync(ConfigApiUrl);
                if (!string.IsNullOrEmpty(urlAPI))
                {
                    await SucursalEnLinea();
                    var apiUrl = $"{urlAPI}/venta/onLineSalesSqlServerMultiple";

                    var ventaEnLinea = new ProcesosOnLine
                    {
                        Sucursal = numeroSucursal,
                        Json = json,
                        NombreProceso = nombre,
                        IdSucursal = IdSucursal,
                        Ver1 = ver1,
                        Ver2 = ver2,
                        Ver3 = ver3,
                        Ver4 = ver4,
                        Ver5 = ver5,
                        Ver6 = ver6,
                        ConDatos = conDatos
                    };

                    HttpResponseMessage response;

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Clear();
                        var content = new StringContent(JsonConvert.SerializeObject(ventaEnLinea), Encoding.UTF8, "application/json");

                        response = client.PostAsync(apiUrl, content).Result;

                        if ((int)response.StatusCode == 400)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        else if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            return apiResponse;
                        }
                        Logger.Info(await response.Content.ReadAsStringAsync());
                        apiResponse = JsonConvert.DeserializeObject<ApiResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    }
                }
                else
                {
                    Logger.Warning("No se encontraron servicios disponibles por el momento.");
                }

                return apiResponse;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en: OnLineSalesSQLServerMultiple.\n{ex.Message}");
                return apiResponse;
            }
        }

        public static async Task<ApiResponse> UploadFileZIP(string token, string fullPathZIP, string apiUrlOrquestacion)
        {
            string[] ulrs = apiUrlOrquestacion.Split('|');
            var apiResponse = new ApiResponse();
            foreach (string url in ulrs)
            {
                try
                {
                    var apiUrl = $"{url}/venta/uploadFileZIP";

                    var form = new MultipartFormDataContent();
                    var fileContent = new ByteArrayContent(File.ReadAllBytes(fullPathZIP));
                    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                    form.Add(fileContent, "File", Path.GetFileName(fullPathZIP));

                    form.Add(new StringContent("uploadFileZIP.zip"), "Name");
                    form.Add(new StringContent("Archivo zip con los JSON"), "Description");

                    var httpClient = new HttpClient();

                    httpClient.DefaultRequestHeaders.Clear();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = httpClient.PostAsync(apiUrl, form).Result;
                    response.EnsureSuccessStatusCode();
                    if ((int)response.StatusCode == 400)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;

                        return apiResponse;
                    }
                    else if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                    {
                        apiResponse.Success = false;
                        apiResponse.Message = response.ReasonPhrase;

                        return apiResponse;
                    }
                    var responseContent = await response.Content.ReadAsStringAsync();

                    apiResponse = JsonConvert.DeserializeObject<ApiResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    return apiResponse;
                }
                catch { }

            }

            return apiResponse;
        }

        public static async Task<bool> DownloadFileZIP(string token, string url, string file, string pathDestino)
        {
            var result = false;
            try
            {
                Logger.Info($"Iniciando consumo de API para descarga de archivos {file}");
                var apiUrl = $"{url}/venta/DownloadFileZIP/{file}";
                var apiResponse = new ApiResponse();
                HttpResponseMessage response;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    response = client.GetAsync(apiUrl).Result;
                    if (response != null)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            var httpContent = response.Content;

                            using (var newFile = System.IO.File.Create(@pathDestino + @"\" + file))
                            {
                                var stream = await httpContent.ReadAsStreamAsync();
                                await stream.CopyToAsync(newFile);
                            }
                            result = true;
                        }
                        else if ((int)response.StatusCode == 400)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            //return apiResponse;
                            result = false;
                        }
                        else if ((int)response.StatusCode == 503)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            //return apiResponse;
                            result = false;
                        }
                        else if ((int)response.StatusCode == 401)
                        {
                            apiResponse.Success = false;
                            apiResponse.Message = response.ReasonPhrase;

                            //return apiResponse;
                            result = false;
                        }
                    }
                    //apiResponse = JsonConvert.DeserializeObject<ApiResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
                Logger.Info($"Descarga del archivo del API correctamente.  {file}");
                //return apiResponse;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                result = false;
            }
            return result;
        }
    }
}
