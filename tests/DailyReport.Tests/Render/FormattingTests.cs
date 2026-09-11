using DailyReport.Core.Model;
using DailyReport.Core.Render;

namespace DailyReport.Tests.Render;

public sealed class FormattingTests
{
    [Test]
    public void Count_uses_invariant_thousands_separators()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Formatting.Count(1234), Is.EqualTo("1,234"));
            Assert.That(Formatting.Count(0), Is.EqualTo("0"));
            Assert.That(Formatting.Count(1_234_567), Is.EqualTo("1,234,567"));
        });
    }

    [Test]
    public void Count_of_null_is_no_data_never_zero()
    {
        Assert.That(Formatting.Count(null), Is.EqualTo("no data"));
    }

    [Test]
    public void Percent_has_one_decimal_place_and_a_percent_sign()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Formatting.Percent(12.5), Is.EqualTo("12.5%"));
            Assert.That(Formatting.Percent(0), Is.EqualTo("0.0%"));
            Assert.That(Formatting.Percent(100), Is.EqualTo("100.0%"));
        });
    }

    [Test]
    public void Percent_of_null_is_no_data_never_zero()
    {
        Assert.That(Formatting.Percent(null), Is.EqualTo("no data"));
    }

    [Test]
    public void Value_dispatches_on_unit()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Formatting.Value(1234, MetricUnit.Count), Is.EqualTo("1,234"));
            Assert.That(Formatting.Value(12.5, MetricUnit.Percent), Is.EqualTo("12.5%"));
            Assert.That(Formatting.Value(null, MetricUnit.Count), Is.EqualTo("no data"));
            Assert.That(Formatting.Value(null, MetricUnit.Percent), Is.EqualTo("no data"));
        });
    }

    [Test]
    public void Delta_of_null_is_an_en_dash()
    {
        Assert.That(Formatting.Delta(null, MetricUnit.Count), Is.EqualTo("–"));
    }

    [Test]
    public void Delta_of_exactly_zero_is_the_equals_glyph_regardless_of_percent()
    {
        Assert.That(Formatting.Delta(new Delta(0, 0), MetricUnit.Count), Is.EqualTo("= 0"));
    }

    [Test]
    public void Positive_count_delta_with_percent_uses_the_up_glyph_and_a_plus_sign()
    {
        Assert.That(Formatting.Delta(new Delta(12, 9.4), MetricUnit.Count), Is.EqualTo("▲ 12 (+9.4%)"));
    }

    [Test]
    public void Negative_count_delta_with_percent_uses_the_down_glyph_and_a_unicode_minus()
    {
        Assert.That(Formatting.Delta(new Delta(-3, -2.1), MetricUnit.Count), Is.EqualTo("▼ 3 (−2.1%)"));
    }

    [Test]
    public void Delta_with_no_percent_baseline_omits_the_parenthetical()
    {
        Assert.That(Formatting.Delta(new Delta(3, null), MetricUnit.Count), Is.EqualTo("▲ 3"));
    }

    [Test]
    public void Percent_unit_delta_renders_the_absolute_part_in_percentage_points()
    {
        Assert.That(Formatting.Delta(new Delta(2.1, null), MetricUnit.Percent), Is.EqualTo("▲ 2.1 pp"));
    }

    [Test]
    public void Percent_unit_delta_can_still_carry_a_relative_percent_change()
    {
        Assert.That(Formatting.Delta(new Delta(-2.0, -9.1), MetricUnit.Percent), Is.EqualTo("▼ 2.0 pp (−9.1%)"));
    }

    [Test]
    public void Count_delta_rounds_a_fractional_baseline_comparison_to_the_nearest_whole_number()
    {
        Assert.That(Formatting.Delta(new Delta(28.57, 40.2), MetricUnit.Count), Is.EqualTo("▲ 29 (+40.2%)"));
    }

    [Test]
    public void BaselineNote_flags_an_incomplete_seven_day_window()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Formatting.BaselineNote(3), Is.EqualTo("3/7 days"));
            Assert.That(Formatting.BaselineNote(0), Is.EqualTo("0/7 days"));
            Assert.That(Formatting.BaselineNote(7), Is.Null);
        });
    }

    [Test]
    public void DayName_is_invariant_and_english_regardless_of_host_culture()
    {
        Assert.That(Formatting.DayName(new DateOnly(2026, 9, 10)), Is.EqualTo("Thursday 10 September 2026"));
    }

    [Test]
    public void IsoDate_is_yyyy_MM_dd()
    {
        Assert.That(Formatting.IsoDate(new DateOnly(2026, 9, 11)), Is.EqualTo("2026-09-11"));
    }
}
