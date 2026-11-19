namespace Olve.Engine3D.Audio.Synthesis;

/// <summary>A patch compiled for a sample rate: its nodes in the order they process, each block.</summary>
internal sealed class PatchVoice
{
    private readonly SynthNode[] _nodes;
    private readonly SynthNode _output;
    private readonly Dictionary<string, SynthParameter> _parameters;

    private PatchVoice(
        SynthNode[] nodes, SynthNode output, Dictionary<string, SynthParameter> parameters, long releaseFrames)
    {
        _nodes = nodes;
        _output = output;
        _parameters = parameters;
        ReleaseFrames = releaseFrames;
    }

    /// <summary>Moves an exposed parameter to the value over the frames; an unknown name is ignored.</summary>
    public void SetParameter(string name, float value, int frames)
    {
        if (_parameters.TryGetValue(name, out var parameter))
        {
            parameter.RampTo(value, frames);
        }
    }

    /// <summary>How long the voice sounds after the note ends: its longest envelope release.</summary>
    public long ReleaseFrames { get; }

    /// <summary>Processes the frames starting at <paramref name="startFrame"/> into <paramref name="output"/>.</summary>
    public void Process(long startFrame, int frames, long gateFrames, Span<float> output)
    {
        foreach (var node in _nodes)
        {
            node.ProcessBlock(startFrame, frames, gateFrames);
        }

        _output.Output.AsSpan(0, frames).CopyTo(output);
    }

    public static Result<PatchVoice> Compile(Patch patch, int sampleRate, IReadOnlyDictionary<string, float> parameters)
    {
        var nodes = new Dictionary<string, SynthNode>();
        foreach (var node in patch.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Name))
            {
                return new ResultProblem("Every node needs a name");
            }

            if (!nodes.TryAdd(node.Name, SynthNode.Create(node, sampleRate)))
            {
                return new ResultProblem("Two nodes are named '{0}'", node.Name);
            }
        }

        if (!nodes.TryGetValue(patch.Output, out var output))
        {
            return new ResultProblem("The output node '{0}' isn't in the patch", patch.Output);
        }

        var dependencies = nodes.Keys.ToDictionary(name => name, _ => new List<string>());
        foreach (var connection in patch.Connections)
        {
            if (Connect(nodes, connection).TryPickProblems(out var problems))
            {
                return problems;
            }

            dependencies[connection.To].Add(connection.From);
        }

        if (SetParameters(patch, nodes, parameters).TryPickProblems(out var parameterProblems, out var exposed))
        {
            return parameterProblems;
        }

        if (Order(dependencies).TryPickProblems(out var orderProblems, out var order))
        {
            return orderProblems;
        }

        var releaseFrames = patch.Nodes.OfType<PatchNode.Envelope>()
            .Select(envelope => (long)double.Ceiling(envelope.Release.TotalSeconds * sampleRate))
            .DefaultIfEmpty(0)
            .Max();

        return new PatchVoice(
            [.. order.Select(name => nodes[name])],
            output,
            exposed,
            releaseFrames);
    }

    private static Result Connect(Dictionary<string, SynthNode> nodes, PatchConnection connection)
    {
        if (!nodes.TryGetValue(connection.From, out var from))
        {
            return new ResultProblem("A connection comes from '{0}', which isn't in the patch", connection.From);
        }

        if (!nodes.TryGetValue(connection.To, out var to))
        {
            return new ResultProblem("A connection goes to '{0}', which isn't in the patch", connection.To);
        }

        if (connection.Parameter is { } parameterName)
        {
            if (!to.Parameters.TryGetValue(parameterName, out var parameter))
            {
                return new ResultProblem("'{0}' has no parameter '{1}' to modulate", connection.To, parameterName);
            }

            parameter.Modulators.Add(from);
            return Result.Success();
        }

        if (!to.TakesAudio)
        {
            return new ResultProblem("'{0}' takes no audio input; connect '{1}' to one of its parameters", connection.To, connection.From);
        }

        to.AudioInputs.Add(from);
        return Result.Success();
    }

    // Sets the given values; returns the exposed parameters by name
    private static Result<Dictionary<string, SynthParameter>> SetParameters(
        Patch patch, Dictionary<string, SynthNode> nodes, IReadOnlyDictionary<string, float> values)
    {
        var exposed = new Dictionary<string, SynthParameter>();
        foreach (var parameter in patch.Parameters)
        {
            if (!nodes.TryGetValue(parameter.Node, out var node))
            {
                return new ResultProblem("Parameter '{0}' sets node '{1}', which isn't in the patch", parameter.Name, parameter.Node);
            }

            if (!node.Parameters.TryGetValue(parameter.Parameter, out var target))
            {
                return new ResultProblem("Parameter '{0}' sets '{1}' of '{2}', which has no such parameter", parameter.Name, parameter.Parameter, parameter.Node);
            }

            if (!exposed.TryAdd(parameter.Name, target))
            {
                return new ResultProblem("Two parameters are named '{0}'", parameter.Name);
            }
        }

        foreach (var (name, value) in values)
        {
            if (!exposed.TryGetValue(name, out var target))
            {
                return new ResultProblem("The patch has no parameter '{0}'", name);
            }

            target.BaseValue = value;
        }

        return exposed;
    }

    // Each node after the nodes it reads from; a cycle has no such order
    private static Result<List<string>> Order(Dictionary<string, List<string>> dependencies)
    {
        var order = new List<string>();
        var state = new Dictionary<string, bool>(); // false: visiting, true: done

        foreach (var name in dependencies.Keys)
        {
            if (Visit(name).TryPickProblems(out var problems))
            {
                return problems;
            }
        }

        return order;

        Result Visit(string name)
        {
            if (state.TryGetValue(name, out var done))
            {
                return done ? Result.Success() : new ResultProblem("The patch has a cycle through '{0}'", name);
            }

            state[name] = false;
            foreach (var dependency in dependencies[name])
            {
                if (Visit(dependency).TryPickProblems(out var problems))
                {
                    return problems;
                }
            }

            state[name] = true;
            order.Add(name);
            return Result.Success();
        }
    }
}
