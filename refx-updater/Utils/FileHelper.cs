using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace refx_updater.Utils
{
    public static class FileHelper
    {
        public static void LinkFolders(string osuFolder)
        {
            var folders = new[]
            {
                "Songs", "Replays", "Skins", "Screenshots"
            };
            
            foreach (var folder in folders)
            {
                var sourcePath = Path.Combine(osuFolder, folder);
                var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
                
                if (Directory.Exists(sourcePath))
                {
                    CreateDirectoryLink(targetPath, sourcePath);
                    Console.WriteLine($"Created folder link {folder}.");
                }
            }
        }

        public static void CopyFiles(string osuFolder)
        {
            var files = new[]
            {
                $"osu!.{Environment.UserName}.cfg",
                /*"osu!.db", TODO: shits updated, i need to update the client*/ 
                "collection.db"
            };
            
            foreach (var file in files)
            {
                var sourcePath = Path.Combine(osuFolder, file);
                var targetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                
                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, targetPath, true);
                    Console.WriteLine($"Copied {file}!");
                }
            }
        }

        public static string ChooseFolder()
        {
            using (var dialog = new FolderBrowserDialog 
            { 
                Description = "Select the osu! folder", 
                ShowNewFolderButton = false 
            })
            {
                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : string.Empty;
            }
        }

        private static void CreateDirectoryLink(string targetPath, string sourcePath)
        {
            var startInfo = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + targetPath + "\" \"" + sourcePath + "\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using (var process = Process.Start(startInfo))
            {
                process?.WaitForExit();
            }
        }
    }
}