namespace mccsx;

public enum AminoAcid
{
    // Charged (side chains often make salt bridges)
    [AminoAcidNames("Arg", 'R')]
    Arginine,
    [AminoAcidNames("Lys", 'K')]
    Lysine,
    [AminoAcidNames("Asp", 'D')]
    AsparticAcid,
    [AminoAcidNames("Glu", 'E')]
    GlutamicAcid,

    // Polar (usually participate in hydrogen bonds as proton donors or acceptors)
    [AminoAcidNames("Gln", 'Q')]
    Glutamine,
    [AminoAcidNames("Asn", 'N')]
    Asparagine,
    [AminoAcidNames("His", 'H')]
    Histidine,
    [AminoAcidNames("Ser", 'S')]
    Serine,
    [AminoAcidNames("Thr", 'T')]
    Threonine,
    [AminoAcidNames("Tyr", 'Y')]
    Tyrosine,
    [AminoAcidNames("Cys", 'C')]
    Cysteine,
    [AminoAcidNames("Trp", 'W')]
    Tryptophan,

    // Hydrophobic (normally buried inside the protein core)
    [AminoAcidNames("Ala", 'A')]
    Alanine,
    [AminoAcidNames("Ile", 'I')]
    Isoleucine,
    [AminoAcidNames("Leu", 'L')]
    Leucine,
    [AminoAcidNames("Met", 'M')]
    Methionine,
    [AminoAcidNames("Phe", 'F')]
    Phenylalanine,
    [AminoAcidNames("Val", 'V')]
    Valine,
    [AminoAcidNames("Pro", 'P')]
    Proline,
    [AminoAcidNames("Gly", 'G')]
    Glycine,
}
