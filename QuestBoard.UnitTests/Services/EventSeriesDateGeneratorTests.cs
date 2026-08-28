using QuestBoard.Domain.Services;

namespace QuestBoard.UnitTests.Services;

public class EventSeriesDateGeneratorTests
{
    [Fact]
    public void GenerateSlots_TwoOnTwoOffWeekly_ProducesExpectedDatesAndFiringSlots()
    {
        // Arrange
        var anchorDate = new DateOnly(2026, 9, 5);
        var mask = new List<bool> { true, true, false, false };

        // Act
        var slots = EventSeriesDateGenerator.GenerateSlots(anchorDate, intervalWeeks: 1, mask, endDate: null, maxSlots: 8).ToList();

        // Assert
        slots.Should().HaveCount(8);
        slots.Select(s => s.Date).Should().Equal(
            new DateOnly(2026, 9, 5),
            new DateOnly(2026, 9, 12),
            new DateOnly(2026, 9, 19),
            new DateOnly(2026, 9, 26),
            new DateOnly(2026, 10, 3),
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 17),
            new DateOnly(2026, 10, 24));

        var firingSlots = slots.Where(s => s.Fires).Select(s => s.SlotIndex).ToList();
        firingSlots.Should().Equal(0, 1, 4, 5);

        var nonFiringSlots = slots.Where(s => !s.Fires).Select(s => s.SlotIndex).ToList();
        nonFiringSlots.Should().Equal(2, 3, 6, 7);
    }

    [Fact]
    public void GenerateSlots_FortnightlySingleOnMask_EveryStepFiresAndDatesStepByFourteenDays()
    {
        // Arrange
        var anchorDate = new DateOnly(2026, 9, 5);
        var mask = new List<bool> { true };

        // Act
        var slots = EventSeriesDateGenerator.GenerateSlots(anchorDate, intervalWeeks: 2, mask, endDate: null, maxSlots: 5).ToList();

        // Assert
        slots.Should().OnlyContain(s => s.Fires);
        slots.Select(s => s.Date).Should().Equal(
            new DateOnly(2026, 9, 5),
            new DateOnly(2026, 9, 19),
            new DateOnly(2026, 10, 3),
            new DateOnly(2026, 10, 17),
            new DateOnly(2026, 10, 31));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GenerateSlots_AnyIntervalAndMask_EveryDateFallsOnAnchorWeekday(int intervalWeeks)
    {
        // Arrange
        var anchorDate = new DateOnly(2026, 9, 5);
        var mask = new List<bool> { true, false, true };

        // Act
        var slots = EventSeriesDateGenerator.GenerateSlots(anchorDate, intervalWeeks, mask, endDate: null, maxSlots: 30).ToList();

        // Assert
        slots.Should().OnlyContain(s => s.Date.DayOfWeek == anchorDate.DayOfWeek);
    }

    [Fact]
    public void GenerateSlots_MirroredMasksOnSameAnchorAndInterval_ShareNoFiringDate()
    {
        // Arrange
        var anchorDate = new DateOnly(2026, 9, 5);
        var maskA = new List<bool> { true, true, false, false };
        var maskB = new List<bool> { false, false, true, true };

        // Act
        var slotsA = EventSeriesDateGenerator.GenerateSlots(anchorDate, intervalWeeks: 1, maskA, endDate: null, maxSlots: 40).ToList();
        var slotsB = EventSeriesDateGenerator.GenerateSlots(anchorDate, intervalWeeks: 1, maskB, endDate: null, maxSlots: 40).ToList();

        // Assert
        var fullGridA = slotsA.Select(s => s.Date).ToList();
        var fullGridB = slotsB.Select(s => s.Date).ToList();
        fullGridA.Should().Equal(fullGridB);

        var firingDatesA = slotsA.Where(s => s.Fires).Select(s => s.Date).ToHashSet();
        var firingDatesB = slotsB.Where(s => s.Fires).Select(s => s.Date).ToHashSet();
        firingDatesA.Intersect(firingDatesB).Should().BeEmpty();
    }

    [Fact]
    public void GenerateSlots_EndDateSet_TruncatesSequenceWithNoDateAfterEndDate()
    {
        // Arrange
        var anchorDate = new DateOnly(2026, 9, 5);
        var mask = new List<bool> { true };
        var endDate = new DateOnly(2026, 9, 19);

        // Act
        var slots = EventSeriesDateGenerator.GenerateSlots(anchorDate, intervalWeeks: 1, mask, endDate, maxSlots: 100).ToList();

        // Assert
        slots.Should().OnlyContain(s => s.Date <= endDate);
        slots.Select(s => s.Date).Should().Equal(
            new DateOnly(2026, 9, 5),
            new DateOnly(2026, 9, 12),
            new DateOnly(2026, 9, 19));
    }

    [Fact]
    public void GenerateSlots_MaxSlotsLargerThanScanCeiling_YieldsAtMostMaxSlotScan()
    {
        // Arrange
        var anchorDate = new DateOnly(2026, 9, 5);
        var mask = new List<bool> { true };

        // Act
        var slots = EventSeriesDateGenerator.GenerateSlots(anchorDate, intervalWeeks: 1, mask, endDate: null, maxSlots: EventSeriesDateGenerator.MaxSlotScan + 500).ToList();

        // Assert
        slots.Should().HaveCount(EventSeriesDateGenerator.MaxSlotScan);
    }

    [Fact]
    public void GenerateSlots_IntervalWeeksZero_ThrowsArgumentOutOfRangeExceptionAtCallTime()
    {
        // Arrange
        var anchorDate = new DateOnly(2026, 9, 5);
        var mask = new List<bool> { true };

        // Act
        var act = () => EventSeriesDateGenerator.GenerateSlots(anchorDate, intervalWeeks: 0, mask, endDate: null, maxSlots: 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1,1,0,0")]
    [InlineData(" 1 , 0 ")]
    public void TryParseMask_ValidMasks_ReturnsTrueWithNoError(string mask)
    {
        // Act
        var result = EventSeriesDateGenerator.TryParseMask(mask, out var parsed, out var error);

        // Assert
        result.Should().BeTrue();
        error.Should().BeNull();
        parsed.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2")]
    [InlineData("1,x")]
    [InlineData("0,0,0")]
    public void TryParseMask_InvalidMasks_ReturnsFalseWithError(string? mask)
    {
        // Act
        var result = EventSeriesDateGenerator.TryParseMask(mask, out var parsed, out var error);

        // Assert
        result.Should().BeFalse();
        error.Should().NotBeNull();
        parsed.Should().BeEmpty();
    }

    [Fact]
    public void TryParseMask_TooManyPositions_ReturnsFalseWithError()
    {
        // Arrange
        var mask = string.Join(',', Enumerable.Repeat("1", 101));

        // Act
        var result = EventSeriesDateGenerator.TryParseMask(mask, out var parsed, out var error);

        // Assert
        result.Should().BeFalse();
        error.Should().NotBeNull();
        parsed.Should().BeEmpty();
    }

    [Fact]
    public void FormatMask_RoundTripsThroughParseMask()
    {
        // Act
        var result = EventSeriesDateGenerator.FormatMask(EventSeriesDateGenerator.ParseMask("1,1,0,0"));

        // Assert
        result.Should().Be("1,1,0,0");
    }

    [Fact]
    public void DateForSlot_MatchesGenerateSlotsArithmetic()
    {
        // Arrange
        var anchorDate = new DateOnly(2026, 9, 5);

        // Act
        var date = EventSeriesDateGenerator.DateForSlot(anchorDate, intervalWeeks: 2, slotIndex: 3);

        // Assert
        date.Should().Be(new DateOnly(2026, 10, 17));
    }
}
