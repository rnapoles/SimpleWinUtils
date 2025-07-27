using System;
using System.Collections.Generic;
using Microsoft.Win32;

class Program
{
    static void Main()
    {
        Console.WriteLine("Extensión\tAplicación asociada");
        Console.WriteLine("---------\t--------------------");

        foreach (string extension in GetAllExtensions())
        {
            string progId = GetProgId(extension);
            string command = GetAssociatedCommand(progId);

            if (!string.IsNullOrEmpty(command))
            {
                Console.WriteLine("{0,-10}\t{1}", extension, command);
            }
        }

        Console.WriteLine("\nPresiona una tecla para salir...");
        Console.ReadKey();
    }

    static IEnumerable<string> GetAllExtensions()
    {
        List<string> extensions = new List<string>();
        RegistryKey classesRoot = Registry.ClassesRoot;

        foreach (string keyName in classesRoot.GetSubKeyNames())
        {
            if (keyName.StartsWith("."))
            {
                extensions.Add(keyName);
            }
        }

        return extensions;
    }

    static string GetProgId(string extension)
    {
        using (RegistryKey extKey = Registry.ClassesRoot.OpenSubKey(extension))
        {
            if (extKey != null)
            {
                object defaultValue = extKey.GetValue("");
                if (defaultValue != null)
                {
                    return defaultValue.ToString();
                }
            }
        }
        return null;
    }

    static string GetAssociatedCommand(string progId)
    {
        if (string.IsNullOrEmpty(progId))
            return null;

        string commandPath = progId + @"\shell\open\command";
        using (RegistryKey cmdKey = Registry.ClassesRoot.OpenSubKey(commandPath))
        {
            if (cmdKey != null)
            {
                object command = cmdKey.GetValue("");
                if (command != null)
                {
                    return command.ToString();
                }
            }
        }
        return null;
    }
}
