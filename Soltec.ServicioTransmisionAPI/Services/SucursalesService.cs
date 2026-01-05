using Soltec.DB;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Threading.Tasks;

public class SucursalesService
{
    private readonly string _mysqlConnectionString;

    public SucursalesService(string mysqlConnectionString)
    {
        _mysqlConnectionString = mysqlConnectionString;
    }

    
}
