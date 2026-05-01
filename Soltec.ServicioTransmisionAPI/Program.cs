using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Soltec.Common.Logger;
using System.Text;
using Serilog;
using Soltec.Orquestacion.Common.Settings;

var builder = WebApplication.CreateBuilder(args);

// ------------------------
// Kestrel
// ------------------------
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524288000;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 524288000;
});

// ------------------------
// Config (ya viene por default, pero lo dejas por compatibilidad)
// ------------------------
builder.Configuration.AddJsonFile("appsettings.json");

// ------------------------
// Logs (archivo)
// ------------------------
bool enableFileLogging = builder.Configuration.GetValue<bool>("LogFile:EnableFileLogging");

if (enableFileLogging)
{
    FileUtil.localLogPath = builder.Configuration.GetValue<string>("LogFile:logPath");
}

// ------------------------
// SERILOG
// ------------------------
builder.Host.UseSerilog((context, services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: $"{FileUtil.localLogPath}/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30
        );
});

// ========================
// 🔥 AMBIENTE + CONEXIONES
// ========================
var configuration = builder.Configuration;

// Ambiente
var currentEnv = configuration["EnvironmentSettings:Current"] ?? "PROD";

// Leer conexiones por ambiente
var dbSettings = configuration
    .GetSection($"ConnectionStringsByEnv:{currentEnv}")
    .Get<DbSettings>();

// 🔁 Fallback (NO rompe lo actual)
if (dbSettings == null)
{
    dbSettings = new DbSettings
    {
        DbFacturaReal = configuration.GetConnectionString("DbFacturaReal"),
        DbFacturaRealOrquestador = configuration.GetConnectionString("DbFacturaRealOrquestador"),
        DbSimiPET = configuration.GetConnectionString("DbSimiPET")
    };
}

// Registrar en DI
builder.Services.AddSingleton(dbSettings);

// ========================
// 🔐 JWT
// ========================
var secretKey = configuration["settingsJWT:secretKey"];
var keyBytes = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(config =>
    {
        config.RequireHttpsMetadata = false;
        config.SaveToken = true;
        config.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// ------------------------
// Servicios
// ------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 👇 si usas DA (recomendado)
builder.Services.AddScoped<Soltec.DB.SetDeTransmisionesDB>();

// Hosted service
builder.Services.AddHostedService<ConexionCacheService>();

var app = builder.Build();

// ------------------------
// Pipeline
// ------------------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();




//using Microsoft.AspNetCore.Authentication.JwtBearer;
//using Microsoft.IdentityModel.Tokens;
//using Soltec.Common.Logger;
//using System.Text;
//using Serilog;
//using MySql.Data.MySqlClient;

//var builder = WebApplication.CreateBuilder(args);

//// Kestrel
//builder.WebHost.ConfigureKestrel(options =>
//{
//    options.Limits.MaxRequestBodySize = 524288000;
//    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(30);
//    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
//});

//builder.Services.Configure<IISServerOptions>(options =>
//{
//    options.MaxRequestBodySize = 524288000;
//});

//// Config
//builder.Configuration.AddJsonFile("appsettings.json");

//// File log path
//bool enableFileLogging = builder.Configuration.GetValue<bool>("LogFile:EnableFileLogging");
//if (enableFileLogging)
//{
//    FileUtil.localLogPath = builder.Configuration.GetValue<string>("LogFile:logPath");
//}

//builder.Host.UseSerilog((context, services, loggerConfig) =>
//{
//    loggerConfig
//        .ReadFrom.Configuration(context.Configuration)
//        .ReadFrom.Services(services)
//        .Enrich.FromLogContext()
//        .WriteTo.Console()
//        .WriteTo.File(
//            path: $"{FileUtil.localLogPath}/log-.txt",
//            rollingInterval: RollingInterval.Day,
//            retainedFileCountLimit: 30
//        );
//});

//// JWT
//var secretKey = builder.Configuration["settingsJWT:secretKey"];
//var keyBytes = Encoding.UTF8.GetBytes(secretKey);

//builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//    .AddJwtBearer(config =>
//    {
//        config.RequireHttpsMetadata = false;
//        config.SaveToken = true;
//        config.TokenValidationParameters = new TokenValidationParameters
//        {
//            ValidateIssuerSigningKey = true,
//            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
//            ValidateIssuer = false,
//            ValidateAudience = false
//        };
//    });

//// Controllers
//builder.Services.AddControllers();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
//builder.Services.AddHostedService<ConexionCacheService>();
//var app = builder.Build();

//app.UseSwagger();
//app.UseSwaggerUI();

//app.UseHttpsRedirection();
//app.UseAuthentication();  
//app.UseAuthorization();

//app.MapControllers();
//app.Run();
