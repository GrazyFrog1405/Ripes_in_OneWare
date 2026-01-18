![Icon](https://raw.githubusercontent.com/GrazyFrog1405/Ripes_in_OneWare/main/Icon.png)

# Get Started with Ripes Connect

# Requirements
[.NET 9.0 SDK.](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

# Installation for Windows & MacOS Users
1. Install this Extension in OneWare
2. Download and Install Ripes for Windows & MacOS via Extras -> Extensions -> Binaries -> Ripes
3. Download and Install RISC-V GCC for Windows & MacOS via Extras -> Extensions -> Compilers -> RISC-V GCC (xPack)

# Installation for Linux Users
1. Install this Extension in OneWare
2. Download and Install Ripes for Linux via Extras -> Extensions -> Binaries -> Ripes
3. Run chmod a+x on the AppImage file
4. Install libfuse2 for Ripes with the command sudo apt install libfuse2
5. Download and Install [RISC-V GCC](https://github.com/xpack-dev-tools/riscv-none-elf-gcc-xpack/releases) for Linux manually
6. Go to Extras -> Settings -> Compiler and set the correct installation path for RISC-V GCC  .../xpack-riscv-none-elf-gcc-15.2.0-1/bin/riscv-none-elf-gcc

# Usage
## 1. Launching Ripes and Simulation
You can launch the Ripes simulator directly or start a simulation from an assembly file.

Open Ripes Standalone: Navigate to the top toolbar, select Ripes, and click Open Ripes.

Simulate a File: Right-click on any assembler file (with the extension .s or .asm) in your file explorer. Click the Simulate in Ripes button in the context menu. This will launch Ripes and automatically load your code for simulation.

## 2. Processor Configuration & Generation

You can configure the processor architecture for your project and generate the corresponding Verilog top module.

Configure Architecture: Right-click on your project folder and select Project Settings. A menu will open with a dropdown labeled "Ripes Processor Architecture." Select your desired processor (or "none") and click Save & Close.

Apply Configuration: Right-click on your project folder again and select "Apply Processor Configuration". This will generate a Processor_top.v file containing the selected processor architecture.

## 3. Generate Memory Files

To convert your assembly code into a memory file:

Right-click on a .s or .asm file.
Select "Create *.mem file".
The extension will compile the assembler code and generate the corresponding memory file in your directory.

[![Test](https://github.com/GrazyFrog1405/Ripes_in_OneWare/actions/workflows/test.yml/badge.svg)](https://github.com/GrazyFrog1405/Ripes_in_OneWare/actions/workflows/test.yml)
[![Publish](https://github.com/GrazyFrog1405/Ripes_in_OneWare/actions/workflows/publish.yml/badge.svg)](https://github.com/GrazyFrog1405/Ripes_in_OneWare/actions/workflows/publish.yml)
