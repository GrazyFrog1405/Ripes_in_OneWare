using Avalonia.Media;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
namespace RipesConnect;

public class ProcessorGeneration
{
    public const string ProcessorSettingKey = "Processor_Type_Key";
    public const string NoneOption = "None";
    private const string ProcessorFileName = "processor_top.v";
    
    public static async Task GenerateProcessorAsync(IProjectRoot root, IProjectExplorerService projectExplorer, IOutputService? outputService)
        {
            try
            {
                dynamic fpgaRoot = root;

                string selectedArch = NoneOption;

                if (fpgaRoot.Properties != null &&
                    fpgaRoot.Properties.ContainsKey(ProcessorSettingKey))
                {
                    var value = fpgaRoot.Properties[ProcessorSettingKey];
                    if (value != null)
                        selectedArch = value.ToString();
                }

                string fullPath = Path.Combine(root.FullPath, ProcessorFileName);

                if (string.Equals(selectedArch, NoneOption, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);

                    fpgaRoot.TopEntity = null;

                    await projectExplorer.SaveProjectAsync(root);
                    await projectExplorer.ReloadAsync(root);

                    outputService?.WriteLine("[Processor] None selected – processor removed.", textColor: Brushes.Red);
                    outputService?.WriteLine("[Processor] Verilog Datei 'processor_top.v wurde gelöscht.", textColor: Brushes.Red);
                    return;
                }
                
                outputService?.WriteLine($"[Processor] Architektur ausgewählt: {selectedArch}", textColor: Brushes.Green);
                outputService?.WriteLine("[Processor] Verilog Datei 'processor_top.v' wurde erstellt.", textColor: Brushes.Green);
                
                string content = selectedArch.Contains("5-stage", StringComparison.OrdinalIgnoreCase)
                    ? ProcessorTemplates.FiveStageCode
                    : ProcessorTemplates.SingleCycleCode;

                await File.WriteAllTextAsync(fullPath, content);
                await projectExplorer.ReloadAsync(root);

                var projectFile = root.Files.FirstOrDefault(
                    f => f.Name.Equals(ProcessorFileName, StringComparison.OrdinalIgnoreCase));

                if (projectFile != null)
                {
                    fpgaRoot.TopEntity = projectFile;
                    await projectExplorer.SaveProjectAsync(root);
                }
            }
            catch (Exception ex)
            {
                outputService?.WriteLine($"[Processor ERROR] {ex}", textColor: Brushes.Red);
            }
        }
}