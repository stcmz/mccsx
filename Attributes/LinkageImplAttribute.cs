using System;

namespace mccsx
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class LinkageImplAttribute : Attribute
    {
        public LinkageImplAttribute(Type linkageClass)
            => LinkageClass = linkageClass;

        public Type LinkageClass { get; }
    }
}
