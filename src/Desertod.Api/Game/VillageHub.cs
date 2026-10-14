using Desertod.Api.Auth;
using Desertod.Domain.Combat;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Desertod.Api.Game;

/// <summary>Read-only feed of a village for signed-in players.</summary>
[Authorize]
public sealed class VillageHub(ChatService chat, GameService game, ILogger<VillageHub> logger) : Hub
{
    public static string GroupName(string slug) => $"village:{slug.ToLowerInvariant()}";

    /// <summary>Chat lines go only to the Tsar and the citizens, unlike the rest of the feed.</summary>
    public static string ChatGroupName(string slug) => $"chat:{slug.ToLowerInvariant()}";

    public override Task OnConnectedAsync()
    {
        logger.HubConnected(Context.ConnectionId, Context.User?.GetAccountId());
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.HubDisconnected(exception, Context.ConnectionId);
        game.Disconnect(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>A citizen watching their own village is present while connected; see <see cref="CitizenPresence"/>.</summary>
    public async Task JoinVillage(string slug)
    {
        logger.HubJoined(Context.ConnectionId, slug);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(slug));
        if (Context.User?.GetAccountId() is { } accountId)
        {
            await game.ConnectAsync(slug, accountId, Context.ConnectionId, Context.ConnectionAborted);
        }
    }

    /// <summary>False when the player neither rules nor lives in the village; they get no chat lines.</summary>
    public async Task<bool> JoinChat(string slug)
    {
        if (Context.User?.GetAccountId() is not { } accountId
            || !await chat.IsMemberAsync(slug, accountId, Context.ConnectionAborted))
        {
            return false;
        }

        logger.HubJoinedChat(Context.ConnectionId, slug);
        await Groups.AddToGroupAsync(Context.ConnectionId, ChatGroupName(slug));
        return true;
    }

    public async Task LeaveVillage(string slug)
    {
        logger.HubLeft(Context.ConnectionId, slug);
        game.Disconnect(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(slug));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ChatGroupName(slug));
    }
}

/// <summary>Events of one village since the last batch, in the order they happened; only the latest summary per citizen.</summary>
/// <param name="Left">Citizens who left the village; none of them is in <paramref name="Citizens"/>.</param>
/// <param name="Away">Citizens who went away, see <see cref="CitizenPresence"/>; none of them is in <paramref name="Back"/>.</param>
/// <param name="Back">Citizens who came back.</param>
public sealed record VillageEventsDto(
    IReadOnlyList<CycleReport> Cycles,
    IReadOnlyList<ProjectCompletion> Projects,
    IReadOnlyList<QuestCompletion> Quests,
    IReadOnlyList<CitizenSummaryDto> Citizens,
    IReadOnlyList<RaidReport> Raids,
    IReadOnlyList<DungeonDto> Dungeons,
    IReadOnlyList<Guid> Left,
    IReadOnlyList<Guid> Away,
    IReadOnlyList<Guid> Back);

/// <summary>
/// Sends the village state at once, and queues the other events, which <see cref="VillageStatePublisher"/> sends
/// as one <c>villageEvents</c> message per village, so a busy village costs each viewer a few messages a second.
/// </summary>
public sealed class VillageNotifier(IHubContext<VillageHub> hub, VillageStateChanges changes)
{
    private readonly Dictionary<string, PendingEvents> pending = new(StringComparer.OrdinalIgnoreCase);

    public Task VillageUpdated(VillageStateDto state) =>
        hub.Clients.Group(VillageHub.GroupName(state.Slug)).SendAsync("villageUpdated", state);

    public Task VillageEvents(string slug, VillageEventsDto events, CancellationToken cancellationToken) =>
        hub.Clients.Group(VillageHub.GroupName(slug)).SendAsync("villageEvents", events, cancellationToken);

    public Task CitizenUpdated(string slug, CitizenSummaryDto citizen) =>
        Queue(slug, events =>
        {
            if (!events.Left.Contains(citizen.Id))
            {
                events.Citizens[citizen.Id] = citizen;
            }
        });

    public Task CitizenLeft(string slug, Guid citizenId) =>
        Queue(slug, events =>
        {
            events.Citizens.Remove(citizenId);
            events.Away.Remove(citizenId);
            events.Back.Remove(citizenId);
            events.Left.Add(citizenId);
        });

    public Task CitizenAway(string slug, Guid citizenId) =>
        Queue(slug, events =>
        {
            events.Back.Remove(citizenId);
            events.Away.Add(citizenId);
        });

    public Task CitizenBack(string slug, Guid citizenId) =>
        Queue(slug, events =>
        {
            events.Away.Remove(citizenId);
            events.Back.Add(citizenId);
        });

    /// <summary>Sent at once, so the viewers go back to the home page.</summary>
    public Task VillageDeleted(string slug) =>
        hub.Clients.Group(VillageHub.GroupName(slug)).SendAsync("villageDeleted");

    public Task CycleCompleted(string slug, CycleReport report) => Queue(slug, events => events.Cycles.Add(report));

    public Task ProjectCompleted(string slug, ProjectCompletion completion) =>
        Queue(slug, events => events.Projects.Add(completion));

    public Task QuestCompleted(string slug, QuestCompletion completion) => Queue(slug, events => events.Quests.Add(completion));

    public Task RaidFought(string slug, RaidReport report) => Queue(slug, events => events.Raids.Add(report));

    public Task DungeonEnded(string slug, DungeonDto run) => Queue(slug, events => events.Dungeons.Add(run));

    /// <summary>Sent at once: players expect their line to show up as they press Enter.</summary>
    public Task ChatMessageSent(string slug, ChatMessageDto message) =>
        hub.Clients.Group(VillageHub.ChatGroupName(slug)).SendAsync("chatMessage", message);

    /// <summary>Takes the queued events of all villages.</summary>
    public IReadOnlyList<(string Slug, VillageEventsDto Events)> TakeEvents()
    {
        lock (pending)
        {
            var taken = pending
                .Select(entry => (entry.Key, new VillageEventsDto(
                    entry.Value.Cycles,
                    entry.Value.Projects,
                    entry.Value.Quests,
                    [.. entry.Value.Citizens.Values],
                    entry.Value.Raids,
                    entry.Value.Dungeons,
                    [.. entry.Value.Left],
                    [.. entry.Value.Away],
                    [.. entry.Value.Back])))
                .ToList();
            pending.Clear();
            return taken;
        }
    }

    private Task Queue(string slug, Action<PendingEvents> add)
    {
        lock (pending)
        {
            if (!pending.TryGetValue(slug, out var events))
            {
                events = new PendingEvents();
                pending[slug] = events;
            }

            add(events);
        }

        changes.Wake();
        return Task.CompletedTask;
    }

    private sealed class PendingEvents
    {
        public List<CycleReport> Cycles { get; } = [];
        public List<ProjectCompletion> Projects { get; } = [];
        public List<QuestCompletion> Quests { get; } = [];
        public Dictionary<Guid, CitizenSummaryDto> Citizens { get; } = [];
        public List<RaidReport> Raids { get; } = [];
        public List<DungeonDto> Dungeons { get; } = [];
        public HashSet<Guid> Left { get; } = [];
        public HashSet<Guid> Away { get; } = [];
        public HashSet<Guid> Back { get; } = [];
    }
}
