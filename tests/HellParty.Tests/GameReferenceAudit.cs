using Mono.Cecil;

// Validate emitted member signatures against the actual game/runtime, including
// optional arguments which still change the binary signature across game updates.
internal sealed class GameReferenceAudit : IDisposable
{
    private readonly string _managedDirectory;
    private readonly Dictionary<string, ModuleDefinition> _modules = new();

    internal GameReferenceAudit(string managedDirectory) => _managedDirectory = managedDirectory;

    private static string ScopeName(TypeReference type) => type.Scope.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        ? type.Scope.Name[..^4] : type.Scope.Name;
    internal static bool IsGameReference(TypeReference type) => ScopeName(type) is "Assembly-CSharp" or
        "PhotonUnityNetworking" or "PhotonRealtime" || ScopeName(type).StartsWith("UnityEngine", StringComparison.Ordinal);

    private TypeDefinition? Find(TypeReference type)
    {
        string scope = ScopeName(type);
        if (!_modules.TryGetValue(scope, out var module))
        {
            string path = Path.Combine(_managedDirectory, scope + ".dll");
            if (!File.Exists(path)) return null;
            module = ModuleDefinition.ReadModule(path);
            _modules.Add(scope, module);
        }
        return module.GetType(type.GetElementType().FullName);
    }

    private IEnumerable<TypeDefinition> Hierarchy(TypeReference type)
    {
        var seen = new HashSet<string>();
        TypeDefinition? current = Find(type);
        while (current != null && seen.Add(current.FullName))
        {
            yield return current;
            current = current.BaseType == null ? null : Find(current.BaseType);
        }
    }

    private static string Signature(TypeReference type) => type switch
    {
        GenericParameter p => (p.Type == GenericParameterType.Method ? "!!" : "!") + p.Position,
        ArrayType a => Signature(a.ElementType) + "[" + new string(',', a.Rank - 1) + "]",
        ByReferenceType r => Signature(r.ElementType) + "&",
        PointerType p => Signature(p.ElementType) + "*",
        GenericInstanceType g => g.ElementType.FullName + "<" + string.Join(",", g.GenericArguments.Select(Signature)) + ">",
        _ => type.FullName
    };

    internal bool Exists(MemberReference member)
    {
        if (member is MethodReference method)
            return Hierarchy(method.DeclaringType).SelectMany(t => t.Methods).Any(m =>
                m.Name == method.Name && m.HasThis == method.HasThis &&
                m.GenericParameters.Count == method.GenericParameters.Count &&
                Signature(m.ReturnType) == Signature(method.ReturnType) &&
                m.Parameters.Select(p => Signature(p.ParameterType)).SequenceEqual(method.Parameters.Select(p => Signature(p.ParameterType))));
        if (member is FieldReference field)
            return Hierarchy(field.DeclaringType).SelectMany(t => t.Fields).Any(f =>
                f.Name == field.Name && Signature(f.FieldType) == Signature(field.FieldType));
        return true;
    }

    internal string[] Missing(ModuleDefinition plugin) => plugin.GetMemberReferences()
        .Where(m => IsGameReference(m.DeclaringType) && !Exists(m)).Select(m => m.FullName).Distinct().Order().ToArray();

    internal string[] PatchErrors(ModuleDefinition plugin)
    {
        var errors = new List<string>();
        foreach (var patch in plugin.Types)
        {
            var args = patch.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")
                .SelectMany(a => a.ConstructorArguments).ToArray();
            var owner = args.Select(a => a.Value).OfType<TypeReference>().FirstOrDefault(IsGameReference);
            string? name = args.Select(a => a.Value).OfType<string>().FirstOrDefault();
            if (owner == null || name == null) continue; // Dynamic providers are checked separately.
            var targets = Hierarchy(owner).Select(t => t.Methods.Where(m => m.Name == name).ToArray())
                .FirstOrDefault(methods => methods.Length > 0) ?? Array.Empty<MethodDefinition>();
            var parameterTypes = args.Select(a => a.Value).OfType<CustomAttributeArgument[]>()
                .SelectMany(a => a).Select(a => a.Value).OfType<TypeReference>().ToArray();
            if (parameterTypes.Length > 0)
                targets = targets.Where(m => m.Parameters.Select(p => Signature(p.ParameterType))
                    .SequenceEqual(parameterTypes.Select(Signature))).ToArray();
            if (targets.Length != 1)
            {
                errors.Add($"{patch.Name}: {owner.FullName}.{name} has {targets.Length} targets");
                continue;
            }
            var target = targets[0];
            foreach (var hook in patch.Methods.Where(m => m.CustomAttributes.Any(a =>
                a.AttributeType.Name is "HarmonyPrefix" or "HarmonyPostfix" or "HarmonyFinalizer")))
            foreach (var parameter in hook.Parameters)
            {
                if (parameter.Name.StartsWith("___", StringComparison.Ordinal))
                {
                    if (!Hierarchy(owner).SelectMany(t => t.Fields).Any(f => f.Name == parameter.Name[3..]))
                        errors.Add($"{patch.Name}.{hook.Name}: field injection {parameter.Name} missing");
                }
                else if (!parameter.Name.StartsWith("__", StringComparison.Ordinal) &&
                    !target.Parameters.Any(p => p.Name == parameter.Name))
                    errors.Add($"{patch.Name}.{hook.Name}: argument {parameter.Name} missing in {target.FullName}");
            }
        }
        return errors.ToArray();
    }

    public void Dispose()
    {
        foreach (var module in _modules.Values) module.Dispose();
    }
}
