using SvgML.SelfWeaver;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: SvgML.SelfWeaver <assembly-path>");
    return 1;
}

try
{
    var result = new SvgMLAssemblyRewriter().Rewrite(args[0]);

    if (result.HasChanges)
    {
        Console.WriteLine(
            "Rewrote '{0}' ({1} type(s), {2} propertie(s), {3} field(s)).",
            Path.GetFileName(args[0]),
            result.RenamedTypes,
            result.RenamedProperties,
            result.RenamedFields);
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
