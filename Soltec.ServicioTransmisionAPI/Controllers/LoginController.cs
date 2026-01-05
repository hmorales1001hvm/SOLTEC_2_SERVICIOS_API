using ApiCommon.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Soltec.ApiCommon.Entities.Security;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiCommon.Api;

namespace Soltec.ServicioTransmisionAPI.Controllers
{
    [Route("api")]
    [ApiController]
    public class LoginController : BaseApiController
    {
        private readonly string secretKey;
        private readonly string userName;
        public readonly string minutesExpire;
        public LoginController(IConfiguration configuration)
        {
            secretKey = configuration["settingsJWT:secretKey"];
            userName = configuration["settingsJWT:userName"];
            minutesExpire = configuration["settingsJWT:minutesExpire"];
        }

        /// <summary>
        /// Método para obtener token
        /// </summary>
        /// <returns></returns>
        [HttpGet("auth/Login")]
        public async Task<IActionResult> Login()
        {
            try
            {
                var keyBytes = Encoding.ASCII.GetBytes(secretKey);
                var claims = new ClaimsIdentity();

                claims.AddClaim(new Claim(ClaimTypes.NameIdentifier, userName));
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = claims,
                    Expires = DateTime.UtcNow.AddMinutes(Convert.ToInt32(minutesExpire)),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var tokenConfig = tokenHandler.CreateToken(tokenDescriptor);

                var tokenResult = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
                var user = new User() { Token = tokenResult };

                return Ok(new ApiResponse<User>(user));
            }
            catch (Exception ex)
            {
                return SoltecErrorMessage(ex);
            }

        }
    }
}
