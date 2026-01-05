namespace ApiCommon.Entities.Ventas
{
    public class ParametrosGenerales
    {
        //public int idParametrosGenerales { get; set; }

        public int Intervalo { get; set; }

        public string URLApi { get; set; }

        public string PuertoApi { get; set; }

        public string EndPointConfiguracion { get; set; }

        public string EndPointSQLScripts { get; set; }

        public string EndPointSincroniza { get; set; }
    }
}
