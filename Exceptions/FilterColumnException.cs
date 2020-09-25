using System;
using System.Runtime.Serialization;

namespace mccsx
{
    [Serializable]
    public class FilterColumnException : Exception
    {
        public FilterColumnException() { }
        public FilterColumnException(string message) : base(message) { }
        public FilterColumnException(string message, Exception inner) : base(message, inner) { }
        public FilterColumnException(string message, string filterName) : base(message) => FilterName = filterName;
        public FilterColumnException(string message, string filterName, Exception inner) : base(message, inner) => FilterName = filterName;
        protected FilterColumnException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }

        public string? FilterName { get; }
    }
}
