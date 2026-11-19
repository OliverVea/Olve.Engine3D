using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Olve.Engine3D.Audio.Sfx.Commands;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Audio.Sfx.Slots;
using Olve.Engine3D.Commands;
using Olve.Engine3D.Scenes;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx;

public static class SfxRegistration
{
    public static IServiceCollection AddSfxLibrary(this IServiceCollection services, SfxLibraryOptions options)
    {
        services.TryAddSingleton(options);
        services.TryAddSingleton<SfxLibraryStore>();
        services.TryAddSingleton<SfxLibraryManager>();
        services.AddSingleton<ICommandHandler, ListSfxPanelsCommandHandler>();
        services.AddSingleton<ICommandHandler, QuerySfxPanelCommandHandler>();
        services.AddSingleton<ICommandHandler, ListSfxSoundsCommandHandler>();
        services.AddSingleton<ICommandHandler, QuerySfxSoundCommandHandler>();
        services.AddSingleton<ICommandHandler, CreateSfxSoundCommandHandler>();
        services.AddSingleton<ICommandHandler, DeleteSfxSoundCommandHandler>();
        services.AddSingleton<ICommandHandler, DeleteSfxPanelCommandHandler>();
        services.AddSingleton<ICommandHandler, ListSfxSlotsCommandHandler>();
        services.AddSingleton<ICommandHandler, BindSfxSlotCommandHandler>();
        services.AddSingleton<ICommandHandler, UnbindSfxSlotCommandHandler>();
        services.AddSingleton<ICommandHandler, ExportSfxLibraryCommandHandler>();
        services.AddSingleton<ICommandHandler, ImportSfxLibraryCommandHandler>();
        services.AddSingleton<ICommandHandler, RenderSfxSoundCommandHandler>();
        return services;
    }

    public static IServiceCollection AddSfxSceneServices(this IServiceCollection services, Id<IScene> sceneId)
    {
        services.AddSceneService<SfxSlotService>(sceneId);
        services.AddSceneService<SfxPreviewService>(sceneId);
        services.AddImmediateEventSceneService(sceneId,
            (SfxLibraryManager library) => library.OnChanged,
            (SfxSlotService slots, SfxLibraryContent content) => slots.RenderBoundSounds(content),
            prefill: library => [library.Content]);
        services.AddImmediateEventSceneService(sceneId,
            (SfxLibraryManager library) => library.OnChanged,
            (SfxPreviewService previews, SfxLibraryContent content) => previews.DropStalePreviews(content));
        return services;
    }
}
