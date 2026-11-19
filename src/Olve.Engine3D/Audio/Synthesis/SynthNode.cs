using System.Diagnostics;

namespace Olve.Engine3D.Audio.Synthesis;

/// <summary>A parameter's value for each frame of a block: its base value plus the signals modulating it.</summary>
internal sealed class SynthParameter(float baseValue)
{
    private float _step;
    private float _target;
    private int _remaining;

    public float BaseValue { get; set; } = baseValue;
    public List<SynthNode> Modulators { get; } = [];
    public float[] Values { get; } = new float[PatchRenderer.BlockFrames];

    /// <summary>Moves the base value to the target in a straight line over the frames.</summary>
    public void RampTo(float target, int frames)
    {
        if (frames <= 0)
        {
            BaseValue = target;
            _remaining = 0;
            return;
        }

        _target = target;
        _step = (target - BaseValue) / frames;
        _remaining = frames;
    }

    public void Fill(int frames)
    {
        var values = Values.AsSpan(0, frames);
        for (var frame = 0; frame < frames; frame++)
        {
            if (_remaining > 0)
            {
                BaseValue = --_remaining == 0 ? _target : BaseValue + _step;
            }

            values[frame] = BaseValue;
        }

        foreach (var modulator in Modulators)
        {
            var signal = modulator.Output.AsSpan(0, frames);
            for (var frame = 0; frame < frames; frame++)
            {
                values[frame] += signal[frame];
            }
        }
    }
}

/// <summary>A node at work: its block of output, the audio summed into it, and its parameters.</summary>
internal abstract class SynthNode
{
    public float[] Output { get; } = new float[PatchRenderer.BlockFrames];
    public List<SynthNode> AudioInputs { get; } = [];
    public Dictionary<string, SynthParameter> Parameters { get; } = [];
    public virtual bool TakesAudio => false;

    public void ProcessBlock(long startFrame, int frames, long gateFrames)
    {
        foreach (var parameter in Parameters.Values)
        {
            parameter.Fill(frames);
        }

        Process(startFrame, frames, gateFrames);
    }

    protected abstract void Process(long startFrame, int frames, long gateFrames);

    /// <summary>The audio connected into the node, summed.</summary>
    protected void SumAudioInputs(Span<float> into)
    {
        into.Clear();
        foreach (var input in AudioInputs)
        {
            var signal = input.Output.AsSpan(0, into.Length);
            for (var frame = 0; frame < into.Length; frame++)
            {
                into[frame] += signal[frame];
            }
        }
    }

    public static SynthNode Create(PatchNode node, int sampleRate) => node switch
    {
        PatchNode.Oscillator oscillator => new OscillatorNode(oscillator, sampleRate),
        PatchNode.Noise noise => new NoiseNode(noise),
        PatchNode.Constant constant => new ConstantNode(constant),
        PatchNode.Filter filter => new FilterNode(filter, sampleRate),
        PatchNode.Gain gain => new GainNode(gain),
        PatchNode.Envelope envelope => new EnvelopeNode(envelope, sampleRate),
        PatchNode.Distortion distortion => new DistortionNode(distortion, sampleRate),
        _ => throw new UnreachableException($"No synth node for {node.GetType().Name}"),
    };
}

/// <summary>Sawtooth and square are band-limited with PolyBLEP; sine and triangle have little to alias.</summary>
internal sealed class OscillatorNode : SynthNode
{
    private readonly Waveform _waveform;
    private readonly int _sampleRate;
    private readonly SynthParameter _frequency;
    private double _phase;

    public OscillatorNode(PatchNode.Oscillator oscillator, int sampleRate)
    {
        _waveform = oscillator.Waveform;
        _sampleRate = sampleRate;
        _frequency = Parameters[PatchNode.Oscillator.FrequencyParameter] = new SynthParameter(oscillator.Frequency);
    }

    protected override void Process(long startFrame, int frames, long gateFrames)
    {
        var frequency = _frequency.Values;
        for (var frame = 0; frame < frames; frame++)
        {
            var increment = frequency[frame] / _sampleRate;
            var step = double.Clamp(double.Abs(increment), 1e-9, 0.5);

            Output[frame] = (float)(_waveform switch
            {
                Waveform.Sine => double.Sin(double.Tau * _phase),
                Waveform.Sawtooth => 2 * _phase - 1 - PolyBlep(_phase, step),
                Waveform.Square => (_phase < 0.5 ? 1 : -1) + PolyBlep(_phase, step) - PolyBlep((_phase + 0.5) % 1, step),
                Waveform.Triangle => 1 - 4 * double.Abs(_phase - 0.5),
                _ => 0,
            });

            _phase = (_phase + increment) % 1;
            if (_phase < 0)
            {
                _phase += 1;
            }
        }
    }

    // Smooths the jump at the start of each period over one sample on each side
    private static double PolyBlep(double phase, double step)
    {
        if (phase < step)
        {
            var t = phase / step;
            return t + t - t * t - 1;
        }

        if (phase > 1 - step)
        {
            var t = (phase - 1) / step;
            return t * t + t + t + 1;
        }

        return 0;
    }
}

/// <summary>Its value parameter, as a signal.</summary>
internal sealed class ConstantNode : SynthNode
{
    private readonly SynthParameter _value;

    public ConstantNode(PatchNode.Constant constant)
    {
        _value = Parameters[PatchNode.Constant.ValueParameter] = new SynthParameter(constant.Value);
    }

    protected override void Process(long startFrame, int frames, long gateFrames) =>
        _value.Values.AsSpan(0, frames).CopyTo(Output);
}

/// <summary>xorshift32, so the same seed gives the same noise everywhere.</summary>
internal sealed class NoiseNode(PatchNode.Noise noise) : SynthNode
{
    private uint _state = noise.Seed == 0 ? 0x9E3779B9u : (uint)noise.Seed;

    protected override void Process(long startFrame, int frames, long gateFrames)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            Output[frame] = (float)(_state / (double)uint.MaxValue * 2 - 1);
        }
    }
}

/// <summary>A biquad filter of its audio input (<see cref="Biquad"/>).</summary>
internal sealed class FilterNode : SynthNode
{
    private readonly FilterType _type;
    private readonly int _sampleRate;
    private readonly SynthParameter _cutoff;
    private readonly SynthParameter _q;
    private readonly SynthParameter _gain;
    private readonly float[] _input = new float[PatchRenderer.BlockFrames];

    private float _lastCutoff = float.NaN;
    private float _lastQ = float.NaN;
    private float _lastGain = float.NaN;
    private Biquad _biquad;

    public FilterNode(PatchNode.Filter filter, int sampleRate)
    {
        _type = filter.Type;
        _sampleRate = sampleRate;
        _cutoff = Parameters[PatchNode.Filter.CutoffParameter] = new SynthParameter(filter.Cutoff);
        _q = Parameters[PatchNode.Filter.QParameter] = new SynthParameter(filter.Q);
        _gain = Parameters[PatchNode.Filter.GainParameter] = new SynthParameter(filter.GainDb);
    }

    public override bool TakesAudio => true;

    protected override void Process(long startFrame, int frames, long gateFrames)
    {
        var input = _input.AsSpan(0, frames);
        SumAudioInputs(input);

        for (var frame = 0; frame < frames; frame++)
        {
            var cutoff = _cutoff.Values[frame];
            var q = _q.Values[frame];
            var gain = _gain.Values[frame];
            if (cutoff != _lastCutoff || q != _lastQ || gain != _lastGain)
            {
                (_lastCutoff, _lastQ, _lastGain) = (cutoff, q, gain);
                _biquad.SetCoefficients(_type, cutoff, q, gain, _sampleRate);
            }

            Output[frame] = (float)_biquad.Process(input[frame]);
        }
    }
}

internal sealed class GainNode : SynthNode
{
    private readonly SynthParameter _amount;
    private readonly float[] _input = new float[PatchRenderer.BlockFrames];

    public GainNode(PatchNode.Gain gain)
    {
        _amount = Parameters[PatchNode.Gain.AmountParameter] = new SynthParameter(gain.Amount);
    }

    public override bool TakesAudio => true;

    protected override void Process(long startFrame, int frames, long gateFrames)
    {
        var input = _input.AsSpan(0, frames);
        SumAudioInputs(input);

        for (var frame = 0; frame < frames; frame++)
        {
            Output[frame] = input[frame] * _amount.Values[frame];
        }
    }
}

/// <summary>
/// Linear segments. Following the note, it's a pure function of the frame, so a block can start anywhere; following a
/// patched gate, it moves from its current level, so a gate that opens again mid-release doesn't jump.
/// </summary>
internal sealed class EnvelopeNode : SynthNode
{
    private readonly double _attack;
    private readonly double _decay;
    private readonly double _sustain;
    private readonly double _release;
    private readonly SynthParameter _gate;

    private Stage _stage = Stage.Idle;
    private double _level;
    private double _releaseStep;

    private enum Stage
    {
        Idle,
        Attack,
        Decay,
        Sustain,
        Release,
    }

    public EnvelopeNode(PatchNode.Envelope envelope, int sampleRate)
    {
        _attack = envelope.Attack.TotalSeconds * sampleRate;
        _decay = envelope.Decay.TotalSeconds * sampleRate;
        _sustain = double.Clamp(envelope.Sustain, 0, 1);
        _release = envelope.Release.TotalSeconds * sampleRate;
        _gate = Parameters[PatchNode.Envelope.GateParameter] = new SynthParameter(0);
    }

    protected override void Process(long startFrame, int frames, long gateFrames)
    {
        var patched = _gate.Modulators.Count > 0;
        for (var frame = 0; frame < frames; frame++)
        {
            Output[frame] = (float)(patched ? Step(_gate.Values[frame] > 0.5f) : Level(startFrame + frame, gateFrames));
        }
    }

    // Following a gate signal: one frame on from the current level
    private double Step(bool open)
    {
        if (open && _stage is Stage.Idle or Stage.Release)
        {
            _stage = Stage.Attack;
        }
        else if (!open && _stage is Stage.Attack or Stage.Decay or Stage.Sustain)
        {
            _stage = Stage.Release;
            _releaseStep = _level / double.Max(_release, 1);
        }

        switch (_stage)
        {
            case Stage.Attack:
                _level += 1 / double.Max(_attack, 1);
                if (_level >= 1)
                {
                    (_level, _stage) = (1, Stage.Decay);
                }

                break;
            case Stage.Decay:
                _level -= (1 - _sustain) / double.Max(_decay, 1);
                if (_level <= _sustain)
                {
                    (_level, _stage) = (_sustain, Stage.Sustain);
                }

                break;
            case Stage.Release:
                _level -= _releaseStep;
                if (_level <= 0)
                {
                    (_level, _stage) = (0, Stage.Idle);
                }

                break;
        }

        return _level;
    }

    private double Level(long frame, long gateFrames)
    {
        if (frame < gateFrames)
        {
            return Held(frame);
        }

        var released = frame - gateFrames;
        return _release > 0 ? Held(gateFrames) * double.Max(0, 1 - released / _release) : 0;
    }

    // The level while the note is held: attack, decay, then sustain
    private double Held(long frame)
    {
        if (frame < _attack)
        {
            return frame / _attack;
        }

        var decayed = frame - _attack;
        return decayed < _decay ? 1 - (1 - _sustain) * decayed / _decay : _sustain;
    }
}

/// <summary>
/// A wave shaper at 4x the rate: each frame is interpolated into four, shaped, then lowpassed below the original
/// Nyquist and taken back down, so the harmonics the curve adds don't fold back as aliasing.
/// </summary>
internal sealed class DistortionNode : SynthNode
{
    private const int Oversampling = 4;

    // Butterworth: no resonant peak
    private const double ButterworthQ = -3.0103;

    private readonly DistortionCurve _curve;
    private readonly SynthParameter _drive;
    private readonly float[] _input = new float[PatchRenderer.BlockFrames];
    private Biquad _first;
    private Biquad _second;
    private float _previous;

    public DistortionNode(PatchNode.Distortion distortion, int sampleRate)
    {
        _curve = distortion.Curve;
        _drive = Parameters[PatchNode.Distortion.DriveParameter] = new SynthParameter(distortion.Drive);
        var oversampledRate = sampleRate * Oversampling;
        _first.SetCoefficients(FilterType.Lowpass, sampleRate * 0.45, ButterworthQ, 0, oversampledRate);
        _second.SetCoefficients(FilterType.Lowpass, sampleRate * 0.45, ButterworthQ, 0, oversampledRate);
    }

    public override bool TakesAudio => true;

    protected override void Process(long startFrame, int frames, long gateFrames)
    {
        var input = _input.AsSpan(0, frames);
        SumAudioInputs(input);
        for (var frame = 0; frame < frames; frame++)
        {
            var drive = _drive.Values[frame];
            var current = input[frame];
            var output = 0.0;
            for (var step = 1; step <= Oversampling; step++)
            {
                var x = _previous + (current - _previous) * step / (float)Oversampling;
                output = _second.Process(_first.Process(Shape(x * drive)));
            }

            _previous = current;
            Output[frame] = (float)output;
        }
    }

    private double Shape(double x) => _curve switch
    {
        DistortionCurve.SoftClip => double.Tanh(x),
        DistortionCurve.HardClip => double.Clamp(x, -1, 1),
        _ => Fold(x),
    };

    // Reflects at -1 and 1: a triangle wave of the input
    private static double Fold(double x)
    {
        var y = ((x + 1) % 4 + 4) % 4;
        return y < 2 ? y - 1 : 3 - y;
    }
}
