using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;
using lmt.db;
using lmt.db.tables;
using Microsoft.EntityFrameworkCore;

namespace lmt
{
    public class Program
    {
        #region Properties

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
        /// A list of all packages.
        /// </summary>
        private static List<Package> Packages { get; set; }

        /// <summary>
        /// A list of all bad package versions.
        /// </summary>
        private static List<PackageBadVersion> PackageBadVersions { get; set; }

        /// <summary>
        /// Full path to the local log file.
        /// </summary>
        private static string LogFile { get; set; }

        #endregion

        /// <summary>
        /// Init all the things..
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        private static void Main(string[] args)
        {
            // Figure out log file.
            LogFile = Path.Combine(
                Directory.GetCurrentDirectory(),
                $"lmt-{DateTimeOffset.Now:yyyy-MM-dd-HH-mm-ss}-{DateTimeOffset.Now.Ticks}.log");

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

            Console.WriteLine();
            Console.WriteLine($"Wrote log to {LogFile}");
        }

        #region Helper functions

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
        /// Get the first package matching the filename-pattern.
        /// </summary>
        /// <param name="filename">Filename to match.</param>
        /// <returns>Package.</returns>
        private static Package GetMatchingPackage(string filename)
        {
            Packages ??= Db.Packages
                .Where(n => !n.Deleted.HasValue)
                .ToList();

            foreach (var package in Packages)
            {
                var patterns = package.GetFiles();

                if (patterns == null ||
                    patterns.Length == 0)
                {
                    continue;
                }

                var add = patterns
                    .Any(pattern =>
                        new Regex(pattern).IsMatch(filename));

                if (add)
                {
                    return package;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if the entry and package represent a bad version.
        /// </summary>
        /// <param name="package">Package.</param>
        /// <param name="entry">File entry.</param>
        /// <param name="index">Index of folder to scan.</param>
        /// <returns>Success.</returns>
        private static bool IsBadVersion(Package package, FileEntry entry, int index)
        {
            if (package == null ||
                entry == null)
            {
                return false;
            }

            PackageBadVersions ??= Db.PackageBadVersions
                .Where(n => !n.Deleted.HasValue)
                .ToList();

            var fpl = PackageBadVersions
                .Where(n => n.PackageId == package.Id)
                .ToList();

            if (!fpl.Any())
            {
                return false;
            }

            foreach (var fp in fpl)
            {
                bool? ibv1 = null;
                bool? ibv2 = null;
                bool? ibv3 = null;
                bool? ibv4 = null;

                Version v1;
                Version v2;
                int vc;

                // Check FileVersionFrom.
                if (fp.FileVersionFrom != null)
                {
                    ibv1 = true;

                    try
                    {
                        v1 = new Version(entry.FileVersion);
                        v2 = new Version(fp.FileVersionFrom);

                        // > 0 = v1 is greater.
                        // < 0 = v1 is lesser.
                        // = 0 = v1 and v2 is equal.
                        vc = v1.CompareTo(v2);

                        if (vc <= 0)
                        {
                            ibv1 = false;
                        }
                    }
                    catch
                    {
                        //
                    }
                }

                // Check FileVersionTo.
                if (fp.FileVersionTo != null)
                {
                    ibv2 = true;

                    try
                    {
                        v1 = new Version(entry.FileVersion);
                        v2 = new Version(fp.FileVersionTo);

                        // > 0 = v1 is greater.
                        // < 0 = v1 is lesser.
                        // = 0 = v1 and v2 is equal.
                        vc = v1.CompareTo(v2);

                        if (vc > 0)
                        {
                            ibv2 = false;
                        }
                    }
                    catch
                    {
                        //
                    }
                }

                // Check ProductVersionFrom.
                if (fp.ProductVersionFrom != null)
                {
                    ibv3 = true;

                    try
                    {
                        v1 = new Version(entry.ProductVersion);
                        v2 = new Version(fp.ProductVersionFrom);

                        // > 0 = v1 is greater.
                        // < 0 = v1 is lesser.
                        // = 0 = v1 and v2 is equal.
                        vc = v1.CompareTo(v2);

                        if (vc <= 0)
                        {
                            ibv3 = false;
                        }
                    }
                    catch
                    {
                        //
                    }
                }

                // Check ProductVersionTo.
                if (fp.ProductVersionTo != null)
                {
                    ibv4 = true;

                    try
                    {
                        v1 = new Version(entry.ProductVersion);
                        v2 = new Version(fp.ProductVersionTo);

                        // > 0 = v1 is greater.
                        // < 0 = v1 is lesser.
                        // = 0 = v1 and v2 is equal.
                        vc = v1.CompareTo(v2);

                        if (vc > 0)
                        {
                            ibv4 = false;
                        }
                    }
                    catch
                    {
                        //
                    }
                }

                // If either the file or product version is within the limits, return true.
                var retval = false;

                /*
                 * FILE VERSION
                 */

                // From is lesser than v, no upper limit.
                if (ibv1.HasValue &&
                    ibv1.Value &&
                    !ibv2.HasValue)
                {
                    retval = true;
                }

                // From is lesser than v, to is larger.
                else if (ibv1.HasValue &&
                         ibv1.Value &&
                         ibv2.HasValue &&
                         ibv2.Value)
                {
                    retval = true;
                }

                // From has not limit, to is larger than v.
                else if (!ibv1.HasValue &&
                         ibv2.HasValue &&
                         ibv2.Value)
                {
                    retval = true;
                }

                /*
                 * PRODUCT VERSION
                 */

                // From is lesser than v, no upper limit.
                if (ibv3.HasValue &&
                    ibv3.Value &&
                    !ibv4.HasValue)
                {
                    retval = true;
                }

                // From is lesser than v, to is larger.
                else if (ibv3.HasValue &&
                         ibv3.Value &&
                         ibv4.HasValue &&
                         ibv4.Value)
                {
                    retval = true;
                }

                // From has not limit, to is larger than v.
                else if (!ibv3.HasValue &&
                         ibv4.HasValue &&
                         ibv4.Value)
                {
                    retval = true;
                }

                if (!retval)
                {
                    continue;
                }

                WriteError(
                    "BAD PACKAGE",
                    new Exception($"{package.Name} - FileVersion: from:{fp.FileVersionFrom ?? "*"}" +
                                  $" - to:{fp.FileVersionTo ?? "*"}" +
                                  $" - ProductVersion: from:{fp.ProductVersionFrom ?? "*"}" +
                                  $" - to:{fp.ProductVersionTo ?? "*"}"),
                    index);

                return retval;
            }

            return false;
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

        #endregion

        #region Scanning functions

        /// <summary>
        /// Update db with information about this file and its origin.
        /// </summary>
        /// <param name="index">Index of folder to scan.</param>
        /// <param name="info">General information about the file.</param>
        /// <param name="ver">Version information about the file.</param>
        private static void ReportToDb(int index, FileInfo info, FileVersionInfo ver)
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

            // Get the first package matching the filename-pattern.
            var package = GetMatchingPackage(fileName);

            if (package == null)
            {
                WriteWarning(
                    $"No package found for {fileName}",
                    index);
            }

            // Get or create a new entry.
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

            if (package != null)
            {
                entry.PackageId = package.Id;
            }

            entry.LastScan = DateTimeOffset.Now;

            if (entry.Id == 0)
            {
                Db.FileEntries.Add(entry);
            }

            Db.SaveChanges();

            if (IsBadVersion(package, entry, index))
            {
                WriteError(
                    "BAD VERSION",
                    new Exception($"{entry.FileName} - FileVersion: {entry.FileVersion} - ProductVersion: {entry.ProductVersion} - Path: {filePath}"),
                    index);
            }
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
                    index,
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

            LogToDisk($"[{index}] Scanning {folder.Path}");

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

                LogToDisk($"[{index}] Files: 0 - Aborting!");

                return;
            }

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"[{index}] ");

            Console.ResetColor();
            Console.WriteLine($"Processing {files.Length} files..");

            LogToDisk($"[{index}] Processing {files.Length} files..");

            foreach (var file in files)
            {
                ScanFile(index, file);
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

        #endregion

        #region Log and Error functions

        /// <summary>
        /// Log a line to disk.
        /// </summary>
        /// <param name="message">Message to log.</param>
        private static void LogToDisk(string message)
        {
            try
            {
                File.AppendAllText(
                    LogFile,
                    message + Environment.NewLine);
            }
            catch
            {
                //
            }
        }

        /// <summary>
        /// Write an exception to console.
        /// </summary>
        /// <param name="ex">Exception to write.</param>
        /// <param name="index">Index of folder to scan.</param>
        private static void WriteError(Exception ex, int? index = null)
        {
            WriteError(
                null,
                ex,
                index);
        }

        /// <summary>
        /// Write an exception to console.
        /// </summary>
        /// <param name="tag">Tag for type. Defaults to 'ERROR'.</param>
        /// <param name="ex">Exception to write.</param>
        /// <param name="index">Index of folder to scan.</param>
        private static void WriteError(string tag, Exception ex, int? index = null)
        {
            var str = string.Empty;

            if (index.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"[{index}] ");

                str += $"[{index}] ";
            }

            tag ??= "ERROR";

            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write($"[{tag}] ");

            Console.ResetColor();
            Console.WriteLine(ex?.Message);

            str += $"[{tag}] {ex?.Message}";
            LogToDisk(str);

            if (ex?.InnerException == null)
            {
                return;
            }

            var pad = $"[{tag}] ";

            if (index.HasValue)
            {
                pad += "   ";

                for (var i = 0; i < index.Value.ToString().Length; i++)
                {
                    pad += " ";
                }
            }

            Console.WriteLine($"{pad}{ex.InnerException.Message}");

            str = string.Empty;

            if (index.HasValue)
            {
                str += $"[{index}] ";
            }

            str += $"[{tag}] {ex.InnerException.Message}";
            LogToDisk(str);
        }

        /// <summary>
        /// Write a warning to console.
        /// </summary>
        /// <param name="message">Message to write.</param>
        /// <param name="index">Index of folder to scan.</param>
        private static void WriteWarning(string message, int? index = null)
        {
            var str = string.Empty;

            if (index.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"[{index}] ");

                str += $"[{index}] ";
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("[WARNING] ");

            Console.ResetColor();
            Console.WriteLine(message);

            str += $"[WARNING] {message}";
            LogToDisk(str);
        }

        #endregion
    }
}