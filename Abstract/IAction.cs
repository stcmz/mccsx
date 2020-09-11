namespace mccsx
{
    internal interface IAction<TOptions>
        where TOptions : IOptions
    {
        int Run(TOptions options);
    }
}
