// csc /langversion:5 PathShadowDetector.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PathShadowDetector
{
    class Program
    {
        private static readonly string[] ExecutableExtensions = { ".exe", ".cmd", ".bat", ".dll" };
        
        static void Main(string[] args)
        {
            Console.WriteLine("Windows PATH Shadow Detector");
            Console.WriteLine("===========================");
            Console.WriteLine();
            
            try
            {
                var pathDirectories = GetPathDirectories();
                var shadowedFiles = FindShadowedFiles(pathDirectories);
                
                if (shadowedFiles.Count == 0)
                {
                    Console.WriteLine("No shadowed files found in PATH.");
                }
                else
                {
                    Console.WriteLine(string.Format("Found {0} file(s) with shadows:", shadowedFiles.Count));
                    Console.WriteLine();
                    
                    foreach (var kvp in shadowedFiles)
                    {
                        string fileName = kvp.Key;
                        var filePaths = kvp.Value;
                        
                        Console.WriteLine(string.Format("File: {0}", fileName));
                        Console.WriteLine(string.Format("  Active (first in PATH): {0}", filePaths[0]));
                        
                        for (int i = 1; i < filePaths.Count; i++)
                        {
                            Console.WriteLine(string.Format("  Shadowed: {0}", filePaths[i]));
                        }
                        Console.WriteLine();
                    }
                }
                
                Console.WriteLine(string.Format("Searched {0} PATH directories.", pathDirectories.Count));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Error: {0}", ex.Message));
            }
        }
        
        private static List<string> GetPathDirectories()
        {
            var pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVariable))
            {
                throw new InvalidOperationException("PATH environment variable is not set or empty.");
            }
            
            var directories = pathVariable.Split(';')
                .Where(dir => !string.IsNullOrWhiteSpace(dir))
                .Where(Directory.Exists)
                .ToList();
            
            Console.WriteLine(string.Format("Found {0} valid directories in PATH:", directories.Count));
            foreach (var dir in directories)
            {
                Console.WriteLine(string.Format("  {0}", dir));
            }
            Console.WriteLine();
            
            return directories;
        }
        
        private static Dictionary<string, List<string>> FindShadowedFiles(List<string> pathDirectories)
        {
            var fileOccurrences = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var directory in pathDirectories)
            {
                try
                {
                    var files = Directory.GetFiles(directory)
                        .Where(file => ExecutableExtensions.Contains(
                            Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    
                    foreach (var filePath in files)
                    {
                        var fileName = Path.GetFileName(filePath);
                        
                        if (!fileOccurrences.ContainsKey(fileName))
                        {
                            fileOccurrences[fileName] = new List<string>();
                        }
                        
                        fileOccurrences[fileName].Add(filePath);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine(string.Format("Warning: Access denied to directory: {0}", directory));
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine(string.Format("Warning: Directory not found: {0}", directory));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("Warning: Error reading directory {0}: {1}", directory, ex.Message));
                }
            }
            
            // Return only files that have shadows (appear in multiple directories)
            return fileOccurrences
                .Where(kvp => kvp.Value.Count > 1)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}