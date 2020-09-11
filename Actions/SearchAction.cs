using System;

namespace mccsx
{
    internal class SearchAction : IAction<SearchOptions>
    {
        public int Run(SearchOptions options)
        {
            Console.WriteLine("search not implemented");
            return 1;
        }
    }
}
