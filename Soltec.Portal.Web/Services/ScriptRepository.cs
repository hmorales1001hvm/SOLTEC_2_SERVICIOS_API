using ApiCommon.Entities.Ventas;
using AppCommon.Api;
using AppCommon.Entities;
using Newtonsoft.Json;
using Soltec.Portal.Web.Models.Script;
using Soltec.Portal.Web.Services.IRepository;
using System.Net.Http.Headers;

namespace Soltec.Portal.Web.Services
{
    public class ScriptRepository:IScriptRepository
    {
        public IConfiguration Configuration;
        public ScriptRepository(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public async Task<ApiResponse<VentasEnLinea>> LoadScripts()
        {
            try
            {
             
                var apiUrl = $"{Configuration.GetSection("AppConfig").GetSection("ApiUrl").Value}/portal/loadSQLScripts";

                var apiResponse = new ApiResponse<VentasEnLinea>();
                HttpResponseMessage response;

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Clear();
                    //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    response = await client.GetAsync(apiUrl).ConfigureAwait(false);

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

                    apiResponse = JsonConvert.DeserializeObject<ApiResponse<VentasEnLinea>>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                }

                return apiResponse;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
