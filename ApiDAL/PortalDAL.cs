using ApiCommon.Entities.Ventas;
using Dapper;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System.Data;

namespace ApiDAL
{
    public class PortalDAL
    {
        private readonly IConfiguration Configuration;

        public PortalDAL(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public async Task<List<VentasEnLinea>> LoadSQLScripts()
        {
            var dbConnection = Configuration.GetSection("ConnectionStrings").GetSection("DbFacturaReal").Value;
            var queryScripts = @"   SELECT C.nombre_empresa Empresa, SUM(Total) Venta  
                                    FROM soltec2_enlinea_ventas A 
                                    INNER JOIN sucursal B ON A.IdSucursal  = B.IdSucursal
                                    INNER JOIN catempresa C ON B.idEmpresa = C.idEmpresa 
                                    GROUP BY C.nombre_empresa;";
            var lstSqlScripts = new List<VentasEnLinea>();

            //try
            //{
            //    using (var connection = new MySqlConnection(dbConnection))
            //    {
            //        await connection.OpenAsync();
            //        lstSqlScripts = connection.Query<VentasEnLinea>(queryScripts, commandType: CommandType.Text, commandTimeout: 420).ToList();
            //        await connection.CloseAsync();
            //    }

                return lstSqlScripts;
            //}
   //         catch (Exception ex)
   //         {

   //             return lstSqlScripts;

			//}
        }

    }
}
