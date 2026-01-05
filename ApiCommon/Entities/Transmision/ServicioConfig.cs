namespace ApiCommon.Entities.Transmision
{
    public class ServicioConfig
    {
        public int Id { get; set; }

        public string ClaveSimi { get; set; }

        public int TiempoVerificaProceso { get; set; }

        public int TiempoEjecutaApp { get; set; }

        public int TiempoEsperaError { get; set; }

        public bool HabilitaLog { get; set; } = true;
    }
}
