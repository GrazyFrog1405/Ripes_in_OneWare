using OneWare.Essentials.Services;

namespace RipesConnect;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Media;

public class Ripes
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    
    public static void RunRipes(string? ripesPath, string? filePath, IOutputService? outputService)
    {
        try
        {
            // 1. Validierung: Haben wir einen gültigen Pfad aus den Settings bekommen?
            if (string.IsNullOrEmpty(ripesPath) || !File.Exists(ripesPath))
            {
                outputService?.WriteLine(
                    "[Ripes Error] Der Pfad zu Ripes wurde nicht gefunden. " +
                    "Bitte installiere Ripes über die Einstellungen (Extras -> Extensions -> Binaries)",
                    textColor: Brushes.Red);
                outputService?.WriteLine(
                    "Oder bei Linux README Datei anschauen zur installation", 
                    textColor: Brushes.Red);
                return;
            }
            
            // 2. Linux: Ausführbar machen (Sicherheitsnetz)
            if (!IsWindows)
            {
                EnsureExecutable(ripesPath);
            }

            // Basis-Verzeichnis der Installation ermitteln
            var ripesDir = Path.GetDirectoryName(ripesPath);
            if (ripesDir == null) return;

            var psi = new ProcessStartInfo
            {
                FileName = ripesPath,    // Hier nutzen wir den übergebenen Pfad!
                WorkingDirectory = ripesDir, 
                UseShellExecute = false,
                CreateNoWindow = false
            };
            
            if (!IsWindows)
            {
                if (!string.IsNullOrEmpty(psi.Arguments))
                {
                    psi.Arguments = $"--appimage-extract-and-run {psi.Arguments}";
                }
                else
                {
                    psi.Arguments = "--appimage-extract-and-run";
                }
            }
            
            // 3. Wenn eine Datei geöffnet werden soll
            if (IsWindows)
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    // WorkingDirectory auf den Ordner der Projektdatei setzen (oft hilfreich für Ripes)
                    psi.WorkingDirectory = Path.GetDirectoryName(filePath) ?? ripesDir;
                    psi.Arguments = $"\"{filePath}\"";
                }
            }

            // 4. Umgebungsvariablen
            // PATH erweitern (PathSeparator ist ';' bei Win, ':' bei Linux)
            var currentPath = psi.EnvironmentVariables["PATH"] ?? "";
            if (!currentPath.Contains(ripesDir))
            {
                psi.EnvironmentVariables["PATH"] = ripesDir + Path.PathSeparator + currentPath;
            }

            // Windows-spezifische Qt-Fixes (unter Linux NICHT setzen, da AppImage)
            if (IsWindows)
            {
                var platformsDir = Path.Combine(ripesDir, "platforms");
                // Nur setzen, wenn der platforms Ordner existiert
                if (Directory.Exists(platformsDir))
                {
                    psi.EnvironmentVariables["QT_PLUGIN_PATH"] = platformsDir;
                    psi.EnvironmentVariables["QT_QPA_PLATFORM"] = "windows";
                }
            }

            outputService?.WriteLine($"Ripes wird gestartet von: {ripesPath}", textColor: Brushes.Green);
            
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            outputService?.WriteLine($"[Ripes CRITICAL ERROR] {ex.Message}", textColor: Brushes.Red);
        }
    }

    // Hilfsmethode für Linux-Rechte (bleibt unverändert)
    private static void EnsureExecutable(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            var psi = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"a+x \"{filePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi)?.WaitForExit();
        }
        catch
        {
            // Fehler ignorieren, wir versuchen trotzdem zu starten
        }
    }
}