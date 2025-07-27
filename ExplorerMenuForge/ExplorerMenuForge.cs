using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

namespace ExplorerMenuForge
{
    class Program
    {
        // Maximum number of entries per registry file
        const int MAX_ENTRIES_PER_FILE = 16;
        // Default menu name prefix
        const string DEFAULT_MENU_PREFIX = "MyMenu";
        // Default menu caption
        const string DEFAULT_MENU_CAPTION = "My New Menu";

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            // Define options that require values
            var optionsWithValues = new Dictionary<string, bool>
            {
                { "-n", true },  // Menu name requires a value
                { "-c", true },  // Menu caption requires a value
                { "-f", true }   // File with paths requires a value
            };

            // Create and parse arguments
            var parser = new ArgumentParser(args, optionsWithValues);
            if (!parser.Parse())
            {
                ShowUsage();
                return;
            }

            // Get menu prefix (name)
            string menuPrefix = DEFAULT_MENU_PREFIX;
            if (parser.HasOption("-n"))
            {
                menuPrefix = parser.GetOptionValue("-n");
            }

            // Get menu caption
            string menuCaption = DEFAULT_MENU_CAPTION;
            if (parser.HasOption("-c"))
            {
                menuCaption = FormatMenuCaption(parser.GetOptionValue("-c"));
            }

            // Get executable paths
            string[] executablePaths;
            if (parser.HasOption("-f"))
            {
                string filePath = parser.GetOptionValue("-f");
                if (!File.Exists(filePath))
                {
                    Console.WriteLine(string.Format("Error: File '{0}' not found.", filePath));
                    return;
                }

                executablePaths = File.ReadAllLines(filePath)
                    .Where(path => !string.IsNullOrWhiteSpace(path) && !path.StartsWith("#"))
                    .Where(path => File.Exists(ResolveExecutablePath(path)))
                    .ToArray();
            }
            else
            {
                // Get non-option arguments as executable paths
                executablePaths = parser.NonOptionArgs;
            }

            // Check if any executable paths were found
            if (executablePaths.Length == 0)
            {
                Console.WriteLine("Error: No executable paths found.");
                return;
            }

            // Sort the paths alphabetically
            Array.Sort(executablePaths);

            // Split the paths into chunks of MAX_ENTRIES_PER_FILE
            List<string[]> pathChunks = new List<string[]>();
            for (int i = 0; i < executablePaths.Length; i += MAX_ENTRIES_PER_FILE)
            {
                int chunkSize = Math.Min(MAX_ENTRIES_PER_FILE, executablePaths.Length - i);
                string[] chunk = new string[chunkSize];
                Array.Copy(executablePaths, i, chunk, 0, chunkSize);
                pathChunks.Add(chunk);
            }

            // Generate registry files for each chunk
            for (int i = 0; i < pathChunks.Count; i++)
            {
                string outputPath = pathChunks.Count == 1 
                    ? "context-menu.reg" 
                    : string.Format("context-menu-{0}.reg", i + 1);
                
                // Use the file number (i+1) for the menu identifier
                string menuId = pathChunks.Count == 1 ? menuPrefix + "1" : string.Format("{0}{1}", menuPrefix, i + 1);
                
                string regFileContent = GenerateRegistryFile(pathChunks[i], menuId, menuCaption);
                File.WriteAllText(outputPath, regFileContent);
                
                Console.WriteLine(string.Format("Registry file generated successfully: {0}", Path.GetFullPath(outputPath)));
            }

            // Generate removal script
            GenerateRemovalScript(pathChunks.Count, menuPrefix);
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  ExplorerMenuForge.exe <executable_path> [<executable_path> ...]");
            Console.WriteLine("  ExplorerMenuForge.exe -f <file_with_paths>");
            Console.WriteLine("  ExplorerMenuForge.exe -n <menu_name> <executable_path> [<executable_path> ...]");
            Console.WriteLine("  ExplorerMenuForge.exe -n <menu_name> -f <file_with_paths>");
            Console.WriteLine("  ExplorerMenuForge.exe -c <menu_caption> <executable_path> [<executable_path> ...]");
            Console.WriteLine("  ExplorerMenuForge.exe -n <menu_name> -c <menu_caption> <executable_path> [<executable_path> ...]");
        }

        /// <summary>
        /// Formats a menu caption to make it more visually appealing
        /// </summary>
        /// <param name="caption">The raw caption string</param>
        /// <returns>A formatted caption string</returns>
        static string FormatMenuCaption(string caption)
        {
            if (string.IsNullOrWhiteSpace(caption))
                return DEFAULT_MENU_CAPTION;

            // Insert spaces before capital letters (for camelCase or PascalCase)
            string spacedCaption = string.Concat(caption.Select((c, i) => 
                i > 0 && char.IsUpper(c) && !char.IsUpper(caption[i-1]) ? " " + c : c.ToString()));
            
            // Convert to title case (first letter of each word capitalized)
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            return textInfo.ToTitleCase(spacedCaption.ToLower());
        }

        /// <summary>
        /// Generates a batch file to remove all registry keys created by this tool
        /// </summary>
        static void GenerateRemovalScript(int numFiles, string menuPrefix)
        {
            StringBuilder sb = new StringBuilder();
            
            // Add batch file header with admin check
            sb.AppendLine("@echo off");
            sb.AppendLine(":: Check for admin privileges");
            sb.AppendLine("net session >nul 2>&1");
            sb.AppendLine("if %errorLevel% neq 0 (");
            sb.AppendLine("    echo This script requires administrator privileges.");
            sb.AppendLine("    echo Please run as administrator.");
            //sb.AppendLine("    pause");
            sb.AppendLine("    exit /b 1");
            sb.AppendLine(")");
            sb.AppendLine();
            
            sb.AppendLine("echo Removing context menu registry keys...");
            sb.AppendLine();
            
            // Remove all menu entries
            for (int i = 1; i <= numFiles; i++)
            {
                string menuId = string.Format("{0}{1}", menuPrefix, i);
                
                // Remove Directory context menu
                sb.AppendLine(string.Format("reg delete \"HKEY_CLASSES_ROOT\\Directory\\shell\\{0}\" /f >nul 2>&1", menuId));
                
                // Remove Directory Background context menu
                sb.AppendLine(string.Format("reg delete \"HKEY_CLASSES_ROOT\\Directory\\background\\shell\\{0}\" /f >nul 2>&1", menuId));
                
                // Remove ContextMenus entries
                sb.AppendLine(string.Format("reg delete \"HKEY_CLASSES_ROOT\\Directory\\ContextMenus\\{0}\" /f >nul 2>&1", menuId));
            }
            
            sb.AppendLine();
            sb.AppendLine("echo Context menu entries have been removed.");
            //sb.AppendLine("pause");
            
            // Write to file
            string outputPath = "remove-context-menu.bat";
            File.WriteAllText(outputPath, sb.ToString());
            
            Console.WriteLine(string.Format("Removal script generated successfully: {0}", Path.GetFullPath(outputPath)));
        }

        static string GenerateRegistryFile(string[] executablePaths, string menuId, string menuCaption)
        {
            StringBuilder sb = new StringBuilder();
            
            // Add registry file header
            sb.AppendLine("Windows Registry Editor Version 5.00");
            sb.AppendLine();
            
            // Add context menu setup for Directory and Directory Background
            sb.AppendLine(";This displays context menu when you right-click Directory");
            sb.AppendLine(string.Format("[HKEY_CLASSES_ROOT\\Directory\\shell\\{0}]", menuId));
            sb.AppendLine(";Directory Level Menu Title");
            sb.AppendLine(string.Format("\"MUIVerb\"=\"{0}\"", menuCaption));
            sb.AppendLine("\"Icon\"=\"cmd.exe\"");
            sb.AppendLine(string.Format("\"ExtendedSubCommandsKey\"=\"Directory\\\\ContextMenus\\\\{0}\"", menuId));
            sb.AppendLine();
            
            sb.AppendLine(";This displays context menu when you right-click Directory Background");
            sb.AppendLine(string.Format("[HKEY_CLASSES_ROOT\\Directory\\background\\shell\\{0}]", menuId));
            sb.AppendLine(";Background Level Menu Title");
            sb.AppendLine(string.Format("\"MUIVerb\"=\"{0}\"", menuCaption));
            sb.AppendLine("\"Icon\"=\"cmd.exe\"");
            sb.AppendLine(string.Format("\"ExtendedSubCommandsKey\"=\"Directory\\\\ContextMenus\\\\{0}\"", menuId));
            sb.AppendLine();
            
            // Add separator after header
            sb.AppendLine(";-----------------------------");
            sb.AppendLine();

            bool isFirstEntry = true;
            foreach (string path in executablePaths)
            {
              
                // Add separator between entries (but not before the first one)
                if (!isFirstEntry)
                {
                    sb.AppendLine(";-----------------------------");
                    sb.AppendLine();
                }
                else
                {
                    isFirstEntry = false;
                }

                // Extract application name from path
                string appName = Path.GetFileNameWithoutExtension(path);

                // Get Full path
                string fullPath = Path.GetFullPath(path);

                // Escape backslashes for registry format
                string escapedPath = fullPath.Replace("\\", "\\\\");

                // Add registry entries
                sb.AppendLine(string.Format("[HKEY_CLASSES_ROOT\\Directory\\ContextMenus\\{0}\\shell\\{1}]", menuId, appName));
                sb.AppendLine(string.Format("\"MUIVerb\"=\"Open with {0}\"", appName));
                sb.AppendLine("\"Position\"=\"top\"");
                sb.AppendLine(string.Format("\"Icon\"=\"{0}\"", escapedPath));
                sb.AppendLine();

                sb.AppendLine(string.Format("[HKEY_CLASSES_ROOT\\Directory\\ContextMenus\\{0}\\shell\\{1}\\command]", menuId, appName));
                sb.AppendLine(string.Format("@=\"{0} \\\"%1\\\"\"", escapedPath));
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Resolves an executable name or path to its full path by searching the system PATH if necessary
        /// </summary>
        /// <param name="executablePath">The executable name or path</param>
        /// <returns>The full path to the executable</returns>
        static string ResolveExecutablePath(string executablePath)
        {
            // If the path already contains directory information or is a full path, return it
            if (Path.IsPathRooted(executablePath) || executablePath.Contains("\\") || executablePath.Contains("/"))
            {
                return executablePath;
            }

            // Make sure we have the .exe extension
            string exeName = executablePath;
            if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                exeName += ".exe";
            }

            // First check if the executable exists in the current directory
            string currentDirPath = Path.Combine(Environment.CurrentDirectory, exeName);
            if (File.Exists(currentDirPath))
            {
                return currentDirPath;
            }

            // Search the PATH environment variable
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return executablePath; // Return original if PATH is not available
            }

            // Split the PATH and search each directory
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    string fullPath = Path.Combine(dir, exeName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch (Exception)
                {
                    // Skip invalid paths in PATH
                    continue;
                }
            }

            // If not found in PATH, return the original
            return executablePath;
        }
    }
}