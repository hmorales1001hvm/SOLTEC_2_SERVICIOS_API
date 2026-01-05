using Common.Api;
using Common.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Soltec.Common.LoggerFramework;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace BLL
{
    public class RequestBLL
    {
        public IConfiguration Configuration;

        public ILogger<RequestBLL> Logger;

        public string ConfigApiUrl;
        public RequestBLL(IConfiguration configuration, ILogger<RequestBLL> logger)
        {
            Configuration = configuration;
            Logger = logger;

            ConfigApiUrl = Configuration.GetSection("AppConfig").GetSection("ApiUrl").Value;
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
                throw new ApplicationException();

            return apiResponse;
        }

        public async Task<ApiResponse<ServicioConfig>> GetConfiguracion(string sucursal, string token)
        {
            var apiUrl = $"{ConfigApiUrl}/transmision/getConfiguracion/{sucursal}";

            var apiResponse = new ApiResponse<ServicioConfig>();
            HttpResponseMessage response;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                response = await client.GetAsync(apiUrl).ConfigureAwait(false);

                if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                {
                    apiResponse.Success = false;
                    apiResponse.Message = response.ReasonPhrase;

                    return apiResponse;
                }

                apiResponse = JsonConvert.DeserializeObject<ApiResponse<ServicioConfig>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
            //if (!response.IsSuccessStatusCode)
            //    throw new ApplicationException();
            return apiResponse;
        }

        public async Task<ApiResponse<ServicioProcesos>> GetProcesos(string token)
        {
            var apiUrl = $"{ConfigApiUrl}/transmision/getProcesosV2";

            var apiResponse = new ApiResponse<ServicioProcesos>();
            HttpResponseMessage response;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                response = await client.GetAsync(apiUrl).ConfigureAwait(false);

                if ((int)response.StatusCode == 503 || (int)response.StatusCode == 401)
                {
                    apiResponse.Success = false;
                    apiResponse.Message = response.ReasonPhrase;

                    return apiResponse;
                }

                apiResponse = JsonConvert.DeserializeObject<ApiResponse<ServicioProcesos>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
            //if (!response.IsSuccessStatusCode)
            //    throw new ApplicationException();
            return apiResponse;
        }
    }
}
