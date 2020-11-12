mccsx
=====

mccsx is a cross-platform analysis tool for residue energy contribution vectors.


Features
--------

mccsx has two commands:
* `search`: to perform similarity search for a pattern vector in a RECV (residue energy contribution vector) library and generate search reports
* `collate`: to collect input vectors from a RECV (residue energy contribution vector) library and optionally generate similarity matrices, heatmaps and Excel workbooks

mccsx supports two kinds of similarity measure:
* cosine: [Cosine similarity](https://en.wikipedia.org/wiki/Cosine_similarity)
* correlation: [Pearson correlation coefficient (PCC)](https://en.wikipedia.org/wiki/Pearson_correlation_coefficient)

two kinds of distance measure:
* cosine: [Cosine distance](https://en.wikipedia.org/wiki/Cosine_similarity)
* correlation: [Pearson's distance](https://en.wikipedia.org/wiki/Pearson_correlation_coefficient#Pearson's_distance)

and three cluster linkage algorithms:
* farthest: [Complete-linkage clustering](https://en.wikipedia.org/wiki/Complete-linkage_clustering)
* nearest: [Single-linkage clustering](https://en.wikipedia.org/wiki/Single-linkage_clustering)
* average: [Unweighted pair group method with arithmetic mean (UPGMA)](https://en.wikipedia.org/wiki/UPGMA)

mccsx collate generates:
* input vectors (.csv) collected from the vector library
* similarity matrices (.csv) for input vectors with supported similarity measure
* clustering log (.csv) for input vectors and/or similarity matrices with supported distance measure and linkage algorithm
* heatmap images (.png) for input vectors and/or similarity matrices
* Excel workbooks (.xlsx) for input vectors and/or similarity matrices visualized with conditional formatting

mccsx additionally allows:
* a residue sequence filter: to align vectors originated from different protein (thus, different amino acid sequence)
* multi-threading: to boost processing performance

Supported operating systems and compilers
-----------------------------------------

All systems with [.NET SDK v5.0] or higher supported, e.g.
* Windows 10 version 1809 or higher
* macOS 10.13 "High Sierra" or higher
* most of current Linux distros: Ubuntu, CentOS, openSUSE, RHEL, Fedora, Debian, Alpine, SLES

Compilation from source code
----------------------------

### Compiler and SDK

mccsx compiles with [.NET SDK v5.0]. Follow the official guide to download and install the SDK before the build. The SDK also comes with the Visual Studio 2019 installer version 16.8 or higher.

The Visual Studio solution and project files are provided. One may open `mccsx.sln` in Visual Studio 2019 and do a rebuild (predefined profiles for Windows/Linux/macOS are also provided).

The generated objects are placed in the `obj` folder, and the generated executable are placed in the `bin` folder.

Thanks to the complete cross-platform nature of the SDK, one can target any supported system on any other supported system. For example, one can build and publish a macOS or Linux version of `mccsx` on Windows or vice versa. Build for "Portable" on "Any CPU" is also possible. However, to enable single-file publish and ready-to-run compilation, a platform-specific build is suggested.


### Build for Linux

To compile for Linux on any supported system, simply run
```
dotnet publish -c release --no-self-contained -r linux-x64 -p:Platform="Any CPU" -p:PublishSingleFile=true
```

Or with MSBuild, simply run
```
msbuild /t:Restore;Clean;Build;Publish /p:Configuration=Release /p:Platform="Any CPU" /p:PublishProfile=LinuxFolderProfile
```

### Build for Windows

To compile for Windows on any supported system, simply run
```
dotnet publish -c release --no-self-contained -r win-x64 -p:Platform="Any CPU" -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

Or with MSBuild, simply run
```
msbuild /t:Restore;Clean;Build;Publish /p:Configuration=Release /p:Platform="Any CPU" /p:PublishProfile=WinFolderProfile
```

### Build for macOS

To compile for macOS on any supported system, simply run
```
dotnet publish -c release --no-self-contained -r osx-x64 -p:Platform="Any CPU" -p:PublishSingleFile=true
```

Or with MSBuild, simply run
```
msbuild /t:Restore;Clean;Build;Publish /p:Configuration=Release /p:Platform="Any CPU" /p:PublishProfile=MacFolderProfile
```

Usage
-----

First add mccsx to the PATH environment variable or place mccsx in a PATH location (e.g. C:\Windows\System32\ or /usr/bin).

To display a full list of available options, simply run the program with the `--help` argument
```
mccsx --help
mccsx search --help
mccsx collate --help
```

### mccsx search

To similarity search for a pattern in a library of jdock resulting vectors, run
```
mccsx search -l docking_output -p scoring_output -m cosine -o bestmatch
```

### mccsx collate

To analyze the relationship among the vectors acquired for a ligand library docked against a receptor structure, run
```
mccsx collate -l docking_output -m cosine -o report -vx
```

To carry out agonist/antagonist analysis using the vectors acquired from a set of receptor-ligand complexes, run
```
mccsx collate -l scoring_output -o report -c -M correlation -L farthest -v
```

To carry out cluster analysis of the receptor structures of different proteins (i.e. different residue sequences) scored against corresponding cocrystal binding ligands, and the residue sequences can be aligned with a certain numbering scheme (e.g. the Ballesteros-Weinstein numbering scheme for GPCR sequences), run
```
mccsx collate -l scoring_output -o report -c -M correlation -L farthest -v -f "gpcrn {}: -1234"
```

Author
--------------

[Maozi Chen]


[Maozi Chen]: https://www.linkedin.com/in/maozichen/
[.NET SDK v5.0]: https://dotnet.microsoft.com/download/dotnet/5.0