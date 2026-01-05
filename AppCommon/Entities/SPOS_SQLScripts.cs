namespace AppCommon.Entities
{
    public class SPOS_SQLScripts
    {
        public string SQLScript { get; set; }
        public string Nombre { get; set; }
        public string Tipo { get; set; }
        public string Condicion { get; set;}
        public int ValorIncrementoDecremento { get; set; }
        public bool EsAPI { get; set; }
		public string Descripcion { get; set; }
		public bool EsCatalogo { get; set; }
        public int IsOnLine { get; set; }
        public bool EsSP { get; set; }
        public bool MultiplesTablas { get; set; }
        public int TiempoTransmision { get; set; }
        public string Carga_SQLServer_SQLite { get; set; }
        public string ScriptTable { get; set; }
        public int ResetearTablaSQLite { get; set; }
        public int IdSucursal { get; set; }
        public int MultiFra { get; set; } = 0;
        public string HostName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
