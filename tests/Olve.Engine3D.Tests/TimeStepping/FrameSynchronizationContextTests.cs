using Olve.Engine3D.TimeStepping;

namespace Olve.Engine3D.Tests.TimeStepping;

public class FrameSynchronizationContextTests
{
    [Test]
    public async Task Posted_Work_Runs_Only_At_The_Frame_Point()
    {
        var context = new FrameSynchronizationContext();
        var ran = false;

        context.Post(_ => ran = true, null);
        await Assert.That(ran).IsFalse();

        context.RunPosted();
        await Assert.That(ran).IsTrue();
    }

    [Test]
    public async Task Work_Posted_While_Running_Waits_For_The_Next_Frame()
    {
        var context = new FrameSynchronizationContext();
        List<string> ran = [];

        context.Post(_ =>
        {
            ran.Add("first");
            context.Post(_ => ran.Add("second"), null);
        }, null);

        context.RunPosted();
        await Assert.That(ran).IsEquivalentTo(["first"]);

        context.RunPosted();
        await Assert.That(ran).IsEquivalentTo(["first", "second"]);
    }

    [Test]
    public async Task A_Host_Woken_By_A_Post_Finds_The_Work_Queued()
    {
        var context = new FrameSynchronizationContext();
        var ran = false;
        context.OnPosted = context.RunPosted;

        context.Post(_ => ran = true, null);

        await Assert.That(ran).IsTrue();
    }

    [Test]
    public async Task A_Throwing_Callback_Is_Reported_And_The_Rest_Still_Run()
    {
        var context = new FrameSynchronizationContext();
        Exception? reported = null;
        var ran = false;
        context.OnCallbackException = exception => reported = exception;

        context.Post(_ => throw new InvalidOperationException("boom"), null);
        context.Post(_ => ran = true, null);
        context.RunPosted();

        await Assert.That(reported).IsTypeOf<InvalidOperationException>();
        await Assert.That(ran).IsTrue();
    }

    [Test]
    public async Task An_Await_Resumes_On_The_Main_Thread_At_The_Frame_Point()
    {
        int? mainThread = null, resumedOn = null;
        var resumedBeforeTheWorkFinished = false;
        using var backgroundWorkMayFinish = new ManualResetEventSlim();

        // A thread of its own plays the main thread, so the test runner's context isn't replaced
        var mainLoop = new Thread(() =>
        {
            var context = new FrameSynchronizationContext();
            context.Install();
            mainThread = Environment.CurrentManagedThreadId;

            var work = ResumeAfterBackgroundWork();
            context.RunPosted();
            resumedBeforeTheWorkFinished = work.IsCompleted;

            backgroundWorkMayFinish.Set();
            while (!work.IsCompleted)
            {
                context.RunPosted();
                Thread.Yield();
            }

            async Task ResumeAfterBackgroundWork()
            {
                await Task.Run(backgroundWorkMayFinish.Wait);
                resumedOn = Environment.CurrentManagedThreadId;
            }
        });

        mainLoop.Start();
        mainLoop.Join();

        await Assert.That(resumedBeforeTheWorkFinished).IsFalse();
        await Assert.That(resumedOn).IsEqualTo(mainThread);
    }
}
