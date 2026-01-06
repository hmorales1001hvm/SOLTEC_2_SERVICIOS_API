using ApiCommon.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Soltec.Entities.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[Route("api")]
[ApiController]
public class LoginController : BaseApiController
{
    private readonly string secretKey;
    private readonly string userName;
    public readonly string minutesExpire;

    // Inyectamos ILogger
    private readonly ILogger<LoginController> _logger;

    public LoginController(IConfiguration configuration, ILogger<LoginController> logger)
    {
        secretKey = configuration["settingsJWT:secretKey"];
        userName = configuration["settingsJWT:userName"];
        minutesExpire = configuration["settingsJWT:minutesExpire"];

        _logger = logger; // asignamos el logger inyectado
    }

    [HttpGet("auth/Login")]
    public async Task<IActionResult> Login([FromHeader(Name = "Sucursal")] string? sucursal = null)
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
            var tokenResult = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
            var user = new User() { Token = tokenResult };

            if (!string.IsNullOrEmpty(sucursal))
            {
                // Aquí usamos el logger
                _logger.LogInformation("Login solicitado desde la sucursal {Sucursal}", sucursal);
            }

            return Ok(new ApiResponse<User>(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el Login");
            return SoltecErrorMessage(ex);
        }
    }
}
