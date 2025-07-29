using Newtonsoft.Json;

namespace refx_updater.Constants
{
    public class Metadata
    {
        [JsonProperty("file_hashmd5")]
        public string FileHashMd5 { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("url_full")]
        public string UrlFull { get; set; }
    }
}