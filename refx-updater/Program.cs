using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace refx_updater
{
    class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        private static readonly string _metadataUrl = "https://updater.refx.online/metadata.json";

        private static readonly ConcurrentDictionary<string, (string Status, int Position)> _fileStatuses = new ConcurrentDictionary<string, (string, int)>();

        // starting position for displaying file statuses in the console
        private static int _cursorTop;

        // to handle console updates
        private static readonly SemaphoreSlim _consoleLock = new SemaphoreSlim(1, 1);

        [STAThread]
        static void Main() => _main().GetAwaiter().GetResult();

        static async Task _main()
        {
            Console.WriteLine("re;fx Updater\n");

            if (await linkFolders()) Console.WriteLine("Ok");

            var files = await getMetadata();
            if (files == null) return;

            _cursorTop = Console.CursorTop;
            Console.WriteLine(new string('\n', files.Count));

            for (int i = 0; i < files.Count; i++)
            {
                _fileStatuses[files[i].filename] = ($"Pending {files[i].filename}", i);
            }

            var downloadTasks = files.Select(async file =>
            {
                await processFile(file);
            });

            await Task.WhenAll(downloadTasks);

            Console.SetCursorPosition(0, _cursorTop + files.Count + 1);
            Console.WriteLine("All files checked and downloaded.");
            Console.WriteLine("Press any key to run the game...");
            Console.ReadKey();
            Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "osu!.exe"));
        }

        private static async Task<List<Metadata>> getMetadata()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(_metadataUrl);
                return JsonConvert.DeserializeObject<List<Metadata>>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching metadata: {ex.Message}");
                return null;
            }
        }

        private static async Task processFile(Metadata file)
        {
            try
            {
                if (File.Exists(file.filename))
                {
                    var hash = MD5Check(file.filename);
                    if (hash.Equals(file.file_hashmd5, StringComparison.OrdinalIgnoreCase))
                    {
                        await updateStatus(file.filename, $"Up to date  {file.filename}", ConsoleColor.White);
                        return;
                    }
                }

                await downloadFile(file.url_full, file.filename);
            }
            catch (Exception ex)
            {
                await updateStatus(file.filename, $"Failed {file.filename}: {ex.Message}", ConsoleColor.Red);
            }
        }

        private static string MD5Check(string filename)
        {
            using (var stream = File.OpenRead(filename))
            using (var md5 = MD5.Create())
            {
                var checksum = md5.ComputeHash(stream);
                return BitConverter.ToString(checksum).Replace("-", "").ToLowerInvariant();
            }
        }

        /*
        private static string SHA256Check(string filename)
        {
            using (var stream = File.OpenRead(filename))
            using (var sha256 = SHA256.Create())
            {
                var checksum = sha256.ComputeHash(stream);
                return BitConverter.ToString(checksum).Replace("-", "").ToLowerInvariant();
            }
        }
        */

        private static async Task downloadFile(string url, string filename)
        {
            var att = 0;
            const int maxatt = 3;

            while (att < maxatt)
            {
                try
                {
                    await updateStatus(filename, $"Downloading {filename} [0%]", ConsoleColor.Yellow);

                    using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
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

                                if (totalBytes != -1)
                                {
                                    var progress = (int)((receivedBytes * 100) / totalBytes);
                                    await updateStatus(filename, $"Downloading {filename} [{progress}%]", ConsoleColor.Yellow);
                                }
                            }
                        }
                    }

                    await updateStatus(filename, $"Downloaded  {filename} [100%]", ConsoleColor.Green);
                    return;
                }
                catch (Exception) when (++att < maxatt)
                {
                    await Task.Delay(1000 * att);
                }
            }

            throw new Exception($"Failed to download after {maxatt} att");
        }

        private static async Task updateStatus(string filename, string status, ConsoleColor color)
        {
            await _consoleLock.WaitAsync();
            try
            {
                var (_, position) = _fileStatuses[filename];
                _fileStatuses[filename] = (status, position);

                Console.SetCursorPosition(0, _cursorTop + position);
                Console.ForegroundColor = color;
                Console.Write(new string(' ', Console.WindowWidth - 1));
                Console.SetCursorPosition(0, _cursorTop + position);
                Console.Write(status);
                Console.ResetColor();
            }
            finally
            {
                _consoleLock.Release();
            }
        }

        private static async Task<bool> linkFolders()
        {
            Console.WriteLine("Do you want to link folders (songs, replays, etc.)? (y/n):");
            if (Console.ReadLine()?.Trim().ToLower() != "y") return false;

            try
            {
                Console.WriteLine("Please pick your main osu! folder!");
                var osuFolder = chooseFolder();
                if (string.IsNullOrEmpty(osuFolder)) return false;

                foreach (var folder in new[] { "Songs", "Replays", "Skins", "Screenshots" })
                {
                    var sourcePath = Path.Combine(osuFolder, folder);
                    var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
                    if (Directory.Exists(sourcePath))
                    {
                        createDir(targetPath, sourcePath);
                        Console.WriteLine($"Created folder link {folder}.");
                    }
                }

                foreach (var file in new[] { $"osu!.{Environment.UserName}.cfg", "osu!.db", "collection.db" })
                {
                    var sourcePath = Path.Combine(osuFolder, file);
                    var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, targetPath, true);
                        Console.WriteLine($"Copied {file}!");
                    }
                }

                Console.WriteLine("Successfully linked your osu! folders!");
                await Task.Delay(300);
                Console.Clear();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

        private static void createDir(string targetPath, string sourcePath)
        {
            var startInfo = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{targetPath}\" \"{sourcePath}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = Process.Start(startInfo))
                process.WaitForExit();
        }

        private static string chooseFolder()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Select the osu! folder", ShowNewFolderButton = false })
                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : string.Empty;
        }

        public class Metadata
        {
            public string file_hashmd5 { get; set; }
            public string filename { get; set; }
            public string url_full { get; set; }
        }
    }
}