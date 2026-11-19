using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Rack;
using Olve.Engine3D.Scenes;

namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>What one scope's screen shows now: the trace, evenly across the screen, from -1 at the bottom to 1 at the top.</summary>
public sealed class ScopeScreen(float millisecondsPerDivision)
{
    public float MillisecondsPerDivision { get; } = millisecondsPerDivision;

    public float[] Points { get; } = new float[ScopeService.Points];

    /// <summary>Whether the trace is drawn: once the audio runs, flat without a signal.</summary>
    public bool Live { get; internal set; }

    /// <summary>The trace's largest swing from the middle, from 0 to 1.</summary>
    public float Peak { get; internal set; }
}

/// <summary>
/// Each frame, the rack's scopes read their inputs back from the audio and trace them (<see cref="ScopeTrace"/>). The left
/// scope's time base suits audio; the right's, slower, suits control signals such as an LFO or an envelope.
/// </summary>
public sealed class ScopeService(SynthRack rack) : ISceneService
{
    public const int Points = 256;

    private readonly float[] _samples = new float[ScopeTrace.MaxSamples];

    /// <summary>The left scope, then the right.</summary>
    public IReadOnlyList<ScopeScreen> Screens { get; } = [new(2), new(50)];

    public Result Update()
    {
        var scopes = rack.Patch.Modules.Where(m => m.Kind == ModuleKind.Scope).Take(RackLayout.MaxScopes).ToList();
        for (var i = 0; i < Screens.Count; i++)
        {
            var screen = Screens[i];
            var read = 0;
            if (i < scopes.Count && rack.Audio.IsRunning)
            {
                var wanted = ScopeTrace.SamplesToRead(screen.MillisecondsPerDivision, rack.Audio.SampleRate);
                read = rack.Audio.ReadScope(scopes[i].Id, _samples.AsSpan(0, wanted));
            }

            screen.Live = read >= 2;
            if (!screen.Live)
            {
                Array.Clear(screen.Points);
                screen.Peak = 0;
                continue;
            }

            ScopeTrace.Trace(_samples.AsSpan(0, read), ScopeTrace.Window(screen.MillisecondsPerDivision, rack.Audio.SampleRate), screen.Points);
            screen.Peak = screen.Points.Max(float.Abs);
        }

        return Result.Success();
    }
}
