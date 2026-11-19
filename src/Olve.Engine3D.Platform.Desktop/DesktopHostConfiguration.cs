using Microsoft.Extensions.Configuration;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>
/// Reads <see cref="DesktopHostOptions"/> from the <c>Desktop</c> configuration section.
/// </summary>
public static class DesktopHostConfiguration
{
    public const string SectionName = "Desktop";
    public const string InstanceIdKey = "InstanceId";
    public const string ListenKey = "Listen";
    public const string FrameLoopKey = "FrameLoop";
    public const string ResolutionKey = "Resolution";
    public const string UserStorageRootKey = "UserStorageRoot";
    public const string FileExchangeFolderKey = "FileExchangeFolder";
    public const string WindowSectionName = "Window";
    public const string WindowTitleKey = "Title";
    public const string WindowSizeKey = "Size";
    public const string WindowSamplesKey = "Samples";
    public const string WindowVSyncKey = "VSync";

    public static Result<DesktopHostOptions> Bind(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var windowSection = section.GetSection(WindowSectionName);
        var reader = new SectionReader();
        var defaults = new DesktopHostOptions();
        if (AudioConfiguration.Bind(configuration).TryPickProblems(out var audioProblems, out var audio))
        {
            reader.Problems.AddRange(audioProblems);
        }

        var options = new DesktopHostOptions
        {
            InstanceId = section[InstanceIdKey],
            Listen = reader.Bool(section, ListenKey, defaults.Listen),
            FrameLoop = reader.Enum(section, FrameLoopKey, defaults.FrameLoop),
            Resolution = reader.Size(section, ResolutionKey),
            UserStorageRoot = section[UserStorageRootKey],
            FileExchangeFolder = section[FileExchangeFolderKey],
            Window = new DesktopWindowOptions
            {
                Title = windowSection[WindowTitleKey] ?? defaults.Window.Title,
                Size = reader.Size(windowSection, WindowSizeKey) ?? defaults.Window.Size,
                Samples = reader.NonNegativeInt(windowSection, WindowSamplesKey, defaults.Window.Samples),
                VSync = reader.Bool(windowSection, WindowVSyncKey, defaults.Window.VSync),
            },
            Audio = audio ?? defaults.Audio,
        };

        return reader.Problems.Count > 0 ? new ResultProblemCollection(reader.Problems) : options;
    }

    /// <summary>
    /// Reads typed values from a section, collecting a problem for each invalid one.
    /// </summary>
    private sealed class SectionReader
    {
        public List<ResultProblem> Problems { get; } = [];

        public bool Bool(IConfigurationSection section, string key, bool fallback)
        {
            if (section[key] is not { } value)
            {
                return fallback;
            }

            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            AddProblem(section, key, value, "true or false");
            return fallback;
        }

        public TEnum Enum<TEnum>(IConfigurationSection section, string key, TEnum fallback) where TEnum : struct, Enum
        {
            if (section[key] is not { } value)
            {
                return fallback;
            }

            if (System.Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && System.Enum.IsDefined(parsed))
            {
                return parsed;
            }

            AddProblem(section, key, value, string.Join(" or ", System.Enum.GetNames<TEnum>()));
            return fallback;
        }

        public int NonNegativeInt(IConfigurationSection section, string key, int fallback)
        {
            if (section[key] is not { } value)
            {
                return fallback;
            }

            if (int.TryParse(value, out var parsed) && parsed >= 0)
            {
                return parsed;
            }

            AddProblem(section, key, value, "a whole number of 0 or more");
            return fallback;
        }

        /// <summary>A size written as <c>&lt;width&gt;x&lt;height&gt;</c>; null when not set.</summary>
        public Vector2<int>? Size(IConfigurationSection section, string key)
        {
            if (section[key] is not { } value)
            {
                return null;
            }

            var parts = value.Split('x');
            if (parts.Length == 2
                && int.TryParse(parts[0], out var width)
                && int.TryParse(parts[1], out var height)
                && width > 0
                && height > 0)
            {
                return new Vector2<int>(width, height);
            }

            AddProblem(section, key, value, "<width>x<height> (e.g. 1920x1080)");
            return null;
        }

        private void AddProblem(IConfigurationSection section, string key, string value, string expected) =>
            Problems.Add(new ResultProblem("Invalid {0}:{1} \"{2}\", expected {3}", section.Path, key, value, expected));
    }
}
