namespace CodeGenerator;

internal record TypeDef(
    string TargetTpe,
    bool IsAbstract,
    string BaseType,
    string FilePath,
    PropertyDef[] Properties);

