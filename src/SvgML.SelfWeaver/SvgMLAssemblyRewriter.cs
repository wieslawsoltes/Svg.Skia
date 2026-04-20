using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SvgML.SelfWeaver;

public sealed class SvgMLAssemblyRewriter
{
    public SvgMLRewriteResult Rewrite(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
        }

        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullAssemblyPath))
        {
            throw new FileNotFoundException("Assembly was not found.", fullAssemblyPath);
        }

        var pdbPath = Path.ChangeExtension(fullAssemblyPath, ".pdb");
        var readSymbols = File.Exists(pdbPath);
        var readerParameters = new ReaderParameters
        {
            ReadSymbols = readSymbols,
            InMemory = true,
            SymbolReaderProvider = readSymbols ? new PortablePdbReaderProvider() : null
        };

        using var assembly = AssemblyDefinition.ReadAssembly(fullAssemblyPath, readerParameters);
        var result = RewriteModule(assembly.MainModule);

        if (!result.HasChanges)
        {
            return result;
        }

        var writerParameters = new WriterParameters
        {
            WriteSymbols = readSymbols,
            SymbolWriterProvider = readSymbols ? new PortablePdbWriterProvider() : null
        };

        var tempAssemblyPath = fullAssemblyPath + ".svgml.tmp";
        var tempPdbPath = Path.ChangeExtension(tempAssemblyPath, ".pdb");

        try
        {
            if (File.Exists(tempAssemblyPath))
            {
                File.Delete(tempAssemblyPath);
            }

            if (File.Exists(tempPdbPath))
            {
                File.Delete(tempPdbPath);
            }

            assembly.Write(tempAssemblyPath, writerParameters);
            File.Move(tempAssemblyPath, fullAssemblyPath, overwrite: true);

            if (readSymbols && File.Exists(tempPdbPath))
            {
                File.Move(tempPdbPath, pdbPath, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(tempAssemblyPath))
            {
                File.Delete(tempAssemblyPath);
            }

            if (File.Exists(tempPdbPath))
            {
                File.Delete(tempPdbPath);
            }
        }

        return result;
    }

    public SvgMLRewriteResult RewriteModule(ModuleDefinition module)
    {
        ArgumentNullException.ThrowIfNull(module);

        var result = new SvgMLRewriteResult();
        var allTypes = module.GetTypes().ToArray();

        foreach (var type in allTypes)
        {
            RewriteFields(allTypes, type, result);
            RewriteProperties(allTypes, type, result);
        }

        foreach (var type in allTypes)
        {
            RewriteType(allTypes, type, result);
        }

        return result;
    }

    private static void RewriteFields(IReadOnlyList<TypeDefinition> allTypes, TypeDefinition type, SvgMLRewriteResult result)
    {
        foreach (var field in type.Fields)
        {
            if (!ShouldRename(field.Name))
            {
                continue;
            }

            var originalName = field.Name;
            var renamed = ToSvgName(originalName);

            UpdateInstructions(
                allTypes,
                static (instruction, context) =>
                {
                    if (instruction.Operand is not FieldReference fieldReference)
                    {
                        return;
                    }

                    if (fieldReference.FullName == context.FieldFullName || fieldReference.Name == context.OriginalName)
                    {
                        fieldReference.Name = context.RenamedName;
                    }
                },
                new FieldRenameContext(originalName, renamed, field.FullName));

            field.Name = renamed;
            result.RenamedFields++;
        }
    }

    private static void RewriteProperties(IReadOnlyList<TypeDefinition> allTypes, TypeDefinition type, SvgMLRewriteResult result)
    {
        foreach (var property in type.Properties)
        {
            if (!ShouldRename(property.Name))
            {
                continue;
            }

            var originalName = property.Name;
            var renamed = ToSvgName(originalName);

            UpdateInstructions(
                allTypes,
                static (instruction, context) =>
                {
                    if (instruction.Operand is MethodReference methodReference)
                    {
                        if (methodReference.DeclaringType.FullName == context.DeclaringTypeFullName &&
                            methodReference.Name == $"get_{context.OriginalName}")
                        {
                            methodReference.Name = $"get_{context.RenamedName}";
                        }
                        else if (methodReference.DeclaringType.FullName == context.DeclaringTypeFullName &&
                                 methodReference.Name == $"set_{context.OriginalName}")
                        {
                            methodReference.Name = $"set_{context.RenamedName}";
                        }
                    }
                    else if (instruction.Operand is FieldReference fieldReference &&
                             fieldReference.DeclaringType.FullName == context.DeclaringTypeFullName &&
                             fieldReference.Name == context.OriginalName)
                    {
                        fieldReference.Name = context.RenamedName;
                    }
                },
                new PropertyRenameContext(originalName, renamed, type.FullName));

            property.Name = renamed;

            if (property.GetMethod is { } getter)
            {
                getter.Name = $"get_{renamed}";
            }

            if (property.SetMethod is { } setter)
            {
                setter.Name = $"set_{renamed}";
            }

            result.RenamedProperties++;
        }
    }

    private static void RewriteType(IReadOnlyList<TypeDefinition> allTypes, TypeDefinition type, SvgMLRewriteResult result)
    {
        if (!ShouldRename(type.Name))
        {
            return;
        }

        var originalName = type.Name;
        var originalFullName = type.FullName;
        var renamed = ToSvgName(originalName);

        UpdateInstructions(
            allTypes,
            static (instruction, context) =>
            {
                if (instruction.Operand is TypeReference typeReference &&
                    (typeReference.FullName == context.OriginalFullName || typeReference.Name == context.OriginalName))
                {
                    typeReference.Name = context.RenamedName;
                }
            },
            new TypeRenameContext(originalName, renamed, originalFullName));

        type.Name = renamed;
        result.RenamedTypes++;
    }

    private static void UpdateInstructions<TContext>(
        IReadOnlyList<TypeDefinition> allTypes,
        Action<Instruction, TContext> update,
        TContext context)
    {
        foreach (var candidateType in allTypes)
        {
            foreach (var method in candidateType.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                foreach (var instruction in method.Body.Instructions)
                {
                    update(instruction, context);
                }
            }
        }
    }

    private static bool ShouldRename(string name) => !name.StartsWith("_", StringComparison.Ordinal) && name.Contains('_');

    private static string ToSvgName(string name) => name.Replace('_', '-');

    private sealed record FieldRenameContext(string OriginalName, string RenamedName, string FieldFullName);

    private sealed record PropertyRenameContext(string OriginalName, string RenamedName, string DeclaringTypeFullName);

    private sealed record TypeRenameContext(string OriginalName, string RenamedName, string OriginalFullName);
}

public sealed class SvgMLRewriteResult
{
    public int RenamedFields { get; set; }

    public int RenamedProperties { get; set; }

    public int RenamedTypes { get; set; }

    public bool HasChanges => RenamedFields > 0 || RenamedProperties > 0 || RenamedTypes > 0;
}
