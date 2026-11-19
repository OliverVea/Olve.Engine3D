using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Devices;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Results;
using Olve.Results.TUnit;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Audio;

public class AudioDeviceManagerTests : IDisposable
{
    private static readonly AudioOutputSettings Settings = new();
    private static readonly AudioDeviceSelection Default = new AudioDeviceSelection.Default();

    private readonly FakeAudioStreamBackend _backend = new();
    private readonly AudioDeviceManager _devices;
    private readonly SoundManager _sounds;
    private readonly AudioMixer _mixer;
    private readonly Id<AudioSegment> _segment = Id.New<AudioSegment>();

    public AudioDeviceManagerTests()
    {
        var audio = new SoftwareAudioBackend(_backend);
        _sounds = SoundRendering.CreateSoundManager(audio);
        _devices = new AudioDeviceManager(audio, NullLogger<AudioDeviceManager>.Instance);
        _mixer = new AudioMixer(_devices, _sounds, NullLogger<AudioMixer>.Instance);
    }

    public void Dispose() => _devices.Dispose();

    private static AudioDeviceSelection Specific(string key) => new AudioDeviceSelection.Specific(key, Default);

    private AudioOutputId Open(AudioDeviceSelection device, AudioOutputSettings? settings = null) =>
        _devices.Open(device, settings ?? Settings).TryPickProblems(out var problems, out var id)
            ? throw new InvalidOperationException(string.Join("; ", problems.Select(p => p.ToDebugString())))
            : id;

    private AudioOutputState State(AudioOutputId id) =>
        _devices.GetState(id).TryPickProblems(out _, out var state) ? throw new InvalidOperationException() : state;

    // A constant 0.5, held for a second
    private SoundId Level()
    {
        var patch = new Patch([new PatchNode.Envelope("level", TimeSpan.Zero, TimeSpan.Zero, 0.5f, TimeSpan.Zero)], [], [], "level");
        if (_sounds.Register(patch, TimeSpan.FromSeconds(1), _segment).TryPickProblems(out _, out var sound))
        {
            throw new InvalidOperationException();
        }

        _sounds.RenderAll();
        return sound;
    }

    private void Play(SoundId sound, BusId bus, bool loop = false) =>
        _mixer.Play(sound, bus, new VoiceSettings(Loop: loop), _segment);

    private static float Peak(float[][] channels) => channels.Max(channel => channel.Max(float.Abs));

    [Test]
    public async Task An_Output_Opens_On_The_Device_Selected()
    {
        var onDefault = Open(Default);
        var onHeadphones = Open(Specific("headphones"));

        await Assert.That(State(onDefault).DeviceKey).IsEqualTo("speakers");
        await Assert.That(State(onHeadphones).DeviceKey).IsEqualTo("headphones");
        await Assert.That(State(onHeadphones).Status).IsEqualTo(AudioOutputStatus.Playing);
        await Assert.That(_devices.DefaultOutput).IsEqualTo(onDefault);
    }

    [Test]
    public async Task A_Missing_Or_Broken_Device_Falls_Back()
    {
        _backend.Broken.Add("headphones");

        var missing = Open(new AudioDeviceSelection.Specific("gone", Specific("headphones")));

        await Assert.That(State(missing).DeviceKey).IsEqualTo("speakers");

        _backend.Broken.Add("speakers");
        await Assert.That(_devices.Open(Specific("headphones"), Settings)).Failed();
    }

    [Test]
    public async Task Settings_Are_Checked()
    {
        await Assert.That(_devices.Open(Default, new AudioOutputSettings(Channels: 6))).Failed();
        await Assert.That(_devices.Open(Default, new AudioOutputSettings(SampleRate: 0))).Failed();
    }

    [Test]
    public async Task Reconfiguring_Keeps_The_Voices_Playing()
    {
        var output = Open(Default);
        var bus = _mixer.AddBus(new BusName("test"));
        Play(Level(), bus, loop: true);
        _backend.Opened[0].Render(1);

        await Assert.That(_devices.Reconfigure(output, Specific("headphones"), new AudioOutputSettings(SampleRate: 44_100))).Succeeded();

        var state = State(output);
        await Assert.That(_backend.Opened[0].IsDisposed).IsTrue();
        await Assert.That(state.DeviceKey).IsEqualTo("headphones");
        await Assert.That(state.Format).IsEqualTo(new AudioFormat(44_100, 2));
        await Assert.That(Peak(_backend.Opened[1].Render(2))).IsGreaterThan(0.3f);
    }

    [Test]
    public async Task A_Failed_Reconfigure_Goes_Back_To_The_Previous_Device()
    {
        var output = Open(Specific("headphones"));
        _backend.Broken.Add("speakers");

        await Assert.That(_devices.Reconfigure(output, Default, Settings)).Failed();
        await Assert.That(State(output).DeviceKey).IsEqualTo("headphones");
        await Assert.That(State(output).Status).IsEqualTo(AudioOutputStatus.Playing);
    }

    [Test]
    public async Task A_Lost_Device_Shows_In_The_State()
    {
        var output = Open(Default);

        _backend.Opened[0].IsLost = true;

        await Assert.That(State(output).Status).IsEqualTo(AudioOutputStatus.Lost);
    }

    [Test]
    public async Task A_Bus_Plays_On_The_Output_It_Is_Routed_To()
    {
        var main = Open(Default);
        var second = Open(Specific("headphones"));
        var routed = _mixer.AddBus(new BusName("routed"));
        var unrouted = _mixer.AddBus(new BusName("unrouted"));
        await Assert.That(_mixer.Route(routed, second)).Succeeded();

        var sound = Level();
        Play(sound, routed);
        var (mainOutput, secondOutput) = (_backend.Opened[0], _backend.Opened[1]);

        await Assert.That(Peak(mainOutput.Render(2))).IsEqualTo(0f);
        await Assert.That(Peak(secondOutput.Render(2))).IsGreaterThan(0.3f);

        Play(sound, unrouted);
        await Assert.That(Peak(mainOutput.Render(2))).IsGreaterThan(0.3f);
        await Assert.That(_mixer.Voices.Select(voice => voice.Output)).IsEquivalentTo([second, main]);
    }

    [Test]
    public async Task A_Bus_Moves_To_The_Default_Output_When_Its_Output_Closes()
    {
        Open(Default);
        var second = Open(Specific("headphones"));
        var bus = _mixer.AddBus(new BusName("routed"));
        _mixer.Route(bus, second);
        _mixer.Set(bus, BusParameter.Gain, 0.5f, TimeSpan.Zero);
        Play(Level(), bus, loop: true);

        await Assert.That(_devices.Close(second)).Succeeded();
        _mixer.Update();

        await Assert.That(_backend.Opened[1].IsDisposed).IsTrue();
        await Assert.That(_mixer.Voices).IsEmpty();

        // The bus keeps its gain on the default output
        Play(Level(), bus);
        var main = _backend.Opened[0].Render(2);
        await Assert.That(Peak(main)).IsEqualTo(0.25f * MathF.Sqrt(0.5f)).Within(1e-3f);
    }

    [Test]
    public async Task Rerouting_A_Bus_Fades_Out_Its_Voices_On_The_Old_Output()
    {
        Open(Default);
        var second = Open(Specific("headphones"));
        var bus = _mixer.AddBus(new BusName("bus"));
        Play(Level(), bus, loop: true);
        _backend.Opened[0].Render(1);

        _mixer.Route(bus, second);
        var faded = _backend.Opened[0].Render(4);
        _mixer.Update();

        await Assert.That(faded[0][^1]).IsEqualTo(0f);
        await Assert.That(_mixer.Voices).IsEmpty();
    }

    [Test]
    public async Task Closing_Every_Output_Leaves_The_Mixer_Silent()
    {
        var output = Open(Default);
        var bus = _mixer.AddBus(new BusName("bus"));
        _devices.Close(output);

        await Assert.That(_mixer.Play(Level(), bus, new VoiceSettings(), _segment)).Succeeded();
        await Assert.That(_mixer.Voices).IsEmpty();
        await Assert.That(_devices.DefaultOutput).IsNull();
    }
}
