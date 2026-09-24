using Flicked.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Flicked.Api.Controllers;

/* Parties.

     POST   /api/party                          make one, which inviting does anyway
     GET    /api/party                          your party and the invites waiting for you
     POST   /api/party/invite/{playerId}        leader only, and only a friend
     POST   /api/party/invites/{partyId}/accept
     DELETE /api/party/invites/{partyId}        decline one sent to you, or, with
                                                ?playerId=, cancel one you sent
     DELETE /api/party/members/{playerId}       leave, or kick if you lead

   The rules are in Services/Parties.cs; this turns them into status codes. Every
   one of them is checked here rather than trusted to the launcher: hiding a
   button is a courtesy to the person pressing it, not a control.

   The launcher does not poll this. Party state rides along with the friends list
   on /api/social/state, because they are drawn on the same screen and fetching
   them at two different instants is how a panel disagrees with itself. */
[ApiController]
[Route("api/party")]
public class PartyController(CurrentPlayer current, Parties parties, Social social) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        return Ok(await social.PartyAsync(me.Id, ct));
    }

    /* Making a party of one on purpose. Rarely needed - inviting somebody makes
       one on the way past - but the launcher can ask for the shape it is about
       to fill rather than inferring it. */
    [HttpPost]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        await parties.EnsureForAsync(me.Id, ct);
        return Ok(await social.PartyAsync(me.Id, ct));
    }

    [HttpPost("invite/{playerId:int}")]
    public async Task<IActionResult> Invite(int playerId, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        return Answer(await parties.InviteAsync(me.Id, playerId, ct));
    }

    [HttpPost("invites/{partyId:int}/accept")]
    public async Task<IActionResult> Accept(int partyId, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        var problem = await parties.AcceptInviteAsync(me.Id, partyId, ct);
        return problem == Parties.Problem.None
            ? Ok(await social.PartyAsync(me.Id, ct))
            : Answer(problem);
    }

    /* One route for both directions, because it is one row either way: without
       a playerId it is the invite sent to you, and with one it is the invite
       your party sent them, which only its leader may take back. */
    [HttpDelete("invites/{partyId:int}")]
    public async Task<IActionResult> RemoveInvite(int partyId, [FromQuery] int? playerId, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        return Answer(await parties.RemoveInviteAsync(me.Id, partyId, playerId, ct));
    }

    [HttpDelete("members/{playerId:int}")]
    public async Task<IActionResult> RemoveMember(int playerId, CancellationToken ct)
    {
        var me = await current.GetAsync(ct);
        if (me is null) return Unauthorized();

        return Answer(await parties.RemoveMemberAsync(me.Id, playerId, ct));
    }

    /* The refusals, in the words the launcher shows. StatusCode(403) rather than
       Forbid(): this app registers no authentication scheme, and Forbid() asks
       for a challenge it cannot produce. */
    private IActionResult Answer(Parties.Problem problem) => problem switch
    {
        Parties.Problem.None => NoContent(),
        Parties.Problem.NotLeader => StatusCode(StatusCodes.Status403Forbidden,
            "Only the party leader can do that."),
        Parties.Problem.NotFriends => StatusCode(StatusCodes.Status403Forbidden,
            "You can only invite friends."),
        Parties.Problem.AlreadyIn => Conflict("They are already in a party."),
        Parties.Problem.PartyFull => Conflict($"A party holds {Parties.MaxMembers} players."),
        Parties.Problem.TooManyInvites => Conflict($"Only {Parties.MaxInvites} invites can be out at once."),
        Parties.Problem.NoInvite => NotFound("That invite is gone."),
        Parties.Problem.NotInParty => NotFound("They are not in your party."),
        _ => StatusCode(StatusCodes.Status500InternalServerError),
    };
}
