namespace Flicked.Api.Services;

/* Putting somebody else's text in our log.

   Most of what FLICKED logs is its own: ids, names it chose, numbers it worked
   out. A few things are not. A MatchZy event name arrives over the network from a
   community server, and so does whatever a CS2 console printed in reply to an
   RCON command. Those are the ones this is for.

   The log is plain text, one entry per line, read with journalctl (see
   docs/DEPLOY-LINUX.md). A newline in a logged value therefore does not appear as
   a newline in a value: it appears as the start of another entry, which anyone
   reading the log will take at face value. That matters most for the lines
   recording a server doing something it should not, because those lines are the
   only record of it and the person who would want them buried is the one
   supplying the text. */
public static class Logs
{
    /* One line, nothing hidden in it, and not endless.

       Control characters go rather than being escaped, because none of them
       belong in an event name or a console reply and a reader loses nothing by
       their absence. That covers more than line breaks: an ESC is a control
       character too, and a terminal takes what follows one as an instruction
       rather than as text.

       The cut keeps a kilobyte of padding, sent to push the interesting part of a
       line out of sight, from costing more than the rest of the entry. */
    public static string OneLine(string? value, int max = 64)
    {
        if (string.IsNullOrEmpty(value)) return "";

        var kept = value.Where(c => !char.IsControl(c)).ToArray();
        var clean = new string(kept).Trim();

        return clean.Length <= max ? clean : string.Concat(clean.AsSpan(0, max), "…");
    }
}
