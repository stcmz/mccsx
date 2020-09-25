using System;

namespace mccsx
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class AminoAcidNamesAttribute : Attribute
    {
        public AminoAcidNamesAttribute(string shortName, char code)
            => (ShortName, Code) = (shortName, code);

        public string ShortName { get; }

        public char Code { get; }
    }
}
