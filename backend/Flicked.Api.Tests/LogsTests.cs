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
    [InlineData("going_live\rforged", "going_liveforged")]
    // tab and the rest of the control characters go the same way
    [InlineData("map\tresult", "mapresult")]
    // ordinary values are left exactly as they are
    [InlineData("series_end", "series_end")]
    [InlineData("  going_live  ", "going_live")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Only_text_that_stays_on_one_line_comes_out(string? value, string expected) =>
        Assert.Equal(expected, Logs.OneLine(value));

    /* The characters that end a line without being a carriage return or a line
       feed, by code point rather than as escapes, because two of them end a line
       in C# source as well and cannot be written into the literal below.

       That is the whole point of them. A filter on char.IsControl waves the
       separators through, since Zl and Zp are not control categories, and
       something displaying the log then breaks the line anyway. */
    [Theory]
    [InlineData(0x2028)]   // line separator
    [InlineData(0x2029)]   // paragraph separator
    [InlineData(0x0085)]   // next line
    [InlineData(0x000c)]   // form feed
    [InlineData(0x000b)]   // vertical tab
    public void Nothing_that_ends_a_line_survives(int codePoint) =>
        Assert.Equal("going_liveforged", Logs.OneLine($"going_live{(char)codePoint}forged"));

    /// An ESC goes, so a terminal reading the log never sees the sequence.
    [Fact]
    public void An_escape_sequence_is_left_as_plain_text()
    {
        var esc = (char)0x1b;

        Assert.Equal("map_result[2K[A overwritten",
            Logs.OneLine($"map_result{esc}[2K{esc}[A overwritten"));
    }

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
