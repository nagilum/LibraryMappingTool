using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using lmt.db;
using lmt.db.tables;
using Microsoft.EntityFrameworkCore;

namespace lmt
{
    public class Program
    {
        /// <summary>
        /// Loaded config.
        /// </summary>
        public static Config LoadedConfig { get; set; }

        /// <summary>
        /// Comma-separater IP addresses.
        /// </summary>
        private static string LocalIpAddresses { get; set; }

        /// <summary>
        /// Database connection.
        /// </summary>
        private static DatabaseContext Db { get; set; }

        /// <summary>
        /// Init all the things..
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        private static void Main(string[] args)
        {
            // Disable the console cursor, for smoother refreshing.
            DisableCursor();

            // Load config from disk.
            if (!LoadConfig(args))
            {
                return;
            }

            // Attempt to connect to the db.
            if (!OpenDbConnection())
            {
                return;
            }

            // Cycle through all network interfaces and gather all local IP addresses.
            GetAllLocalIpAddresses();

            // Scan the available folders from loaded config.
            ScanFolders();
        }

        /// <summary>
        /// Disable the console cursor, for smoother refreshing.
        /// </summary>
        private static void DisableCursor()
        {
            try
            {
                Console.CursorVisible = false;
            }
            catch
            {
                //
            }
        }

        /// <summary>
        /// Cycle through all network interfaces and gather all local IP addresses.
        /// </summary>
        private static void GetAllLocalIpAddresses()
        {
            var ips = new List<string>();
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                            n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            foreach (var nic in nics)
            {
                foreach (var adrinfo in nic.GetIPProperties().UnicastAddresses)
                {
                    var ip = adrinfo.Address.ToString();

                    if (!ips.Contains(ip))
                    {
                        ips.Add(ip);
                    }
                }
            }

            LocalIpAddresses = string.Join(", ", ips.OrderBy(n => n));
        }

        /// <summary>
        /// Load config from disk.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Success.</returns>
        private static bool LoadConfig(string[] args)
        {
            try
            {
                var path = args.Length > 0
                    ? string.Join(" ", args)
                    : Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "config.json");

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"Unable to find config file: {path}");
                }

                LoadedConfig = JsonSerializer.Deserialize<Config>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (LoadedConfig == null)
                {
                    throw new Exception($"Unable to load config from file: {path}");
                }

                if (LoadedConfig.Database == null)
                {
                    throw new Exception("The 'database' group is required.");
                }

                if (LoadedConfig.Folders?.Length == 0)
                {
                    throw new Exception("The 'folders' group is required and must have at least 1 entry.");
                }

                return true;
            }
            catch (Exception ex)
            {
                WriteError(ex);
                return false;
            }
        }

        /// <summary>
        /// Attempt to connect to the db.
        /// </summary>
        /// <returns>Success.</returns>
        private static bool OpenDbConnection()
        {
            try
            {
                Db = new DatabaseContext();
                Db.Database.OpenConnection();

                return true;
            }
            catch (Exception ex)
            {
                WriteError(ex);
                return false;
            }
        }

        /// <summary>
        /// Update db with information about this file and its origin.
        /// </summary>
        /// <param name="info">General information about the file.</param>
        /// <param name="ver">Version information about the file.</param>
        private static void ReportToDb(FileInfo info, FileVersionInfo ver)
        {
            var fileVersion = string.Format(
                    "{0}.{1}.{2}.{3}",
                    ver.FileMajorPart,
                    ver.FileMinorPart,
                    ver.FileBuildPart,
                    ver.FilePrivatePart);

            var productVersion = string.Format(
                "{0}.{1}.{2}.{3}",
                ver.ProductMajorPart,
                ver.ProductMinorPart,
                ver.ProductBuildPart,
                ver.ProductPrivatePart);

            var filePath = info.DirectoryName?.ToLower();
            var fileName = info.Name.ToLower();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new Exception($"Unable to get file path for {fileName}");
            }

            var entry = Db.FileEntries
                            .FirstOrDefault(n => n.ServerName == Environment.MachineName &&
                                                 n.FilePath == filePath &&
                                                 n.FileName == fileName &&
                                                 n.FileVersion == fileVersion &&
                                                 n.ProductVersion == productVersion) ??
                        new FileEntry
                        {
                            Created = DateTimeOffset.Now,
                            ServerName = Environment.MachineName,
                            ServerIps = LocalIpAddresses,
                            FilePath = filePath,
                            FileName = fileName,
                            FileSize = info.Length,
                            FileVersion = fileVersion,
                            FileVersionMajor = ver.FileMajorPart,
                            FileVersionMinor = ver.FileMinorPart,
                            FileVersionBuild = ver.FileBuildPart,
                            FileVersionPrivate = ver.FilePrivatePart,
                            ProductVersion = productVersion,
                            ProductVersionMajor = ver.ProductMajorPart,
                            ProductVersionMinor = ver.ProductMinorPart,
                            ProductVersionBuild = ver.ProductBuildPart,
                            ProductVersionPrivate = ver.ProductPrivatePart
                        };

            entry.LastScan = DateTimeOffset.Now;

            if (entry.Id == 0)
            {
                Db.FileEntries.Add(entry);
            }

            Db.SaveChanges();
        }

        /// <summary>
        /// Get versions for file and report to db.
        /// </summary>
        /// <param name="index">Index of folder to scan.</param>
        /// <param name="path">Full file path.</param>
        private static void ScanFile(int index, string path)
        {
            try
            {
                ReportToDb(
                    new FileInfo(path),
                    FileVersionInfo.GetVersionInfo(path));
            }
            catch (Exception ex)
            {
                WriteError(ex, index);
            }
        }

        /// <summary>
        /// Scan a single folder entry.
        /// </summary>
        /// <param name="index">Index of folder to scan.</param>
        /// <param name="folder">Folder entry to scan.</param>
        private static void ScanFolder(int index, Config.FolderEntry folder)
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"[{index}] ");

            Console.ResetColor();
            Console.WriteLine($"Scanning {folder.Path}");

            string[] files;

            try
            {
                if (!Directory.Exists(folder.Path))
                {
                    throw new FileNotFoundException($"Folder does not exist: {folder.Path}");
                }

                files = folder.IncludeSubfolders.HasValue &&
                        folder.IncludeSubfolders.Value
                    ? Directory.GetFiles(folder.Path, "*.dll", SearchOption.AllDirectories)
                    : Directory.GetFiles(folder.Path, "*.dll", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                WriteError(ex, index);
                return;
            }

            if (files.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"[{index}] ");

                Console.ResetColor();
                Console.WriteLine("Files: 0 - Aborting!");

                return;
            }

            var top = Console.CursorTop;
            var processed = 0;
            var max = files.Length;

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"[{index}] ");

            Console.ResetColor();
            Console.Write($"Files Processed: 0 of {max}");

            foreach (var file in files)
            {
                ScanFile(index, file);

                processed++;

                Console.CursorTop = top;
                Console.CursorLeft = 0;

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"[{index}] ");

                Console.ResetColor();
                Console.Write($"Files Processed: {processed} of {max}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Scan the available folders from loaded config.
        /// </summary>
        private static void ScanFolders()
        {
            var index = 0;

            foreach (var folder in LoadedConfig.Folders)
            {
                ScanFolder(++index, folder);
            }
        }

        /// <summary>
        /// Write an exception to console.
        /// </summary>
        /// <param name="ex">Exception to write.</param>
        /// <param name="index">Index of folder to scan.</param>
        private static void WriteError(Exception ex, int? index = null)
        {
            if (index.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"[{index}] ");
            }

            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write("[ERROR] ");

            Console.ResetColor();
            Console.WriteLine(ex?.Message);

            if (ex?.InnerException == null)
            {
                return;
            }

            var pad = "        ";

            if (index.HasValue)
            {
                pad += "   ";

                for (var i = 0; i < index.Value.ToString().Length; i++)
                {
                    pad += " ";
                }
            }

            Console.WriteLine($"{pad}{ex.InnerException.Message}");
        }
    }
}