using Dapper;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;

namespace DAL
{
    public static class WorkerServiceDAL
    {
        //public IConfiguration Configuration;

        //public string DbFacturaReal;

        //public WorkerServiceDAL(IConfiguration configuration)
        //{
        //    Configuration = configuration;
        //}
        

        public static async Task<string> GetSucursal(string sqlConnection)
        {
            string sucursal = string.Empty;

            var query = "SELECT Id_Farmacia AS ClaveSimi FROM Configuracion_Farmacia";
            var connection = new SqlConnection(sqlConnection);

            try
            {
                connection.Open();

                sucursal = connection.QuerySingle<string>(query, commandTimeout: 420);

                connection.Close();
                
            }
            catch (Exception ex)
            {
                connection.Close();
            }

			return sucursal;
		}
    }
}
