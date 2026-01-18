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
            outputService?.WriteLine($"Starte Build für: {asmFilePath}...");

            try
            {
                // 1. Validierung: Wurde der GCC Pfad in den Settings gefunden?
                if (string.IsNullOrEmpty(gccPath) || !File.Exists(gccPath))
                {
                    outputService?.WriteLine("[Build Error] GCC Compiler nicht gefunden! Bitte unter 'Packages' installieren oder Pfad in den Settings (Compiler) prüfen.", textColor: Brushes.Red);
                    return;
                }

                var asmDir = Path.GetDirectoryName(asmFilePath)!;
                var elfFilePath = Path.ChangeExtension(asmFilePath, ".elf");
                var memFilePath = Path.Combine(asmDir, "code.mem");

                // 2. Pfade ableiten
                // Wir haben den Pfad zu GCC. ObjCopy liegt immer im selben Ordner.
                var binDir = Path.GetDirectoryName(gccPath)!;
                
                // Trick: Wir nehmen den Namen von GCC (z.B. "riscv-none-elf-gcc.exe") 
                // und ersetzen "gcc" durch "objcopy". Das erhält automatisch die .exe Endung auf Windows.
                var gccFileName = Path.GetFileName(gccPath);
                var objCopyFileName = gccFileName.Replace("gcc", "objcopy"); 
                var objCopyPath = Path.Combine(binDir, objCopyFileName);

                if (!File.Exists(objCopyPath))
                {
                    outputService?.WriteLine($"[Build Error] 'objcopy' nicht gefunden unter: {objCopyPath}", textColor: Brushes.Red);
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

                // 5. ObjCopy ausführen (ELF -> MEM/Hex)
                RunProcess(
                    objCopyPath,
                    $"-O verilog \"{elfFilePath}\" \"{memFilePath}\"",
                    asmDir,
                    "OBJCOPY", outputService);

                outputService?.WriteLine("Build erfolgreich abgeschlossen! 'code.mem' wurde erstellt.", textColor: Brushes.Green);
            }
            catch (Exception ex)
            {
                outputService?.WriteLine($"[BuildHex ERROR] {ex.Message}", textColor: Brushes.Red);
            }
        }

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
                throw new Exception($"{toolName} konnte nicht gestartet werden.");

            string stdOut = process.StandardOutput.ReadToEnd();
            string stdErr = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdOut))
                outputService?.WriteLine($"[{toolName}] {stdOut.Trim()}", textColor: Brushes.Gray);

            // GCC schreibt Warnungen oft in StdErr, aber das ist kein Absturz.
            if (!string.IsNullOrWhiteSpace(stdErr))
                outputService?.WriteLine($"[{toolName} MSG] {stdErr.Trim()}", textColor: Brushes.Orange); 

            if (process.ExitCode != 0)
                throw new Exception($"{toolName} fehlgeschlagen mit Exit Code {process.ExitCode}.");
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
