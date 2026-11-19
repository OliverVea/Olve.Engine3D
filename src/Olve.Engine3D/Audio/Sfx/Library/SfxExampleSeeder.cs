namespace Olve.Engine3D.Audio.Sfx.Library;

public static class SfxExampleSeeder
{
    public static SfxLibraryContent Seed(SfxLibraryContent content, SfxExamples examples)
    {
        var seededPanels = content.SeededPanels.ToBuilder();
        var seededSounds = content.SeededSounds.ToBuilder();
        var panels = content.Panels.ToBuilder();
        var sounds = content.Sounds.ToBuilder();

        foreach (var panel in examples.Panels.Where(panel => seededPanels.Add(panel.Id)))
        {
            if (panels.All(existing => existing.Id != panel.Id))
            {
                panels.Add(panel);
            }
        }

        foreach (var sound in examples.Sounds.Where(sound => seededSounds.Add(sound.Id)))
        {
            if (sounds.All(existing => existing.Id != sound.Id) && panels.Any(panel => panel.Id == sound.Panel))
            {
                sounds.Add(sound);
            }
        }

        return seededPanels.Count == content.SeededPanels.Count && seededSounds.Count == content.SeededSounds.Count
            ? content
            : new SfxLibraryContent(panels.ToImmutable(), sounds.ToImmutable(), content.Bindings, seededPanels.ToImmutable(), seededSounds.ToImmutable());
    }
}
