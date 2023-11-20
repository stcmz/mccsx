mccsx
=====
![build workflow](https://github.com/stcmz/mccsx/actions/workflows/build.yml/badge.svg)
![release workflow](https://github.com/stcmz/mccsx/actions/workflows/release.yml/badge.svg)

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

All systems with [.NET SDK v8.0] or higher supported, e.g.
* Windows 10 version 1607 or higher
* macOS 10.15 "Catalina" or higher
* most of current Linux distros: Ubuntu, CentOS, openSUSE, RHEL, Fedora, Debian, Alpine, SLES

See [official document](https://github.com/dotnet/core/blob/main/release-notes/8.0/supported-os.md) for more details.

Compilation from source code
----------------------------

### Compiler and SDK

mccsx compiles with [.NET SDK v8.0]. Follow the official guide to download and install the SDK before the build. The SDK also comes with the Visual Studio 2022 installer version 17.8 or higher.

The Visual Studio solution and project files, as well as the vscode settings are provided. One may open `mccsx.sln` in Visual Studio 2022 and do a rebuild (predefined profiles for Windows/Linux/macOS are also provided), or open the cloned repository in vscode and run the build task.

The generated objects are placed in the `obj` folder, and the generated executable are placed in the `bin` folder.

Thanks to the complete cross-platform nature of the SDK, one can target any supported system on any other supported system. For example, one can build and publish a macOS or Linux version of `mccsx` on Windows or vice versa. Build for "Portable" on "Any CPU" is also possible. However, to enable single-file publish and ready-to-run compilation, a platform-specific build is suggested.


### Build for Linux

To compile for Linux on any supported system, simply run either of
```Powershell
# Dynamic build, will require installation of .NET Runtime/SDK
dotnet publish -c release --no-self-contained -r linux-x64 -p:UseAppHost=true

# Static build, no .NET Runtime/SDK is required
dotnet publish -c release --self-contained -r linux-x64 -p:UseAppHost=true -p:PublishTrimmed=true
```

Or with MSBuild, simply run either of
```Powershell
# Dynamic build, will require installation of .NET Runtime/SDK
msbuild /t:"Restore;Clean;Build;Publish" /p:Configuration=Release /p:PublishProfile=LinuxFolderProfile

# Static build, no .NET Runtime/SDK is required
msbuild /t:"Restore;Clean;Build;Publish" /p:Configuration=Release /p:PublishProfile=LinuxFolderProfile_static
```

### Build for Windows

To compile for Windows on any supported system, simply run either of
```Powershell
# Dynamic build, will require installation of .NET Runtime/SDK
dotnet publish -c release --no-self-contained -r win-x64 -p:UseAppHost=true -p:PublishReadyToRun=true

# Static build, no .NET Runtime/SDK is required
dotnet publish -c release --self-contained -r win-x64 -p:UseAppHost=true -p:PublishTrimmed=true -p:PublishReadyToRun=true
```

Or with MSBuild, simply run either of
```Powershell
# Dynamic build, will require installation of .NET Runtime/SDK
msbuild /t:"Restore;Clean;Build;Publish" /p:Configuration=Release /p:PublishProfile=WinFolderProfile

# Static build, no .NET Runtime/SDK is required
msbuild /t:"Restore;Clean;Build;Publish" /p:Configuration=Release /p:PublishProfile=WinFolderProfile_static
```

Kindly note that, the `PublishReadyToRun` option is only available while building on Windows.

### Build for macOS

To compile for macOS on any supported system, simply run either of
```PowerShell
# Dynamic build, will require installation of .NET Runtime/SDK
dotnet publish -c release --no-self-contained -r osx-x64 -p:UseAppHost=true

# Static build, no .NET Runtime/SDK is required
dotnet publish -c release --self-contained -r osx-x64 -p:UseAppHost=true -p:PublishTrimmed=true
```

Or with MSBuild, simply run either of
```PowerShell
# Dynamic build, will require installation of .NET Runtime/SDK
msbuild /t:"Restore;Clean;Build;Publish" /p:Configuration=Release /p:PublishProfile=MacFolderProfile

# Static build, no .NET Runtime/SDK is required
msbuild /t:"Restore;Clean;Build;Publish" /p:Configuration=Release /p:PublishProfile=MacFolderProfile_static
```

Usage
-----

First add mccsx to the PATH environment variable or place mccsx in a PATH location (e.g. C:\Windows\System32\ or /usr/bin).

To display a full list of available options, simply run the program with the `--help` argument
```bash
mccsx --help
mccsx search --help
mccsx collate --help
```

### mccsx search

To similarity search for a pattern in a library of jdock resulting vectors, run
```bash
mccsx search -l docking_output -p scoring_output -m cosine -o bestmatch
```

### mccsx collate

To analyze the relationship among the vectors acquired for a ligand library docked against a receptor structure, run
```bash
mccsx collate -l docking_output -m cosine -o report -vx
```

To carry out agonist/antagonist analysis using the vectors acquired from a set of receptor-ligand complexes, run
```bash
mccsx collate -l scoring_output -o report -c -M correlation -L farthest -v
```

To carry out cluster analysis of the receptor structures of different proteins (i.e. different residue sequences) scored against corresponding cocrystal binding ligands, and the residue sequences can be aligned with a certain numbering scheme (e.g. the Ballesteros-Weinstein numbering scheme for GPCR sequences), run
```bash
mccsx collate -l scoring_output -o report -c -M correlation -L farthest -v -f "gpcrn {}: -1234"
```

Citations
---------

### The Original MCCS Paper

**Maozi Chen**, Zhiwei Feng, Siyi Wang, Weiwei Lin, Xiang-Qun Xie. **MCCS, a scoring function-based characterization method for protein-ligand binding**. *Briefings in Bioinformatics*. 2020, October 14.

https://doi.org/10.1093/bib/bbaa239

https://pubmed.ncbi.nlm.nih.gov/33051641/


### MCCS in COVID-19 Drug Repurposing

Zhiwei Feng, **Maozi Chen**, Ying Xue, Tianjian Liang, Hui Chen, Yuehan Zhou, Thomas Nolin, and Xiang-Qun Xie. **MCCS: A Novel Recognition Pattern-based Method for Fast Track Discovery of anti-SARS-CoV-2 drugs**. *Briefings in Bioinformatics*. Volume 22, Issue 2, March 2021, Pages 946–962.

https://doi.org/10.1093/bib/bbaa260

https://pubmed.ncbi.nlm.nih.gov/33078827/


### MCCS on GPCR Modulators

Feng, Zhiwei; Liang, Tianjian; Wang, Siyi; **Chen, Maozi**; Hou, Tianling; Zhao, Jack; Chen, Hui; Zhou, Yuehan; Xie, Xiang-Qun. **Binding Characterization of GPCRs-Modulator by Molecular Complex Characterizing System (MCCS)**. *ACS Chemical Neuroscience*. 2020, 11, 20, 3333–3345.

https://doi.org/10.1021/acschemneuro.0c00457

https://pubmed.ncbi.nlm.nih.gov/32941011/


### MCCS in AA<sub>2A</sub>R Case Study

Jin Cheng, **Maozi Chen**, Siyi Wang, Tianjian Liang, Hui Chen, Chih-Jung Chen, Zhiwei Feng, Xiang-Qun Xie. **Binding Characterization of Agonists and Antagonists by MCCS: A Case Study from Adenosine A<sub>2A</sub> Receptor**. *ACS Chemical Neuroscience*. 2021, 12, 9, 1606–1620.

https://doi.org/10.1021/acschemneuro.1c00082

https://pubmed.ncbi.nlm.nih.gov/33856784/


Author
--------------

[Maozi Chen]


[Maozi Chen]: https://www.linkedin.com/in/maozichen/
[.NET SDK v8.0]: https://dotnet.microsoft.com/download/dotnet/8.0
