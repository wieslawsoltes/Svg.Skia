using System.Collections.Generic;
using Fody;
using Mono.Cecil;

namespace Weavers;

public class PropertyRename : BaseModuleWeaver
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
        foreach (var property in type.Properties)
        {
            if (!property.Name.StartsWith("_") && property.Name.Contains('_'))
            {
                WriteInfo($"Rename property: {property.Name} in {type}");
                RenameProperty(type, property, property.Name.Replace('_', '-'));
            }
        }

        foreach (var nestedType in type.NestedTypes)
        {
            ProcessType(nestedType);
        }
    }

    private void RenameProperty(TypeDefinition type, PropertyDefinition property, string newName)
    {
        UpdatePropertyReferences(type, property, newName);

        property.Name = newName;

        var getter = property.GetMethod;

        if (getter != null)
        {
            getter.Name = $"get_{newName}";
        }

        var setter = property.SetMethod;
        if (setter != null)
        {
            setter.Name = $"set_{newName}";
        }
    }

    private void UpdatePropertyReferences(TypeDefinition type, PropertyDefinition property, string newName)
    {
        foreach (var method in type.Methods)
        {
            if (!method.HasBody)
            {
                continue;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.Operand is MethodReference methodRef)
                {
                    if (methodRef.Name == $"get_{property.Name}")
                    {
                        methodRef.Name = $"get_{newName}";
                    }
                    else if (methodRef.Name == $"set_{property.Name}")
                    {
                        methodRef.Name = $"set_{newName}";
                    }
                }
                else if (instruction.Operand is FieldReference fieldRef && fieldRef.Name == property.Name)
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
                    if (instruction.Operand is MethodReference methodRef)
                    {
                        if (methodRef.DeclaringType == type && methodRef.Name == $"get_{property.Name}")
                        {
                            methodRef.Name = $"get_{newName}";
                        }
                        else if (methodRef.DeclaringType == type && methodRef.Name == $"set_{property.Name}")
                        {
                            methodRef.Name = $"set_{newName}";
                        }
                    }
                    else if (instruction.Operand is FieldReference fieldRef && fieldRef.DeclaringType == type && fieldRef.Name == property.Name)
                    {
                        fieldRef.Name = newName;
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
