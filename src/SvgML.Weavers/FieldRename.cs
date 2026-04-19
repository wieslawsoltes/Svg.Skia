using Fody;
using Mono.Cecil;

namespace Weavers;

public class FieldRename : BaseModuleWeaver
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
        foreach (var field in type.Fields)
        {
            if (!field.Name.StartsWith("_") && field.Name.Contains('_'))
            {
                WriteInfo($"Rename field: {field.Name} in {type}");
                RenameField(type, field, field.Name.Replace('_', '-'));
            }
        }

        foreach (var nestedType in type.NestedTypes)
        {
            ProcessType(nestedType);
        }
    }

    private void RenameField(TypeDefinition type, FieldDefinition field, string newName)
    {
        UpdateFieldReferences(type, field, newName);

        field.Name = newName;
    }

    private void UpdateFieldReferences(TypeDefinition type, FieldDefinition field, string newName)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasBody)
            {
                continue;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.Operand is FieldReference fieldRef && fieldRef.Name == field.Name)
                {
                    fieldRef.Name = newName;
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
                    if (instruction.Operand is FieldReference fieldReference && fieldReference.FullName == field.FullName)
                    {
                        fieldReference.Name = newName;
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
