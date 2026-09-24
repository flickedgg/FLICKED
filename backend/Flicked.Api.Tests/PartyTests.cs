using Flicked.Api.Data;
using Flicked.Api.Models;
using Flicked.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Flicked.Api.Tests;

/* What a party is allowed to become, against a real database.

   Most of what can go wrong here is a race or a constraint, which is exactly
   what an in-memory provider cannot tell the truth about: the unique index that
   keeps a player out of two parties does not exist there, so a test of it would
   pass whatever we wrote. */
[Collection("pool")]
public class PartyTests(PoolFixture fixture)
{
    private static Parties NewParties(FlickedDbContext db) => new(db);

    /* Two friends invite the same person, and they answer both at the same
       instant. One of the answers has to lose, and losing must leave them in one
       party rather than in two or in none. */
    [Fact]
    public async Task Accepting_two_invites_at_once_ends_in_one_party()
    {
        await fixture.ResetMatchmakingAsync();
        var players = await fixture.AddPlayersAsync(3, rating: 1500);
        var (first, second, invitee) = (players[0], players[1], players[2]);

        await fixture.BefriendAsync(first, invitee);
        await fixture.BefriendAsync(second, invitee);
        await fixture.AddPartyAsync([invitee]);   // they were playing alone until now

        await using (var db = fixture.NewContext())
            Assert.Equal(Parties.Problem.None, await NewParties(db).InviteAsync(first.Id, invitee.Id));
        await using (var db = fixture.NewContext())
            Assert.Equal(Parties.Problem.None, await NewParties(db).InviteAsync(second.Id, invitee.Id));

        var partyIds = await PartyIdsAsync(first.Id, second.Id);

        /* Two connections, because that is the point: one DbContext would
           serialise them and the test would prove nothing. */
        await using var one = fixture.NewContext();
        await using var two = fixture.NewContext();
        var answers = await Task.WhenAll(
            NewParties(one).AcceptInviteAsync(invitee.Id, partyIds[first.Id]),
            NewParties(two).AcceptInviteAsync(invitee.Id, partyIds[second.Id]));

        await using var check = fixture.NewContext();
        Assert.Equal(1, await check.PartyMembers.CountAsync(m => m.PlayerId == invitee.Id));
        Assert.Contains(Parties.Problem.None, answers);
    }

    /* A party that loses a member is not the party that joined the queue: it has
       fewer seats and a different rating, so it goes back to the Play screen
       rather than being matched on what it used to be. */
    [Fact]
    public async Task A_member_leaving_a_queued_party_takes_it_out_of_the_queue()
    {
        await fixture.ResetMatchmakingAsync();
        var players = await fixture.AddPlayersAsync(2, rating: 1500);
        var party = await fixture.QueuePartyAsync(players);

        await using var db = fixture.NewContext();
        Assert.Equal(Parties.Problem.None, await NewParties(db).RemoveMemberAsync(players[1].Id, players[1].Id));

        await using var check = fixture.NewContext();
        Assert.Empty(await check.Queue.ToListAsync());
        Assert.Equal(1, await check.PartyMembers.CountAsync(m => m.PartyId == party.Id));
    }

    /// Somebody has to be able to queue, so the longest-standing member takes over.
    [Fact]
    public async Task The_longest_standing_member_takes_over_when_the_leader_leaves()
    {
        await fixture.ResetMatchmakingAsync();
        var players = await fixture.AddPlayersAsync(3, rating: 1500);
        var party = await fixture.AddPartyAsync(players);

        await using var db = fixture.NewContext();
        Assert.Equal(Parties.Problem.None, await NewParties(db).RemoveMemberAsync(players[0].Id, players[0].Id));

        await using var check = fixture.NewContext();
        Assert.Equal(players[1].Id, (await check.Parties.SingleAsync(p => p.Id == party.Id)).LeaderId);
    }

    [Fact]
    public async Task The_last_member_leaving_deletes_the_party()
    {
        await fixture.ResetMatchmakingAsync();
        var players = await fixture.AddPlayersAsync(1, rating: 1500);
        var party = await fixture.AddPartyAsync(players);

        await using var db = fixture.NewContext();
        await NewParties(db).RemoveMemberAsync(players[0].Id, players[0].Id);

        await using var check = fixture.NewContext();
        Assert.False(await check.Parties.AnyAsync(p => p.Id == party.Id));
    }

    /* Without this rule, any signed-in account can throw invites at every player
       id it can guess. The launcher only offers the button on a friend, which is
       why the server has to check it too. */
    [Fact]
    public async Task Only_friends_can_be_invited()
    {
        await fixture.ResetMatchmakingAsync();
        var players = await fixture.AddPlayersAsync(2, rating: 1500);

        await using var db = fixture.NewContext();
        Assert.Equal(Parties.Problem.NotFriends,
            await NewParties(db).InviteAsync(players[0].Id, players[1].Id));
    }

    [Fact]
    public async Task Only_the_leader_invites_and_kicks()
    {
        await fixture.ResetMatchmakingAsync();
        var players = await fixture.AddPlayersAsync(3, rating: 1500);
        await fixture.AddPartyAsync([players[0], players[1]]);
        await fixture.BefriendAsync(players[1], players[2]);

        await using var db = fixture.NewContext();
        var parties = NewParties(db);

        Assert.Equal(Parties.Problem.NotLeader, await parties.InviteAsync(players[1].Id, players[2].Id));
        Assert.Equal(Parties.Problem.NotLeader, await parties.RemoveMemberAsync(players[1].Id, players[0].Id));
    }

    /* The cap is enforced when the invite is answered rather than when it is
       sent, because the party can fill in between: both of these invites were
       legal when they went out. */
    [Fact]
    public async Task A_party_fills_up_and_the_last_invite_is_refused()
    {
        await fixture.ResetMatchmakingAsync();
        var members = await fixture.AddPlayersAsync(4, rating: 1500);
        var guests = await fixture.AddPlayersAsync(2, rating: 1500);
        var party = await fixture.AddPartyAsync(members);

        foreach (var guest in guests)
        {
            await fixture.BefriendAsync(members[0], guest);
            await using var db = fixture.NewContext();
            Assert.Equal(Parties.Problem.None, await NewParties(db).InviteAsync(members[0].Id, guest.Id));
        }

        await using (var db = fixture.NewContext())
            Assert.Equal(Parties.Problem.None, await NewParties(db).AcceptInviteAsync(guests[0].Id, party.Id));

        await using (var db = fixture.NewContext())
            Assert.Equal(Parties.Problem.PartyFull, await NewParties(db).AcceptInviteAsync(guests[1].Id, party.Id));

        await using var check = fixture.NewContext();
        Assert.Equal(Parties.MaxMembers, await check.PartyMembers.CountAsync(m => m.PartyId == party.Id));
    }

    /* Every invite is a row somebody else's launcher reads on every poll, so an
       unbounded list is a way to make their client expensive. */
    [Fact]
    public async Task Invites_are_capped()
    {
        await fixture.ResetMatchmakingAsync();
        var leader = (await fixture.AddPlayersAsync(1, rating: 1500))[0];
        var friends = await fixture.AddPlayersAsync(Parties.MaxInvites + 1, rating: 1500);

        foreach (var friend in friends) await fixture.BefriendAsync(leader, friend);

        for (var i = 0; i < Parties.MaxInvites; i++)
        {
            await using var db = fixture.NewContext();
            Assert.Equal(Parties.Problem.None, await NewParties(db).InviteAsync(leader.Id, friends[i].Id));
        }

        await using var last = fixture.NewContext();
        Assert.Equal(Parties.Problem.TooManyInvites,
            await NewParties(last).InviteAsync(leader.Id, friends[^1].Id));
    }

    /// Asking somebody again refreshes their invite instead of making a second one.
    [Fact]
    public async Task Inviting_the_same_player_twice_leaves_one_invite()
    {
        await fixture.ResetMatchmakingAsync();
        var players = await fixture.AddPlayersAsync(2, rating: 1500);
        await fixture.BefriendAsync(players[0], players[1]);

        await using (var db = fixture.NewContext())
            await NewParties(db).InviteAsync(players[0].Id, players[1].Id);
        await using (var db = fixture.NewContext())
            Assert.Equal(Parties.Problem.None, await NewParties(db).InviteAsync(players[0].Id, players[1].Id));

        await using var check = fixture.NewContext();
        Assert.Equal(1, await check.PartyInvites.CountAsync(i => i.ToPlayerId == players[1].Id));
    }

    /// An invite past its time is not an offer any more, swept or not.
    [Fact]
    public async Task An_expired_invite_cannot_be_accepted()
    {
        await fixture.ResetMatchmakingAsync();
        var players = await fixture.AddPlayersAsync(2, rating: 1500);
        await fixture.BefriendAsync(players[0], players[1]);

        await using (var db = fixture.NewContext())
            await NewParties(db).InviteAsync(players[0].Id, players[1].Id);

        await using (var age = fixture.NewContext())
        {
            var invite = await age.PartyInvites.SingleAsync(i => i.ToPlayerId == players[1].Id);
            invite.ExpiresAt = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(1);
            await age.SaveChangesAsync();
        }

        await using var db2 = fixture.NewContext();
        var parties = NewParties(db2);
        var partyId = (await PartyIdsAsync(players[0].Id))[players[0].Id];

        Assert.Equal(Parties.Problem.NoInvite, await parties.AcceptInviteAsync(players[1].Id, partyId));
        Assert.Equal(1, await parties.SweepExpiredInvitesAsync());
    }

    /// The party each of these players leads, by player id.
    private async Task<Dictionary<int, int>> PartyIdsAsync(params int[] playerIds)
    {
        await using var db = fixture.NewContext();
        return await db.PartyMembers
            .Where(m => playerIds.Contains(m.PlayerId))
            .ToDictionaryAsync(m => m.PlayerId, m => m.PartyId);
    }
}
