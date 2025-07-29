using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using refx_updater.Constants;
using refx_updater.Utils;

namespace refx_updater.Services
{
    public class Updater
    {
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

        private readonly ConcurrentDictionary<string, ValueTuple<string, int>> _fileStatuses =
            new ConcurrentDictionary<string, ValueTuple<string, int>>();

        private readonly SemaphoreSlim _consoleLock = new SemaphoreSlim(1, 1);
        private int _cursorTop;

        public async Task RunAsync()
        {
            Console.WriteLine("re;fx Updater\n");

            if (!await HandleFolderLinking())
                Console.WriteLine("Ok");

            await ProcessUpdates();
        }

        private async Task<bool> HandleFolderLinking()
        {
            Console.WriteLine("Do you want to link folders (songs, replays, etc.)? (y/n):");
            if (Console.ReadLine()?.Trim().ToLower() != "y") return false;

            try
            {
                Console.WriteLine("Please pick your main osu! folder!");
                var osuFolder = FileHelper.ChooseFolder();
                if (string.IsNullOrEmpty(osuFolder)) return false;

                FileHelper.LinkFolders(osuFolder);
                FileHelper.CopyFiles(osuFolder);

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

        private async Task ProcessUpdates()
        {
            var files = await GetMetadataAsync();
            if (files == null) return;

            InitializeStatusDisplay(files);
            var downloadTasks = files.Select(ProcessFileAsync);
            await Task.WhenAll(downloadTasks);

            Console.SetCursorPosition(0, _cursorTop + files.Count + 1);
            Console.WriteLine("All files checked and downloaded.");
            Console.WriteLine("Press any key to run the game...");
            Console.ReadKey();
            
            Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "osu!.exe"));
        }

        private void InitializeStatusDisplay(List<Metadata> files)
        {
            _cursorTop = Console.CursorTop;
            Console.WriteLine(new string('\n', files.Count));

            for (int i = 0; i < files.Count; i++)
                _fileStatuses[files[i].Filename] = new ValueTuple<string, int>("Pending " + files[i].Filename, i);
        }

        private async Task<List<Metadata>> GetMetadataAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync(Url.MetadataUrl);
                return JsonConvert.DeserializeObject<List<Metadata>>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error fetching metadata: " + ex.Message);
                return null;
            }
        }

        private async Task ProcessFileAsync(Metadata file)
        {
            try
            {
                if (File.Exists(file.Filename))
                {
                    var hash = Crypto.GetMd5Hash(file.Filename);
                    if (hash.Equals(file.FileHashMd5, StringComparison.OrdinalIgnoreCase))
                    {
                        await UpdateStatus(file.Filename, $"Up to date {file.Filename}", ConsoleColor.White);
                        return;
                    }
                }

                await DownloadHelper.DownloadFile(
                    _httpClient, 
                    file.UrlFull, 
                    file.Filename,
                    (progress) => UpdateStatus(
                        file.Filename, 
                        $"Downloading {file.Filename} [{progress}%]",
                        ConsoleColor.Yellow),
                    () => UpdateStatus(
                        file.Filename, 
                        $"Downloaded {file.Filename} [100%]", //t 
                        ConsoleColor.Green));
            }
            catch (Exception ex)
            {
                await UpdateStatus(file.Filename, $"Failed {file.Filename}: {ex.Message}", ConsoleColor.Red);
            }
        }

        private async Task UpdateStatus(string filename, string status, ConsoleColor color)
        {
            await _consoleLock.WaitAsync();
            try
            {
                var fileStatus = _fileStatuses[filename];
                var position = fileStatus.Item2;
                _fileStatuses[filename] = new ValueTuple<string, int>(status, position);

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
    }
}