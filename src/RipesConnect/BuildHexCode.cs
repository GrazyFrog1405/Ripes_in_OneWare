using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Media;
using OneWare.Essentials.Services;

namespace RipesConnect
{
    public class BuildHexCode
    {
        // Hilfseigenschaft: Prüft, ob wir auf Windows sind
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        
        public static void BuildHex(string? gccPath, string asmFilePath, IOutputService? outputService)
        {
            outputService?.WriteLine($"Starte Build für: {asmFilePath}...");

            try
            {
                // 1. Validierung: Existiert der Compiler?
                if (string.IsNullOrEmpty(gccPath) || !File.Exists(gccPath))
                {
                    outputService?.WriteLine("[Build Error] GCC Compiler nicht gefunden! Bitte Pfad in den Settings prüfen.", textColor: Brushes.Red);
                    return;
                }

                // Pfade vorbereiten
                var asmDir = Path.GetDirectoryName(asmFilePath)!;
                var elfFilePath = Path.ChangeExtension(asmFilePath, ".elf");
                var memFilePath = Path.Combine(asmDir, "code.mem");

                // 2. Pfad zu ObjCopy finden
                var binDir = Path.GetDirectoryName(gccPath)!;
                var gccFileName = Path.GetFileName(gccPath);
                // "gcc" durch "objcopy" ersetzen (Groß-/Kleinschreibung ignorieren)
                var objCopyFileName = gccFileName.Replace("gcc", "objcopy", StringComparison.OrdinalIgnoreCase); 
                var objCopyPath = Path.Combine(binDir, objCopyFileName);

                if (!File.Exists(objCopyPath))
                {
                    outputService?.WriteLine($"[Build Error] 'objcopy' nicht gefunden unter: {objCopyPath}", textColor: Brushes.Red);
                    return;
                }

                // 3. Linux/Mac Spezialbehandlung: Ausführbar machen
                if (!IsWindows)
                {
                    EnsureExecutable(gccPath, outputService);
                    EnsureExecutable(objCopyPath, outputService);
                }

                // 4. GCC Argumente definieren
                var gccArgs = new List<string>
                {
                    "-x", "assembler",
                    "-march=rv32im_zicsr", // Sicher für GCC 14+ inkl. CSR Register
                    "-mabi=ilp32",
                    "-nostdlib",
                    "-Ttext=0x0",
                    asmFilePath,
                    "-o", elfFilePath
                };
                
                // GCC Ausführen
                RunProcess(gccPath, gccArgs.ToArray(), asmDir, "GCC", outputService);

                // 5. ObjCopy Argumente definieren
                var objCopyArgs = new[] 
                { 
                    "-O", "verilog",         
                    elfFilePath,             
                    memFilePath              
                };

                // ObjCopy Ausführen
                RunProcess(objCopyPath, objCopyArgs, asmDir, "OBJCOPY", outputService);

                outputService?.WriteLine("Build erfolgreich! 'code.mem' wurde erstellt.", textColor: Brushes.Green);
            }
            catch (Exception ex)
            {
                outputService?.WriteLine($"[BuildHex Exception] {ex.Message}", textColor: Brushes.Red);
            }
        }

        private static void RunProcess(string exe, string[] args, string workingDir, string toolName, IOutputService? outputService)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = workingDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Wichtig: ArgumentList verhindert Probleme mit Leerzeichen/Anführungszeichen auf Linux
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process == null) 
                throw new Exception($"{toolName} konnte nicht gestartet werden.");

            string stdOut = process.StandardOutput.ReadToEnd();
            string stdErr = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdOut))
                outputService?.WriteLine($"[{toolName}] {stdOut.Trim()}", textColor: Brushes.Gray);

            if (!string.IsNullOrWhiteSpace(stdErr))
                outputService?.WriteLine($"[{toolName} MSG] {stdErr.Trim()}", textColor: Brushes.Orange); 

            if (process.ExitCode != 0)
                throw new Exception($"{toolName} fehlgeschlagen mit Exit Code {process.ExitCode}.");
        }

        private static void EnsureExecutable(string filePath, IOutputService? outputService)
        {
            try
            {
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    // .NET 7+ Native Methode
                    var mode = File.GetUnixFileMode(filePath);
                    File.SetUnixFileMode(filePath, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                }
            }
            catch (Exception ex)
            {
                outputService?.WriteLine($"[Warning] SetUnixFileMode fehlgeschlagen: {ex.Message}. Versuche chmod...", textColor: Brushes.Orange);
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    psi.ArgumentList.Add("+x");
                    psi.ArgumentList.Add(filePath);
                    Process.Start(psi)?.WaitForExit();
                }
                catch { /* Ignorieren */ }
            }
        }
    }
}
