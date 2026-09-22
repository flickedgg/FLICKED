namespace Flicked.Api.Models;

public enum FriendshipStatus
{
    Pending,
    Accepted,
    Blocked,   // reserved: nothing sets this yet
}

public class Friendship
{
    public int Id { get; set; }

    public int RequesterId { get; set; }
    public Player? Requester { get; set; }

    public int AddresseeId { get; set; }
    public Player? Addressee { get; set; }

    public FriendshipStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    public Player? Other(int playerId) => playerId == RequesterId ? Addressee : Requester;
    public int OtherId(int playerId) => playerId == RequesterId ? AddresseeId : RequesterId;
}
