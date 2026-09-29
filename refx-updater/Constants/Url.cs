using System;

namespace refx_updater.Constants
{
    public static class Url
    {
        public static readonly string BaseUrl = ResolveBaseUrl();

        public static readonly string MetadataUrl = BaseUrl + "/metadata.json";

        private static string ResolveBaseUrl()
        {
            // usage: refxUpdater.exe -devserver myhost.example
            // (also reads UPDATER_BASE_URL env as fallback)
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-devserver")
                    return Norm(args[i + 1]);
            }

            var env = Environment.GetEnvironmentVariable("UPDATER_BASE_URL");
            if (!string.IsNullOrWhiteSpace(env))
                return Norm(env);

            return "https://updater.041095.xyz";
        }

        private static string Norm(string url)
        {
            url = url.Trim();
            if (url.EndsWith("/"))
                url = url.TrimEnd('/');
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;
            return url;
        }
    }
}
