using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

class KillAll
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: KillAll.exe process_name [process_name2 ...]");
            Console.WriteLine("Example: KillAll.exe notepad chrome");
            return 1;
        }

        bool anySuccess = false;
        bool anyFailure = false;
        object lockObj = new object();
        List<Thread> threads = new List<Thread>();

        foreach (string arg in args)
        {
            string processName = arg;
            
            // Add .exe extension if not present
            if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processName = processName + ".exe";
            }

            // Remove .exe for Process.GetProcessesByName (it doesn't expect the extension)
            string processNameWithoutExt = processName.Substring(0, processName.Length - 4);

            Thread thread = new Thread(() =>
            {
                Console.WriteLine("Killing all instances of " + processName + "...");

                try
                {
                    Process[] processes = Process.GetProcessesByName(processNameWithoutExt);
                    
                    if (processes.Length == 0)
                    {
                        Console.WriteLine("No running instances of " + processName + " found");
                        lock (lockObj)
                        {
                            anyFailure = true;
                        }
                    }
                    else
                    {
                        foreach (Process process in processes)
                        {
                            try
                            {
                                process.Kill();
                                process.WaitForExit(1000);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Failed to kill process " + process.Id + ": " + ex.Message);
                                lock (lockObj)
                                {
                                    anyFailure = true;
                                }
                            }
                        }
                        Console.WriteLine("Successfully killed " + processes.Length + " instance(s) of " + processName);
                        lock (lockObj)
                        {
                            anySuccess = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error killing " + processName + ": " + ex.Message);
                    lock (lockObj)
                    {
                        anyFailure = true;
                    }
                }
            });

            thread.Start();
            threads.Add(thread);
        }

        // Wait for all threads to complete
        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        Console.WriteLine("Done.");
        return anyFailure ? 1 : 0;
    }
}
