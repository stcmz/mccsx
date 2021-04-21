namespace mccsx
{
    // The name will be passed to the filters and renderered in the output
    // The path will be relative to the library full path
    // e.g. GPCR/CNR2/ligand.pdbqt
    // e.g. GPCR/CNR2/ligand_hbonding.csv
    internal enum NamingScheme
    {
        // Default value
        // e.g. GPCR/CNR2/ligand
        filepath,

        // e.g. ligand
        filestem,

        // e.g. GPCR/CNR2
        dirpath,

        // e.g. CNR2
        dirname,
    }
}
