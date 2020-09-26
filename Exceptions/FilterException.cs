using System;
using System.Runtime.Serialization;

namespace mccsx
{
    [Serializable]
    public class FilterException : Exception
    {
        public FilterException() { }
        public FilterException(string message) : base(message) { }
        public FilterException(string message, Exception inner) : base(message, inner) { }
        public FilterException(string message, string filterName) : base(message) => FilterName = filterName;
        public FilterException(string message, string filterName, Exception inner) : base(message, inner) => FilterName = filterName;
        protected FilterException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }

        public string? FilterName { get; }
    }
}
