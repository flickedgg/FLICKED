using Flicked.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections;
using System.Numerics;
namespace Flicked.Api.Controllers;

[ApiController]
[Route("api/news")]

public class NewsController : ControllerBase
{
    private static readonly NewsPost[] Posts = [
     new("n6", "patch", "18 Sep", "0.2",
              "Alpha 0.2: parties and map vote",
              "Queue with up to four friends, and pick the map together after everyone accepts.",
              [
                  "Parties are here. Invite friends from the list on the Play screen, or share your party code so they can join directly. The leader queues for everyone.",
                  "Map veto is replaced by a map vote. After all ten players accept, everyone has 15 seconds to vote. The map with the most votes is played; a tie is settled at random.",
                  "Also in this release: the launcher starts faster, fonts ship with the app, and the window no longer flashes white on open.",
              ]),
          new ("n5", "update", "15 Sep", null,
              "Self-host FLICKED on a single machine",
              "A new guide walks through running the backend, database and one CS2 server on one box.",
              [
                  "You do not need a cluster to run FLICKED. The new guide in the repository shows how to run the backend, PostgreSQL, Redis and a CS2 dedicated server on a single machine.",
                  "It covers ports, the match config, and how to point the launcher at your own server.",
              ]),
          new ("n4", "event", "12 Sep", null,
              "Community Cup #1: sign-ups open",
              "Five-stack tournament, single elimination, played on community servers.",
              [
                  "Sign-ups for the first FLICKED Community Cup are open. Teams of five, single elimination, best of one until the final.",
                  "Matches run on community-hosted servers. Brackets are published the day before the first round.",
              ]),
          new ("n3", "patch", "08 Sep", "0.1.3",
              "Alpha 0.1.3: queue fixes",
              "Fixes a case where a declined match kept you in queue, plus smaller stability fixes.",
              [
                  "Declining a match now always returns you to the Play screen. Before, a declined match could leave you searching with no way to cancel.",
                  "Reconnecting to a live match is faster, and the server log keeps the full match history.",
              ]),
          new ("n2", "update", "03 Sep", null,
              "Every match now records a demo",
              "Demos are saved on the server and can be downloaded from the match page.",
              [
                  "Every match played on FLICKED now records a demo automatically. Demos are stored on the server that hosted the match.",
                  "Server owners can set how long demos are kept.",
              ]),
          new ("n1", "event", "29 Aug", null,
              "Weekly 5v5 night, Fridays at 20:00 CET",
              "A standing night to find full stacks. Queue times drop, games get better.",
              [
                  "Every Friday from 20:00 CET we play community 5v5s. More people in queue at the same time means shorter waits and closer matches.",
              ]),  
        
    ];

    [HttpGet]
    public IActionResult GetNewsPosts()
    {
        return Ok(Posts);
    }
}
