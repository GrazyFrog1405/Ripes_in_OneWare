using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace RipesConnect
{
    public class RipesConnect : IModule
    {
        private const string ProcessorSettingKey = ProcessorGeneration.ProcessorSettingKey;
        private const string NoneOption = ProcessorGeneration.NoneOption;
        public const string RipesPathSetting = "RipesModule_RipesPath";
        public const string GccPathSetting = "RipesModule_GccPath";
        
        public static readonly Package RipesPackage = new()
        {
            Category = "Binaries",
            Id = "ripes",
            Type = "NativeTool",
            Name = "Ripes",
            Description = "A visual processor simulator and assembly editor for the RISC-V ISA",
            License = "MIT",
            IconUrl = "https://raw.githubusercontent.com/mortbopet/Ripes/master/resources/icons/logo.svg",
            Links = [ new PackageLink() { Name = "GitHub", Url = "https://github.com/mortbopet/Ripes" } ],
            Versions =
            [
                new PackageVersion()
                {
                    Version = "2.2.6",
                    Targets =
                    [
                        // Windows Target
                        new PackageTarget()
                        {
                            Target = "win-x64",
                            Url = "https://github.com/mortbopet/Ripes/releases/download/v2.2.6/Ripes-v2.2.6-win-x86_64.zip",
                            AutoSetting = [ 
                                new PackageAutoSetting()
                                {
                                    RelativePath = "Ripes.exe", 
                                    SettingKey = RipesPathSetting
                                } ]
                        },
                        // Linux
                        new PackageTarget()
                        {
                            Target = "linux-x64",
                            Url = "https://github.com/GrazyFrog1405/Ripes_in_OneWare/releases/download/2.2.6/Ripes-v.2.2.6-linux-x86_64.zip",
                            AutoSetting = [ 
                                new PackageAutoSetting()
                                {
                                    RelativePath = "Ripes-v2.2.6-linux-x86_64.AppImage", 
                                    SettingKey = RipesPathSetting
                                } ]
                        },
                        
                        // Mac Target
                        new PackageTarget()
                        {
                            Target = "osx-x64",
                            Url = "https://github.com/mortbopet/Ripes/releases/download/v2.2.6/Ripes-v2.2.6-mac-x86_64.zip",
                            AutoSetting = [ 
                                new PackageAutoSetting()
                                {
                                    RelativePath = "Ripes.app/Contents/MacOS/Ripes", 
                                    SettingKey = RipesPathSetting
                                } ]
                        }
                    ]
                }
            ]
        };
        
        public static readonly Package GccPackage = new()
{
    Category = "Compiler", 
    Id = "riscv-gcc",
    Type = "NativeTool",
    Name = "RISC-V GCC (xPack)",
    Description = "The xPack GNU RISC-V Embedded GCC toolchain",
    License = "MIT",
    //IconUrl = "",
    Links = [ new PackageLink() { Name = "GitHub", Url = "https://github.com/xpack-dev-tools/riscv-none-elf-gcc-xpack" } ],
    Versions =
    [
        new PackageVersion()
        {
            Version = "15.2.0.1", 
            Targets =
            [
                // Windows
                new PackageTarget()
                {
                    Target = "win-x64",
                    Url = "https://github.com/xpack-dev-tools/riscv-none-elf-gcc-xpack/releases/download/v15.2.0-1/xpack-riscv-none-elf-gcc-15.2.0-1-win32-x64.zip",
                    AutoSetting = 
                    [ 
                        new PackageAutoSetting() 
                        { 
                            RelativePath = "xpack-riscv-none-elf-gcc-15.2.0-1/bin/riscv-none-elf-gcc.exe", 
                            SettingKey = GccPathSetting 
                        } 
                    ]
                },
                // macOS
                new PackageTarget()
                {
                    Target = "osx-x64",
                    Url = "https://github.com/xpack-dev-tools/riscv-none-elf-gcc-xpack/releases/download/v15.2.0-1/xpack-riscv-none-elf-gcc-15.2.0-1-darwin-x64.tar.gz",
                    AutoSetting = 
                    [ 
                        new PackageAutoSetting() 
                        { 
                            RelativePath = "xpack-riscv-none-elf-gcc-15.2.0-1/bin/riscv-none-elf-gcc", 
                            SettingKey = GccPathSetting 
                        } 
                    ]
                }
            ]
        },
    ]
};

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
        }

        public void OnInitialized(IContainerProvider containerProvider)
{
    var windowService = containerProvider.Resolve<IWindowService>();
    var projectExplorer = containerProvider.Resolve<IProjectExplorerService>();
    var settingsService = containerProvider.Resolve<IProjectSettingsService>();
    var outputService = containerProvider.Resolve<IOutputService>();
    var paths = containerProvider.Resolve<IPaths>();
    var globalSettingsService = containerProvider.Resolve<ISettingsService>();
    var packageService = containerProvider.Resolve<IPackageService>();
    
    // ============================================================
    // 0. PACKAGES & GLOBAL SETTINGS
    // ============================================================
    
    packageService.RegisterPackage(RipesPackage);
    packageService.RegisterPackage(GccPackage);
    
    globalSettingsService.RegisterSetting(
        "Simulator",       
        "Ripes",            
        RipesPathSetting,   
        new FilePathSetting( 
            title: "Ripes Path",
            defaultValue: "",
            watermark: "Path to Ripes executable (e.g. Ripes.AppImage or Ripes.exe)",
            startDirectory: paths.NativeToolsDirectory,
            checkPath: File.Exists
        )
    );
    
    globalSettingsService.RegisterSetting(
        "Tools",       
        "RISC-V GCC",    
        GccPathSetting,    
        new FilePathSetting(
            title: "GCC Path",
            defaultValue: "",
            watermark: "Path to riscv-none-elf-gcc executable",
            startDirectory: paths.NativeToolsDirectory,
            checkPath: File.Exists
        )
    );
    
    // ============================================================
    // 1. PROJECT SETTINGS
    // ============================================================
    string[] rvlOptions =
    {
        NoneOption,
        "32-bit Single Cycle RISC-V Processor",
        "32-bit 5-stage RISC-V Processor w/o Forwarding or Hazard Detection"
    };

    var comboBox = new ComboBoxSetting("Ripes Processor Architecture", NoneOption, rvlOptions);

    settingsService.AddProjectSetting(new ProjectSetting(ProcessorSettingKey, comboBox, _ => true));

    // ============================================================
    // 2. MAIN MENU
    // ============================================================
    
    windowService.RegisterMenuItem(
        "MainWindow_MainMenu/Ripes",
        new MenuItemViewModel("Ripes.Open")
        {
            Header = "Open Ripes",
            Command = new RelayCommand(() => 
            {
                var currentPath = globalSettingsService.GetSettingValue<string>(RipesPathSetting);
                    Ripes.RunRipes(currentPath, null, outputService);
            })
        });

    // ============================================================
    // 3. CONTEXT MENU
    // ============================================================
    projectExplorer.RegisterConstructContextMenu((selected, menuItems) =>
    {
        // ASM files
        if (selected is [IProjectFile { Extension: var ext } asmFile] &&
            (ext.Equals(".s", StringComparison.OrdinalIgnoreCase) ||
             ext.Equals(".asm", StringComparison.OrdinalIgnoreCase)))
        {
            menuItems.Add(new MenuItemViewModel("Ripes.Simulate")
            {
                Header = "Simulate in Ripes",
                Command = new RelayCommand(() => 
                {
                    var currentPath = globalSettingsService.GetSettingValue<string>(RipesPathSetting);
                         Ripes.RunRipes(currentPath, asmFile.FullPath, outputService);
                })
            });

            menuItems.Add(new MenuItemViewModel("Ripes.BuildMem")
            {
                Header = "Create *.mem file",
                Command = new RelayCommand( () => {
                    var gccPath = globalSettingsService.GetSettingValue<string>(GccPathSetting);
                    BuildHexCode.BuildHex(gccPath, asmFile.FullPath, outputService);
                })
            });
        }
        
        var root = ResolveProjectRoot(selected.FirstOrDefault());
        if (root == null)
            return;

        menuItems.Add(new MenuItemViewModel("Ripes.ApplyProcessor")
        {
            Header = "Apply Ripes Processor Configuration",
            Command = new AsyncRelayCommand(
                () => ProcessorGeneration.GenerateProcessorAsync(root, projectExplorer, outputService))
        });
    });
}
        // ============================================================
        // HELPERS
        // ============================================================
        private static IProjectRoot? ResolveProjectRoot(object? item)
        {
            if (item is IProjectRoot root)
                return root;

            try
            {
                var prop = item?.GetType().GetProperty("Root");
                return prop?.GetValue(item) as IProjectRoot;
            }
            catch
            {
                return null;
            }
        }
    }
}
