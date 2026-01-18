using Avalonia.Media;
using Moq;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Xunit;
using System.Linq; // Wichtig für SelectMany

namespace RipesConnect.UnitTests;

// 1. Hilfs-Interface
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
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        try
        {
            var outputMock = new Mock<IOutputService>();
            var explorerMock = new Mock<IProjectExplorerService>();
            
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
        
        Assert.NotNull(ripes.Versions);
        Assert.NotEmpty(ripes.Versions); 
        
        // Suche in allen Versionen nach Windows (nur als Beispiel)
        var winTarget = ripes.Versions.SelectMany(v => v.Targets).FirstOrDefault(t => t.Target == "win-x64");
        Assert.NotNull(winTarget);
        Assert.Contains(".zip", winTarget.Url ?? "");

        // 2. GCC prüfen
        var gcc = RipesConnect.GccPackage;
        Assert.Equal("riscv-gcc", gcc.Id);
        
        Assert.NotNull(gcc.Versions);
        Assert.NotEmpty(gcc.Versions);
        
        // -----------------------------------------------------------------------
        // FIX: Wir suchen jetzt in ALLEN Versionen nach Linux, 
        // da Linux jetzt in Version 14 (Versions[1]) ist und nicht mehr in Versions[0].
        // -----------------------------------------------------------------------
        var allTargets = gcc.Versions.SelectMany(v => v.Targets);
        var linuxTarget = allTargets.FirstOrDefault(t => t.Target == "linux-x64");
        
        // Das hier schlug vorher fehl, weil er es in Version[0] nicht fand
        Assert.NotNull(linuxTarget); 
        
        Assert.NotNull(linuxTarget.AutoSetting);
        Assert.NotEmpty(linuxTarget.AutoSetting);
        Assert.Contains("riscv-none-elf-gcc", linuxTarget.AutoSetting[0].RelativePath ?? "");
    }
}
