namespace Olve.Engine3D.Samples.Synth.Playing;

/// <summary>
/// What a browser audio reads back once a frame in one call (<c>readFrame</c> in synth.js), and what it says: whether the
/// audio runs (1 or 0), its sample rate, the output's level, the left and right speakers' peaks, how many metered
/// outputs' peaks follow and the peaks, then each watched knob's value and each watched sequencer's step heard (-1 for
/// none). The audio sends peaks only while it runs. Until the first frame is read, it says nothing.
/// </summary>
public sealed class AudioFrame
{
    public const int RunningAt = 0, RateAt = 1, LevelAt = 2, SpeakersAt = 3, MetersAt = 5, PeaksAt = 6;

    private double[] _values = new double[64];
    private int _peaks, _knobs, _steps;

    /// <summary>Whether a frame has been read.</summary>
    public bool IsRead { get; private set; }

    /// <summary>Where to read the next frame into: room for this many metered outputs' peaks, watched knobs and watched sequencers.</summary>
    public Span<double> Into(int metered, int knobs, int steps = 0)
    {
        var size = PeaksAt + metered + knobs + steps;
        if (_values.Length < size)
        {
            _values = new double[size * 2];
        }

        return _values.AsSpan(0, size);
    }

    /// <summary>A frame has been read into <see cref="Into"/>, with this many knobs and sequencers watched.</summary>
    public void Read(int knobs, int steps = 0)
    {
        _peaks = (int)_values[MetersAt];
        _knobs = knobs;
        _steps = steps;
        IsRead = true;
    }

    /// <summary>The outputs metered changed: the peaks read are for the ones before, so none are read until the next frame.</summary>
    public void Remeter() => _peaks = 0;

    public bool IsRunning => IsRead && _values[RunningAt] != 0;

    public float SampleRate => (float)_values[RateAt];

    public float OutputLevel => (float)_values[LevelAt];

    /// <summary>Fills <paramref name="peaks"/> with the speakers' peaks, left then right: none while the audio isn't running.</summary>
    public int ReadSpeakers(Span<float> peaks)
    {
        if (!IsRunning)
        {
            return 0;
        }

        var read = int.Min(2, peaks.Length);
        for (var i = 0; i < read; i++)
        {
            peaks[i] = (float)_values[SpeakersAt + i];
        }

        return read;
    }

    /// <summary>Fills <paramref name="peaks"/> with the metered outputs' peaks, in the order they were metered.</summary>
    public int ReadLevels(Span<float> peaks)
    {
        var read = int.Min(peaks.Length, _peaks);
        for (var i = 0; i < read; i++)
        {
            peaks[i] = (float)_values[PeaksAt + i];
        }

        return read;
    }

    /// <summary>The value of the watched knob in this slot, if it was watched when the frame was read.</summary>
    public bool TryReadKnob(int slot, out float value)
    {
        value = slot < _knobs ? (float)_values[PeaksAt + (int)_values[MetersAt] + slot] : 0;
        return IsRead && slot < _knobs;
    }

    /// <summary>
    /// The step heard of the watched sequencer in this slot, from 0, or null while it plays none, if it was watched when
    /// the frame was read.
    /// </summary>
    public bool TryReadStep(int slot, out int? step)
    {
        var read = IsRead && slot < _steps;
        var value = read ? (int)_values[PeaksAt + (int)_values[MetersAt] + _knobs + slot] : -1;
        step = value >= 0 ? value : null;
        return read;
    }
}
