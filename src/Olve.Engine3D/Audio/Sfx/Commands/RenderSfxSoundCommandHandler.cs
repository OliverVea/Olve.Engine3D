using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Audio.Sfx.Slots;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Engine3D.Commands;

namespace Olve.Engine3D.Audio.Sfx.Commands;

public class RenderSfxSoundCommandHandler(SfxLibraryManager library, ILogger<RenderSfxSoundCommandHandler> logger) : ICommandHandler
{
    private static readonly CommandArgument SoundArgument = new("sound", "The sound's id or name", true);
    private static readonly CommandArgument PathArgument = new("path", "WAV file to write", true);

    public string Verb => "render-sfx-sound";
    public string HelpString => $"Renders a sound from the SFX library to a {SoundManager.SampleRate / 1000} kHz WAV file";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [SoundArgument, PathArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (SfxArguments.Sound(library, context, SoundArgument).TryPickProblems(out var problems, out var sound)
            || context.GetRequiredArgument(PathArgument).TryPickProblems(out problems, out var path))
        {
            return problems;
        }

        if (library.Panel(sound.Panel) is not { } panel)
        {
            return new ResultProblem("Sound '{0}' is made from panel {1}, which isn't in the library", sound.Name, sound.Panel);
        }

        if (PatchRenderer.Render(panel.Patch, SoundManager.SampleRate, sound.Length, SfxRendering.InputValues(sound, panel))
            .TryPickProblems(out problems, out var buffer))
        {
            return problems;
        }

        try
        {
            using var file = File.Create(path);
            WavEncoder.Write(file, buffer, SoundManager.SampleRate);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ResultProblem("Failed to write '{0}': {1}", path, exception.Message);
        }

        logger.LogInformation("Rendered SFX sound '{Sound}' ({Frames} frames) to '{Path}'", sound.Name, buffer.Frames, path);
        return new CommandOutput(SfxArguments.Json(new SfxRenderJson(path, buffer.Frames, SoundManager.SampleRate), SfxCommandJsonContext.Default.SfxRenderJson));
    }
}
