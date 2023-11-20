using System;

namespace mccsx;

[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
internal sealed class LinkageImplAttribute(Type linkageClass) : Attribute
{
    public Type LinkageClass { get; } = linkageClass;
}
