namespace AppCommon.Entities
{
    public class AppSettings
    {
        public AppCofig AppConfig { get; set; }
    }

    public class AppCofig
    {
        public string ApiUrl { get; set; }

        public string Version { get; set; }
    }

}
