namespace Olve.Engine3D.Samples.Synth.Patching;

public readonly record struct ModuleId(int Value)
{
    public override string ToString() => Value.ToString();
}

public sealed record Module(ModuleId Id, ModuleKind Kind)
{
    public ModuleDefinition Definition => Modules.Of(Kind);
}

/// <summary>A jack on a module, by the module and the jack's name.</summary>
public readonly record struct JackRef(ModuleId Module, string Jack);

/// <summary>A cable from an output jack into an input jack.</summary>
public sealed record Cable(JackRef From, JackRef To);

/// <summary>
/// The patch: the modules on the rack, their knob values and the cables between their jacks. An output feeds any
/// number of cables; an input takes one, so a new cable into it replaces the old, and an input without one hears nothing:
/// every connection is a cable. Cables never loop back into a module they come from, through other cables.
/// </summary>
public sealed class SynthPatch
{
    private readonly List<Module> _modules = [];
    private readonly List<Cable> _cables = [];
    private readonly Dictionary<(ModuleId, string), float> _values = [];

    public IReadOnlyList<Module> Modules => _modules;

    public IReadOnlyList<Cable> Cables => _cables;

    /// <summary>Counts each change to the modules or the cables, so what is worked out from them is worked out again only after one.</summary>
    public int Version { get; private set; }

    public Module Add(ModuleKind kind)
    {
        var module = new Module(new ModuleId(_modules.Count + 1), kind);
        _modules.Add(module);
        Version++;
        foreach (var parameter in module.Definition.Parameters)
        {
            _values[(module.Id, parameter.Name)] = parameter.Default;
        }

        return module;
    }

    public Module? Find(ModuleId id) => _modules.FirstOrDefault(m => m.Id == id);

    public float GetValue(ModuleId module, string parameter) => _values.GetValueOrDefault((module, parameter));

    /// <summary>Sets a knob, clamped to its range and, if stepped, rounded to a whole step. Returns the value set.</summary>
    public Result<float> SetValue(ModuleId module, string parameter, float value)
    {
        if (Find(module) is not { } found)
        {
            return new ResultProblem("No module {0}", module);
        }

        if (found.Definition.FindParameter(parameter) is not { } definition)
        {
            return new ResultProblem("{0} has no knob '{1}'", found.Definition.Title, parameter);
        }

        var clamped = float.Clamp(value, definition.Min, definition.Max);
        if (definition.Scale == ParameterScale.Stepped)
        {
            clamped = MathF.Round(clamped);
        }

        _values[(module, parameter)] = clamped;
        return clamped;
    }

    public Cable? CableInto(JackRef input) => _cables.FirstOrDefault(c => c.To == input);

    public IEnumerable<Cable> CablesAt(JackRef jack) => _cables.Where(c => c.From == jack || c.To == jack);

    public JackDefinition? FindJack(JackRef jack) => Find(jack.Module)?.Definition.FindJack(jack.Jack);

    /// <summary>
    /// What a jack carries now: its own kind, or for an output that follows an input (<see cref="JackDefinition.Follows"/>),
    /// audio while what that input hears is audio, else a control voltage. A jack the rack doesn't have carries a control voltage.
    /// </summary>
    public SignalKind SignalOf(JackRef jack)
    {
        if (FindJack(jack) is not { } definition)
        {
            return SignalKind.Control;
        }

        if (definition.Follows is not { } follows)
        {
            return definition.Signal;
        }

        // Cables never loop, so following them back ends
        return CableInto(new JackRef(jack.Module, follows)) is { } heard && SignalOf(heard.From) == SignalKind.Audio ? SignalKind.Audio : SignalKind.Control;
    }

    /// <summary>
    /// The cable that would join the two jacks, in either order: from the output into the input. Fails, saying why, if
    /// they can't be joined.
    /// </summary>
    public Result<Cable> Plan(JackRef a, JackRef b)
    {
        if (FindJack(a) is not { } jackA)
        {
            return new ResultProblem("No jack {0} on module {1}", a.Jack, a.Module);
        }

        if (FindJack(b) is not { } jackB)
        {
            return new ResultProblem("No jack {0} on module {1}", b.Jack, b.Module);
        }

        if (jackA.Direction == jackB.Direction)
        {
            return new ResultProblem(jackA.Direction == JackDirection.Output
                ? "A cable joins an output to an input, not two outputs"
                : "A cable joins an output to an input, not two inputs");
        }

        var (from, to, output, input) = jackA.Direction == JackDirection.Output ? (a, b, jackA, jackB) : (b, a, jackB, jackA);
        if (input.Signal == SignalKind.Gate && output.Signal != SignalKind.Gate)
        {
            return new ResultProblem("The {0} input takes only a gate", input.Name.ToUpperInvariant());
        }

        if (from.Module == to.Module || LoopsBack(new Cable(from, to)))
        {
            return new ResultProblem("A cable can't loop back into a module it comes from");
        }

        return new Cable(from, to);
    }

    /// <summary>Joins the two jacks, replacing any cable already in the input.</summary>
    public Result<CableChange> Connect(JackRef a, JackRef b)
    {
        if (Plan(a, b).TryPickProblems(out var problems, out var cable))
        {
            return problems;
        }

        var replaced = CableInto(cable.To);
        if (replaced == cable)
        {
            return new CableChange(cable, null);
        }

        if (replaced is not null)
        {
            _cables.Remove(replaced);
        }

        _cables.Add(cable);
        Version++;
        return new CableChange(cable, replaced);
    }

    public DeletionResult Disconnect(Cable cable)
    {
        if (!_cables.Remove(cable))
        {
            return DeletionResult.NotFound();
        }

        Version++;
        return DeletionResult.Success();
    }

    // Whether the cable would close a loop through the cables, less the one it replaces: whether a signal leaving its
    // input's module comes back round to its output's
    private bool LoopsBack(Cable cable)
    {
        var seen = new HashSet<ModuleId>();
        var pending = new Stack<ModuleId>([cable.To.Module]);
        while (pending.TryPop(out var module))
        {
            if (module == cable.From.Module)
            {
                return true;
            }

            if (seen.Add(module))
            {
                foreach (var next in _cables.Where(c => c.From.Module == module && c.To != cable.To))
                {
                    pending.Push(next.To.Module);
                }
            }
        }

        return false;
    }
}

/// <summary>A cable added, and the one it replaced in its input, if any.</summary>
public sealed record CableChange(Cable Added, Cable? Replaced);
