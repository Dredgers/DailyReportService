using DailyReport.Worker.Hosting;

namespace DailyReport.Tests.Configuration;

public sealed class InvocationTests
{
    [Test]
    public void Once_with_dry_run_and_date()
    {
        var i = Invocation.Parse(["--once", "--dry-run", "--date=2026-09-10"]);
        Assert.That(i, Is.EqualTo(new Invocation(true, true, new DateOnly(2026, 9, 10))));
    }

    [Test]
    public void No_args_is_the_scheduler()
    {
        Assert.That(Invocation.Parse([]), Is.EqualTo(new Invocation(false, false, null)));
    }

    [Test]
    public void Dry_run_without_once_is_rejected()
    {
        Assert.That(() => Invocation.Parse(["--dry-run"]), Throws.ArgumentException);
    }
}
