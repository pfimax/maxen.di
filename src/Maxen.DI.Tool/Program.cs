using Maxen.DI.Tool;

var arguments = ToolArguments.Parse(args);
if (arguments.Error is not null)
{
    Console.Error.WriteLine(arguments.Error);
    Console.Error.WriteLine();
    Console.Error.WriteLine(ToolArguments.Usage);
    return 2;
}

return arguments.Command switch
{
    "diff" => DiffCommand.Run(arguments, Console.Out, Console.Error),
    "explain" => ExplainCommand.Run(arguments, Console.Out, Console.Error),
    "snapshot" => SnapshotCommand.Run(arguments, Console.Out, Console.Error),
    "init" => InitCommand.Run(arguments, Console.Out, Console.Error),
    _ => GraphCommand.Run(arguments, Console.Out, Console.Error),
};
