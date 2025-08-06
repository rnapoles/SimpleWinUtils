// csc PathShadowDetector.cs

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
                    Console.WriteLine($"Found {shadowedFiles.Count} file(s) with shadows:");
                    Console.WriteLine();
                    
                    foreach (var kvp in shadowedFiles)
                    {
                        string fileName = kvp.Key;
                        var filePaths = kvp.Value;
                        
                        Console.WriteLine($"File: {fileName}");
                        Console.WriteLine($"  Active (first in PATH): {filePaths[0]}");
                        
                        for (int i = 1; i < filePaths.Count; i++)
                        {
                            Console.WriteLine($"  Shadowed: {filePaths[i]}");
                        }
                        Console.WriteLine();
                    }
                }
                
                Console.WriteLine($"Searched {pathDirectories.Count} PATH directories.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
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
            
            Console.WriteLine($"Found {directories.Count} valid directories in PATH:");
            foreach (var dir in directories)
            {
                Console.WriteLine($"  {dir}");
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
                    Console.WriteLine($"Warning: Access denied to directory: {directory}");
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine($"Warning: Directory not found: {directory}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error reading directory {directory}: {ex.Message}");
                }
            }
            
            // Return only files that have shadows (appear in multiple directories)
            return fileOccurrences
                .Where(kvp => kvp.Value.Count > 1)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}