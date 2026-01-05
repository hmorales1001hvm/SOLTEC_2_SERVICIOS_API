using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace ApiCommon.Api
{
    public abstract class BaseApiController : ControllerBase
    {
        protected IActionResult SoltecErrorMessage(Exception ex, object parameters = null, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        {
            var error = new ApiResponse(ex);

            return StatusCode((int)statusCode, error);
        }

        protected ObjectResult Forbidden() =>
            new ObjectResult(new ApiResponse
            {
                Success = false,
                Message = "Operacion no autorizada",
                Url = Url.RouteUrl("Login")
            })
            { StatusCode = 404 };

        protected NotFoundObjectResult NotFound(string message = null) =>
            NotFound(new ApiResponse
            {
                Success = false,
                Message = message ?? "Elemento no encontrado"
            });

        protected BadRequestObjectResult BadRequest(string message = null) =>
            BadRequest(new ApiResponse
            {
                Success = false,
                Message = message ?? "Parámetros incorrectos"
            });

        protected UnauthorizedObjectResult NotAuthorized() => new UnauthorizedObjectResult(new ApiResponse
        {
            Success = false,
            Message = "Operacion no autorizada",
        })
        { StatusCode = 401 };

    }
}
