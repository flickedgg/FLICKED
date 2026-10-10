using Flicked.Api.Services;

namespace Flicked.Api.Tests;

/* Text from somebody else, on its way into the log (see Services/Logs.cs).

   No database and no fixture: this is one function over a string, and the only
   thing worth proving is that nothing survives it that could pass for a second
   log entry or steer a terminal. */
public class LogsTests
{
    [Theory]
    // what a line break would do: the second half reads as its own entry
    [InlineData("going_live\nMatch 48213 finished 16-0", "going_liveMatch 48213 finished 16-0")]
    [InlineData("going_live\r\nwarn: forged", "going_livewarn: forged")]
    // tab and the rest go the same way, for the same reason
    [InlineData("map\tresult", "mapresult")]
    // an ESC is a control character, so a terminal never sees the sequence
    [InlineData("map_result\u001b[2K\u001b[A overwritten", "map_result[2K[A overwritten")]
    // ordinary values are left exactly as they are
    [InlineData("series_end", "series_end")]
    [InlineData("  going_live  ", "going_live")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Only_text_that_stays_on_one_line_comes_out(string? value, string expected) =>
        Assert.Equal(expected, Logs.OneLine(value));

    /* Padding is how you push the part of a line somebody does not want read off
       the end of it, so length is part of the problem and not only tidiness. */
    [Fact]
    public void A_long_value_is_cut_short()
    {
        var result = Logs.OneLine(new string('a', 500));

        Assert.Equal(65, result.Length);        // 64 kept, plus the mark that says so
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void How_much_is_kept_is_up_to_the_caller()
    {
        Assert.Equal("abcde", Logs.OneLine("abcde", 200));
        Assert.Equal("ab…", Logs.OneLine("abcde", 2));
    }
}
