# RV_Fabrication

`RV_Fabrication` is a RISC-V 32I assembly pre-processor that provides some handy tools to speed up development. It is meant to complement the assembler (specifically the one present in [RIPES](https://github.com/mortbopet/Ripes)) and prevent mistakes when saving/restoring registers for function calls.

## Motivation

This tool was made to help me with a project for university (Introduction to Computer Architecture) and make development easier and faster. Naturally, this took me **A LOT** more than the actual project would have taken me to do by hand, especially considering this is a rewrite of the original code, but it was a lot of fun to make.

## Documentation

For directive documentation, check the [directives](doc/directives.md).  
For command line arguments, check the [usage](#usage) section below.
For a quick introduction to the functioning of the code, check what [passes](doc/passes.md) the code performs.

## Dependencies

To run the tool, you will need the `.NET 8.0 Runtime`.  
To compile the tool, you will need the `.NET 8.0 SDK`.
Both can be downloaded from the [Official .NET 8.0 downloads page]((https://dotnet.microsoft.com/en-us/download/dotnet/8.0)).
In the root directory of the repository, there is a makefile to deploy the tool on Linux. To run it, you will also need [GNU Make](https://www.gnu.org/software/make/).

## Compilation and installation

To compile, make sure you have the needed [dependencies](#dependencies) first.

1) Clone the repository by navigating to a directory of your choosing and running `git clone https://github.com/didas72/RV_Fabrication.git`.
2) Enter the cloned directory with `cd RV_Fabrication`.
3) Compile with `dotnet build` **OR** automatically build and deploy on linux by running `make deploy`. (requires sudo)

## Usage

RV_Fabrication comes with balanced default modes, to make it's use as simple as possible. If you just want to get started, try  
`RV_Fabrication <source_file>`

If you don't like the default output file, you MAY specify a different one by using  
`RV_Fabrication <source_file> <out_file>`

Full documentation on command line arguments can be accessed by running  
`RV_Fabrication -h`

## Known issues

None for now :)  
Please report any issues you find.

## Planned features

- Implement function reference counting with automatic removal of unused functions
- Add RV_Fabrication comment stripping option. (eg: Comments generated when implementing macros are not stripped unless in `source comments` stripping mode)
