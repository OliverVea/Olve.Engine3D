using Olve.Engine3D.Utilities;
using Olve.Results;
using Olve.Results.TUnit;

namespace Olve.Engine3D.Tests.Utilities;

public class ResultExtensionsTests
{
    [Test]
    public async Task IfSucceeded_runs_the_action_with_the_value_on_success()
    {
        int? seen = null;

        var value = await Result<int>.Success(7).IfSucceeded(v => seen = v).AssertSuccessAndGetAsync();

        await Assert.That(seen).IsEqualTo(7);
        await Assert.That(value).IsEqualTo(7);
    }

    [Test]
    public async Task IfSucceeded_skips_the_action_and_keeps_the_problems_on_failure()
    {
        var problem = new ResultProblem("Broken");
        Result<int> failure = problem;
        var ran = false;

        var result = failure.IfSucceeded(_ => ran = true);

        await Assert.That(result).Failed();
        await Assert.That(ran).IsFalse();
        _ = result.TryPickProblems(out var problems, out _);
        await Assert.That(problems!.Single()).IsSameReferenceAs(problem);
    }
}
