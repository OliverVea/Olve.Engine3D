using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Commands;

public class ScreenshotCommandHandler(ScreenshotManager screenshotManager, ILogger<ScreenshotCommandHandler> logger) : ICommandHandler
{
    private const string PathKey = "path";
    private const string TargetKey = "target";
    private const string DebugKey = "debug";

    public string Verb => "screenshot";
    public string HelpString => "Captures a screenshot and saves it to the specified path. Use target= to capture a named framebuffer (e.g. shadow-map). Use debug=true to show debug overlays.";
    public IReadOnlyList<CommandArgument> Arguments { get; } =
    [
        new(PathKey, "output file path for the screenshot", true),
        new(TargetKey, "named framebuffer target (omit for screen)", false),
        new(DebugKey, "enable debug overlay (true/false)", false),
    ];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (!context.Arguments.TryGetValue(PathKey, out var path) || string.IsNullOrWhiteSpace(path))
        {
            return new ResultProblem("path argument is required");
        }

        context.Arguments.TryGetValue(TargetKey, out var target);
        var targetName = target ?? ScreenshotManager.ScreenTargetName;

        if (!screenshotManager.TargetNames.Contains(targetName))
        {
            return new ResultProblem("Unknown screenshot target '{0}'. Available: {1}", targetName, string.Join(", ", screenshotManager.TargetNames));
        }

        var debug = context.Arguments.TryGetValue(DebugKey, out var debugValue)
                    && string.Equals(debugValue, "true", StringComparison.OrdinalIgnoreCase);

        screenshotManager.RequestScreenshot(path, target, debug);
        logger.LogInformation("Screenshot requested: {Path} (target={Target}, debug={Debug})", path, targetName, debug);

        return new CommandOutput($"Screenshot queued: {path} (target={targetName}, debug={debug})");
    }
}
