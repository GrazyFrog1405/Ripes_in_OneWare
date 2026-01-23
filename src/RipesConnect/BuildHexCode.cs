using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Media;
using OneWare.Essentials.Services;

namespace RipesConnect
{
    public class BuildHexCode
    {
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static void BuildHex(string? gccPath, string asmFilePath, IOutputService? outputService)
        {
            outputService?.WriteLine($"Start build for: {asmFilePath}...");

            try
            {
                // 1. Validierung
                if (string.IsNullOrEmpty(gccPath) || !File.Exists(gccPath))
                {
                    outputService?.WriteLine("[Build error] GCC compiler not found! Please install it under 'Packages' or check the path in the settings (Compiler).", textColor: Brushes.Red);
                    return;
                }

                var asmDir = Path.GetDirectoryName(asmFilePath)!;
                var elfFilePath = Path.ChangeExtension(asmFilePath, ".elf");
                var memFilePath = Path.Combine(asmDir, "code.mem");

                // 2. Pfade ableiten
                var binDir = Path.GetDirectoryName(gccPath)!;
                var gccFileName = Path.GetFileName(gccPath);
                var objCopyFileName = gccFileName.Replace("gcc", "objcopy"); 
                var objCopyPath = Path.Combine(binDir, objCopyFileName);

                if (!File.Exists(objCopyPath))
                {
                    outputService?.WriteLine($"[Build Error] 'objcopy' not found at: {objCopyPath}", textColor: Brushes.Red);
                    return;
                }

                // 3. Linux Permissions sicherstellen
                if (!IsWindows)
                {
                    EnsureExecutable(gccPath);
                    EnsureExecutable(objCopyPath);
                }

                // 4. GCC ausführen (Assemblieren -> ELF)
                RunProcess(
                    gccPath,
                    $"-x assembler -march=rv32i -mabi=ilp32 -nostdlib -Ttext=0x0 \"{asmFilePath}\" -o \"{elfFilePath}\"",
                    asmDir,
                    "GCC", outputService);

                // ---------------------------------------------------------
                // NEU: 4a. Alte Datei löschen (Fix für Linux Nummerierung)
                // ---------------------------------------------------------
                try
                {
                    if (File.Exists(memFilePath))
                    {
                        File.Delete(memFilePath);
                    }
                }
                catch (Exception ex)
                {
                    outputService?.WriteLine($"[Warning] Could not delete old code.mem: {ex.Message}", textColor: Brushes.Orange);
                    // Wir machen trotzdem weiter, vielleicht klappt das Überschreiben ja doch
                }
                // ---------------------------------------------------------

                // 5. ObjCopy ausführen (ELF -> MEM/Hex)
                RunProcess(
                    objCopyPath,
                    $"-O verilog \"{elfFilePath}\" \"{memFilePath}\"",
                    asmDir,
                    "OBJCOPY", outputService);

                outputService?.WriteLine("Build completed successfully! 'code.mem' was created.", textColor: Brushes.Green);
            }
            catch (Exception ex)
            {
                outputService?.WriteLine($"[BuildHex ERROR] {ex.Message}", textColor: Brushes.Red);
            }
        }

        // ... Rest der Klasse (RunProcess, EnsureExecutable) bleibt gleich ...
        private static void RunProcess(string exe, string args, string workingDir, string toolName, IOutputService? outputService)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workingDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);

            if (process == null)
                throw new Exception($"{toolName} It could not be started.");

            string stdOut = process.StandardOutput.ReadToEnd();
            string stdErr = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdOut))
                outputService?.WriteLine($"[{toolName}] {stdOut.Trim()}", textColor: Brushes.Gray);

            if (!string.IsNullOrWhiteSpace(stdErr))
                outputService?.WriteLine($"[{toolName} MSG] {stdErr.Trim()}", textColor: Brushes.Orange); 

            if (process.ExitCode != 0)
                throw new Exception($"{toolName} Failed with exit code {process.ExitCode}.");
        }

        private static void EnsureExecutable(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;
                var psi = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi)?.WaitForExit();
            }
            catch { /* Ignorieren */ }
        }
    }
}
