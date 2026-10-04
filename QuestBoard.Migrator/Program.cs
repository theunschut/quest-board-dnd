namespace QuestBoard.Migrator;

internal static class Program
{
    private static int Main(string[] args) =>
        MigratorCli.Run(args, Console.Out, Console.Error, MigratorCli.CreateProductionContext);
}
