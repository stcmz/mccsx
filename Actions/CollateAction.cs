using System;

namespace mccsx
{
    internal class CollateAction : IAction<CollateOptions>
    {
        public int Setup(CollateOptions options)
        {
            Console.WriteLine("setupt collate not implemented");
            return 2;
        }

        public int Run()
        {
            Console.WriteLine("run collate not implemented");
            return 2;
        }
    }
}
