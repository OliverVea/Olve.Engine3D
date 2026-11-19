using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Olve.Engine3D.Audio.Devices;

namespace Olve.Engine3D.Audio;

public static class AudioServiceRegistration
{
    /// <summary>
    /// Registers the engine's audio services. The host registers the <see cref="IAudioDeviceBackend"/> and the
    /// <see cref="ISoundBackend"/> (silent for a host without audio, <see cref="AddSoftwareAudio"/> for one that plays a
    /// stream) and may register the <see cref="AudioStartupOptions"/>.
    /// </summary>
    public static IServiceCollection AddAudioServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IAudioDeviceBackend, SilentAudioDeviceBackend>();
        services.TryAddSingleton<ISoundBackend, SilentSoundBackend>();
        services.TryAddSingleton(AudioStartupOptions.Default);
        services.TryAddSingleton<AudioDeviceManager>();
        services.TryAddSingleton<AudioOutputStartup>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(SoundRenderBudget.RealTime);
        services.TryAddSingleton<SoundManager>();
        services.TryAddSingleton<AudioMixer>();
        services.TryAddScoped<AudioSegment>();
        services.TryAddSingleton<TestTone>();
        return services;
    }

    public static IServiceCollection AddSoftwareAudio(this IServiceCollection services)
    {
        services.AddSingleton<SoftwareAudioBackend>();
        services.AddSingleton<IAudioDeviceBackend>(provider => provider.GetRequiredService<SoftwareAudioBackend>());
        services.AddSingleton<ISoundBackend>(provider => provider.GetRequiredService<SoftwareAudioBackend>());
        return services;
    }
}
