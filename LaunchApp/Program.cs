using System.ComponentModel;
using System.Diagnostics;

namespace LaunchApp
{
    internal class Program
    {
        static void Main(string[] args)
        {

            string? fileName = null;
            string? arguments = null;
            string? workingDirectory = null;

            foreach (var arg in args)
            {
                if (arg.StartsWith("/filename=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--filename=", StringComparison.OrdinalIgnoreCase))
                {
                    int index = arg.IndexOf('=');
                    fileName = arg.Substring(index + 1).Trim('"');
                    continue;
                }
                    

                if (arg.StartsWith("/arguments=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--arguments=", StringComparison.OrdinalIgnoreCase))
                {
                    int index = arg.IndexOf('=');
                    arguments = arg.Substring(index + 1).Trim('"');
                    continue;
                }

                if (arg.StartsWith("/directory=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--directory=", StringComparison.OrdinalIgnoreCase))
                {
                    int index = arg.IndexOf('=');
                    workingDirectory = arg.Substring(index + 1).Trim('"');
                    continue;
                }

                if (arg.StartsWith("/help", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--help", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("LaunchApp.exe");
                    Console.WriteLine();

                    Console.WriteLine("/filename | --filename : Executable path");
                    Console.WriteLine("[/arguments | --arguments] : Executable arguments");
                    Console.WriteLine("[/directory | --directory] : Executable working directory");
                    Console.WriteLine("[/help | --help] : Show this help");

                    Console.WriteLine();
                    Console.WriteLine("Example :");
                    Console.WriteLine($"LaunchApp.exe /filename=\"C:\\Windows\\System32\\mmc.exe\" /arguments=\"C:\\Windows\\System32\\diskmgmt.msc\"");
                    Console.WriteLine($"LaunchApp.exe --filename=\"C:\\Windows\\System32\\mmc.exe\" --arguments=\"C:\\Windows\\System32\\diskmgmt.msc\"");
                    continue;
                }
            }


            if (string.IsNullOrWhiteSpace(fileName))
            {
                Console.WriteLine("Erreur : le paramètre /filename= est requis.");
                Environment.Exit(1);
            }
            
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo()
                {
                    FileName = fileName,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                if (!string.IsNullOrEmpty(arguments))
                    startInfo.Arguments = arguments;

                if (!string.IsNullOrEmpty(workingDirectory))
                    startInfo.WorkingDirectory = workingDirectory;

                Process.Start(
                    startInfo
                );
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(ex.NativeErrorCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(2);
            }

            Environment.Exit(0);
        }
    }
}
