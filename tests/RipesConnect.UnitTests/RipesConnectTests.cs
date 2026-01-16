
using Avalonia.Media;
using Moq;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Xunit;

namespace RipesConnect.UnitTests;

// 1. Hilfs-Interface: Das behebt den "Cannot resolve symbol Properties" Fehler
public interface IMockRoot : IProjectRoot
{
    IDictionary<string, object> Properties { get; }
}

public class RipesConnectTests
{
    // ==========================================
    // GRUPPE 1: Templates & Generator
    // ==========================================

    [Fact]
    public void Templates_ShouldNotBeEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ProcessorTemplates.FiveStageCode));
        Assert.False(string.IsNullOrWhiteSpace(ProcessorTemplates.SingleCycleCode));
        Assert.Contains("module processor_top", ProcessorTemplates.FiveStageCode);
    }

    [Fact]
    public async Task GenerateProcessor_ShouldCreateFile_WhenArchSelectedAsync()
    {
        // Behebt "Cannot resolve symbol Guid/Task"
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        try
        {
            var outputMock = new Mock<IOutputService>();
            var explorerMock = new Mock<IProjectExplorerService>();
            
            // WICHTIG: Wir nutzen IMockRoot statt IProjectRoot für die Properties
            var rootMock = new Mock<IMockRoot>();

            rootMock.Setup(r => r.FullPath).Returns(tempPath);
            rootMock.Setup(r => r.Files).Returns(new List<IProjectFile>());
            
            var props = new Dictionary<string, object>
            {
                { ProcessorGeneration.ProcessorSettingKey, "32-bit 5-stage RISC-V Processor" }
            };
            rootMock.Setup(r => r.Properties).Returns(props);

            await ProcessorGeneration.GenerateProcessorAsync(rootMock.Object, explorerMock.Object, outputMock.Object);

            var expectedFilePath = Path.Combine(tempPath, "processor_top.v");
            Assert.True(File.Exists(expectedFilePath));
            
            // Behebt "Method WriteLine has 2 parameters but invoked with 3"
            outputMock.Verify(x => x.WriteLine(It.IsAny<string>(), Brushes.Green), Times.AtLeastOnce);
        }
        finally
        {
            if (Directory.Exists(tempPath)) Directory.Delete(tempPath, true);
        }
    }

    // ==========================================
    // GRUPPE 2: BuildHex (.mem Erstellung)
    // ==========================================

    [Fact]
    public void BuildHex_ShouldLogError_WhenGccMissing()
    {
        var outputMock = new Mock<IOutputService>();
        
        BuildHexCode.BuildHex(null, "dummy.s", outputMock.Object);

        outputMock.Verify(
            x => x.WriteLine(It.Is<string>(s => s.Contains("GCC Compiler nicht gefunden")), Brushes.Red), 
            Times.Once);
    }

    [Fact]
    public void BuildHex_ShouldLogError_WhenObjCopyMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            var fakeGccPath = Path.Combine(tempDir, "riscv-none-elf-gcc.exe"); 
            File.WriteAllText(fakeGccPath, "Dummy GCC");

            var outputMock = new Mock<IOutputService>();

            BuildHexCode.BuildHex(fakeGccPath, "code.s", outputMock.Object);

            outputMock.Verify(
                x => x.WriteLine(It.Is<string>(s => s.Contains("'objcopy' nicht gefunden")), Brushes.Red), 
                Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // ==========================================
    // GRUPPE 3: Ripes (Simulator)
    // ==========================================

    [Fact]
    public void RunRipes_ShouldLogError_WhenPathInvalid()
    {
        var outputMock = new Mock<IOutputService>();

        Ripes.RunRipes(@"C:\Gibts\Nicht\Ripes.exe", null, outputMock.Object);

        outputMock.Verify(
            x => x.WriteLine(It.Is<string>(s => s.Contains("[Ripes Error]")), Brushes.Red), 
            Times.Once);
    }

    // ==========================================
    // GRUPPE 4: Modul Konfiguration
    // ==========================================
    
    [Fact]
    public void PackageConfig_ShouldBeValid()
    {
        // 1. Ripes prüfen
        var ripes = RipesConnect.RipesPackage;
        Assert.Equal("ripes", ripes.Id);
        
        // Behebt gelbe Warnungen: Wir prüfen explizit auf null
        Assert.NotNull(ripes.Versions);
        // Da wir Assert. NotNull gemacht haben, weiß der Compiler jetzt, dass es existiert
        Assert.NotEmpty(ripes.Versions); 
        
        var firstVersion = ripes.Versions[0];
        Assert.NotNull(firstVersion.Targets);
        
        var winTarget = firstVersion.Targets.FirstOrDefault(t => t.Target == "win-x64");
        Assert.NotNull(winTarget);
        Assert.Contains(".zip", winTarget.Url ?? "");

        // 2. GCC prüfen
        var gcc = RipesConnect.GccPackage;
        Assert.Equal("riscv-gcc", gcc.Id);
        
        Assert.NotNull(gcc.Versions);
        Assert.NotEmpty(gcc.Versions);
        
        var gccVersion = gcc.Versions[0];
        Assert.NotNull(gccVersion.Targets);

        var linuxTarget = gccVersion.Targets.FirstOrDefault(t => t.Target == "linux-x64");
        Assert.NotNull(linuxTarget);
        
        Assert.NotNull(linuxTarget.AutoSetting);
        Assert.NotEmpty(linuxTarget.AutoSetting);
        Assert.Contains("riscv-none-elf-gcc", linuxTarget.AutoSetting[0].RelativePath ?? "");
    }
}