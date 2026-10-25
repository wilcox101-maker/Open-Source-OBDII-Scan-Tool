using ObdLink.Core.Dashboard;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// Customize-mode reorder. The WPF drop target is hit-tested separately; these tests lock the
/// rules the view must call: nothing moves while Customize is off, a drop inserts at the target
/// slot, and Up/Down follows the wrap row rather than a single card's width.
/// </summary>
public class DashboardReorderTests
{
    private static DashboardLayout Sample()
    {
        var l = new DashboardLayout { Name = "T" };
        foreach (var k in new[] { "rpm", "speed", "gear", "coolant", "iat" })
            l.Items.Add(new DashItem { Key = k });
        return l;
    }

    [Fact]
    public void MoveDoesNothingWhileCustomizeIsOff()
    {
        var l = Sample();
        var before = l.Items.Select(i => i.Key).ToArray();
        Assert.Equal(-1, DashReorder.Move(l, 0, 1, customizing: false));
        Assert.Equal(before, l.Items.Select(i => i.Key));
        Assert.False(DashReorder.Drop(l, "rpm", "coolant", customizing: false));
        Assert.Equal(before, l.Items.Select(i => i.Key));
    }

    [Fact]
    public void DropInsertsAtTheTargetSlotAndUnknownKeysStayPut()
    {
        var l = Sample();
        Assert.True(DashReorder.Drop(l, "rpm", "coolant", customizing: true));
        Assert.Equal(new[] { "speed", "gear", "coolant", "rpm", "iat" }, l.Items.Select(i => i.Key));
        Assert.False(DashReorder.Drop(l, "rpm", "rpm", customizing: true));
        Assert.False(DashReorder.Drop(l, "missing", "rpm", customizing: true));
        Assert.False(DashReorder.Drop(l, "rpm", "missing", customizing: true));
        Assert.False(DashReorder.Drop(l, null, "rpm", customizing: true));
        Assert.Equal(new[] { "speed", "gear", "coolant", "rpm", "iat" }, l.Items.Select(i => i.Key));
    }

    [Fact]
    public void MoveByOnePlaceMatchesTheLeftAndRightButtons()
    {
        var l = Sample();
        Assert.Equal(1, DashReorder.Move(l, 0, 1, customizing: true));
        Assert.Equal("speed", l.Items[0].Key);
        Assert.Equal("rpm", l.Items[1].Key);
        Assert.Equal(-1, DashReorder.Move(l, 0, -1, customizing: true)); // already first
    }

    [Fact]
    public void VerticalDeltaFollowsTheWrapRow()
    {
        // three cards on the first row, two on the second
        DashReorder.Slot[] slots =
        {
            new(10, 10), new(220, 10), new(430, 10),
            new(10, 180), new(220, 180),
        };
        Assert.Equal(2, DashReorder.RowCount(slots));
        Assert.Equal(3, DashReorder.VerticalDelta(slots, 0, +1, unmeasuredStride: 9)); // straight down
        Assert.Equal(3, DashReorder.VerticalDelta(slots, 1, +1, unmeasuredStride: 9)); // x=220 -> second row x=220
        Assert.Equal(2, DashReorder.VerticalDelta(slots, 2, +1, unmeasuredStride: 9)); // x=430 -> nearest is x=220
        Assert.Equal(0, DashReorder.VerticalDelta(slots, 0, -1, unmeasuredStride: 9)); // already top
        Assert.Equal(-3, DashReorder.VerticalDelta(slots, 3, -1, unmeasuredStride: 9));
        Assert.Equal(0, DashReorder.VerticalDelta(slots, 4, +1, unmeasuredStride: 9)); // already bottom
    }

    [Fact]
    public void SingleRowUpDownStepsOnePlaceAndUnmeasuredUsesStride()
    {
        DashReorder.Slot[] row = { new(0, 5), new(100, 5), new(200, 8) }; // same row (8px bucket)
        Assert.Equal(1, DashReorder.RowCount(row));
        Assert.Equal(1, DashReorder.VerticalDelta(row, 0, +1, unmeasuredStride: 4));
        Assert.Equal(-1, DashReorder.VerticalDelta(row, 2, -1, unmeasuredStride: 4));

        DashReorder.Slot[] unmeasured = { new(0, 0), new(0, 0), new(0, 0) };
        Assert.False(DashReorder.IsMeasured(unmeasured));
        Assert.Equal(4, DashReorder.VerticalDelta(unmeasured, 0, +1, unmeasuredStride: 4));
        Assert.Equal(-1, DashReorder.NearestIndex(System.Array.Empty<DashReorder.Slot>(), 0, 0));
        Assert.Equal(1, DashReorder.NearestIndex(row, 90, 0));
    }
}
