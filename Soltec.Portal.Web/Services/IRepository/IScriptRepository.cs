using ApiCommon.Entities.Ventas;
using AppCommon.Api;
using Soltec.Portal.Web.Models.Script;

namespace Soltec.Portal.Web.Services.IRepository
{
    public interface IScriptRepository
    {
        public Task<ApiResponse<VentasEnLinea>> LoadScripts();
    }
}
