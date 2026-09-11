using DailyReport.Worker.Hosting;

namespace DailyReport.Tests.Configuration;

public sealed class InvocationTests
{
    [Test]
    public void Once_with_dry_run_date_and_force()
    {
        var i = Invocation.Parse(["--once", "--dry-run", "--date=2026-09-10", "--force"]);
        Assert.That(i, Is.EqualTo(new Invocation(true, true, new DateOnly(2026, 9, 10), true, false)));
    }

    [Test]
    public void No_args_is_the_scheduler()
    {
        Assert.That(Invocation.Parse([]), Is.EqualTo(new Invocation(false, false, null, false, false)));
    }

    [Test]
    public void Healthcheck_stands_alone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Invocation.Parse(["--healthcheck"]).HealthCheck, Is.True);
            Assert.That(() => Invocation.Parse(["--healthcheck", "--once"]), Throws.ArgumentException);
        });
    }

    [Test]
    public void Unknown_or_misspelt_arguments_are_rejected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => Invocation.Parse(["--once", "--dryrun"]), Throws.ArgumentException.With.Message.Contains("--dryrun"));
            Assert.That(() => Invocation.Parse(["--once", "--date", "2026-09-01"]), Throws.ArgumentException);
            Assert.That(() => Invocation.Parse(["--help"]), Throws.ArgumentException);
        });
    }

    [Test]
    public void Dry_run_without_once_is_rejected()
    {
        Assert.That(() => Invocation.Parse(["--dry-run"]), Throws.ArgumentException);
    }
}
