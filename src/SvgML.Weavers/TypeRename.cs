using Fody;
using Mono.Cecil;

namespace Weavers;

public class TypeRename : BaseModuleWeaver
{
    public override void Execute()
    {
        foreach (var type in ModuleDefinition.Types)
        {
            ProcessType(type);
        }
    }

    private void ProcessType(TypeDefinition type)
    {
        if (!type.Name.StartsWith("_") && type.Name.Contains('_'))
        {
            WriteInfo($"Rename type: {type.Name} in {type}");
            RenameType(type, type.Name.Replace('_', '-'));
        }

        foreach (var nestedType in type.NestedTypes)
        {
            ProcessType(nestedType);
        }
    }

    private void RenameType(TypeDefinition type, string newName)
    {
        UpdateFieldReferences(type, newName);

        type.Name = newName;
    }

    private void UpdateFieldReferences(TypeDefinition type, string newName)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasBody)
            {
                continue;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.Operand is TypeReference typeRef && typeRef.Name == type.Name)
                {
                    typeRef.Name = newName;
                }
            }
        }

        foreach (var otherType in ModuleDefinition.Types)
        {
            foreach (var method in otherType.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.Operand is TypeReference typeRef && typeRef.FullName == type.FullName)
                    {
                        typeRef.Name = newName;
                    }
                }
            }
        }
    }

    public override IEnumerable<string> GetAssembliesForScanning()
    {
        return [];
    }

    public override bool ShouldCleanReference => true;
}
