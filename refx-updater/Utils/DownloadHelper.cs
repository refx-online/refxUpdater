using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace refx_updater.Utils
{
    public static class DownloadHelper
    {
        public static async Task DownloadFile(
            HttpClient httpClient, 
            string url, 
            string filename, 
            
            Func<int, Task> onProgress, 
            Func<Task> onComplete)
        {
            const int maxAttempts = 3;
            var attempt = 0;

            while (attempt < maxAttempts)
            {
                try
                {
                    await onProgress(0);

                    using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var receivedBytes = 0L;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                        {
                            // allocate 80 KB buffer to temporarily store chunks of data
                            var buffer = new byte[81920];
                            int bytesRead;
                            
                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                receivedBytes += bytesRead;

                                if (totalBytes == -1)
                                    continue;

                                var progress = (int)((receivedBytes * 100) / totalBytes);
                                await onProgress(progress);
                            }
                        }
                    }

                    await onComplete();
                    return;
                }
                catch (Exception) when (++attempt < maxAttempts)
                {
                    await Task.Delay(1000 * attempt);
                }
            }

            throw new Exception("Failed to download after " + maxAttempts + " attempts");
        }
    }
}