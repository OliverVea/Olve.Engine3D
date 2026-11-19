namespace Olve.Engine3D.Utilities;

public static class ResultExtensions
{
    /// <summary>On failure, runs <paramref name="undo"/> and returns both its problems and the original ones.</summary>
    public static Result IfProblemUndo(this Result result, Func<Result> undo)
    {
        if (!result.TryPickProblems(out var problems))
        {
            return result;
        }

        return undo().TryPickProblems(out var undoProblems)
            ? new ResultProblemCollection([.. problems, .. undoProblems])
            : problems;
    }

    /// <summary>On success, runs <paramref name="action"/> with the value; returns the result either way.</summary>
    public static Result<T> IfSucceeded<T>(this Result<T> result, Action<T> action)
    {
        if (!result.TryPickProblems(out _, out var value))
        {
            action(value);
        }

        return result;
    }
}
