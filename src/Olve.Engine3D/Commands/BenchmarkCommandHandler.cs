using System.Globalization;
using Olve.Engine3D.Diagnostics.Benchmarks;

namespace Olve.Engine3D.Commands;

public class BenchmarkCommandHandler(BenchmarkRecorder recorder) : ICommandHandler
{
    private static readonly CommandArgument OutputArgument = new("output", "path of the JSON report", true);
    private static readonly CommandArgument ScenarioArgument = new("scenario", "label for the report (default: benchmark)", false);
    private static readonly CommandArgument FramesArgument = new("frames", "frames measured (default: 1800)", false);
    private static readonly CommandArgument WarmupArgument = new("warmup", "frames run before measuring (default: 300)", false);
    private static readonly CommandArgument ExitArgument = new("exit", "close the game when done (true/false, default: true)", false);

    public string Verb => "benchmark";
    public string HelpString => "Measures frame times over a number of frames after a warm-up, writes a JSON report, then closes the game.";
    public IReadOnlyList<CommandArgument> Arguments { get; } = [OutputArgument, ScenarioArgument, FramesArgument, WarmupArgument, ExitArgument];

    public Result<CommandOutput> Handle(CommandContext context)
    {
        if (context.GetRequiredArgument(OutputArgument).TryPickProblems(out var problems, out var output)
            || GetCount(context, FramesArgument, 1800).TryPickProblems(out problems, out var frames)
            || GetCount(context, WarmupArgument, 300).TryPickProblems(out problems, out var warmup))
        {
            return problems;
        }

        var exit = context.GetOptionalArgument(ExitArgument) is not { } exitValue || !bool.TryParse(exitValue, out var parsed) || parsed;
        var scenario = context.GetOptionalArgument(ScenarioArgument) ?? "benchmark";

        if (recorder.Start(new BenchmarkSettings(scenario, warmup, frames, output, exit)).TryPickProblems(out problems))
        {
            return problems;
        }

        return new CommandOutput($"Benchmark '{scenario}' started: {warmup} warm-up frames, then {frames} measured frames");
    }

    private static Result<int> GetCount(CommandContext context, CommandArgument argument, int fallback)
    {
        if (context.GetOptionalArgument(argument) is not { } value)
        {
            return fallback;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 0
            ? count
            : new ResultProblem("Expected a whole number of 0 or more for '{0}', got '{1}'", argument.Key, value);
    }
}
