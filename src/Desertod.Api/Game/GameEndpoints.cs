using System.Security.Claims;
using Desertod.Api.Auth;

namespace Desertod.Api.Game;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var player = app.MapGroup("/api").RequireAuthorization();

        player.MapGet("/catalog", (CatalogDto catalog) => catalog);

        player.MapGet("/villages/public", (GameService game, CancellationToken cancellationToken) =>
            game.ListPublicVillagesAsync(cancellationToken));

        player.MapGet("/villages/{slug}", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.GetVillageAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapGet("/villages", (ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.ListVillagesAsync(user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages", (CreateVillageRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CreateVillageAsync(user.RequireAccountId(), request, cancellationToken));

        player.MapDelete("/villages/{slug}", async (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
        {
            await game.DeleteVillageAsync(slug, user.RequireAccountId(), cancellationToken);
            return Results.NoContent();
        });

        player.MapPost("/villages/{slug}/invite", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.NewInviteCodeAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapGet("/villages/{slug}/me", async (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            await game.GetCitizenAsync(slug, user.RequireAccountId(), cancellationToken) is { } citizen
                ? Results.Ok(citizen)
                : Results.NotFound());

        player.MapDelete("/villages/{slug}/me", async (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
        {
            await game.LeaveVillageAsync(slug, user.RequireAccountId(), cancellationToken);
            return Results.NoContent();
        });

        player.MapPost("/villages/{slug}/citizens", (string slug, JoinVillageRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.JoinVillageAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapPut("/villages/{slug}/citizens/{citizenId:guid}/subtitle", (string slug, Guid citizenId, SetSubtitleRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.SetSubtitleAsync(slug, user.RequireAccountId(), citizenId, request.Subtitle, cancellationToken));

        player.MapPut("/villages/{slug}/citizens/{citizenId:guid}/governor", (string slug, Guid citizenId, SetGovernorRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.SetGovernorAsync(slug, user.RequireAccountId(), citizenId, request.IsGovernor, cancellationToken));

        player.MapPost("/villages/{slug}/me/task", (string slug, StartTaskRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.StartTaskAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapDelete("/villages/{slug}/me/task", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelTaskAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/drop", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.DropLoadAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/drink", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.DrinkAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/heal", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.HealAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/undead", (string slug, CallUndeadRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CallUndeadAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapDelete("/villages/{slug}/undead", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelUndeadAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/dungeon", (string slug, OpenDungeonRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.OpenDungeonAsync(slug, user.RequireAccountId(), request.DungeonId, cancellationToken));

        player.MapDelete("/villages/{slug}/dungeon", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelDungeonAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapGet("/villages/{slug}/me/dungeon", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.GetHeroClassesAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/dungeon", (string slug, JoinDungeonRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.JoinDungeonAsync(slug, user.RequireAccountId(), request.Class, cancellationToken));

        player.MapDelete("/villages/{slug}/me/dungeon", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.LeaveDungeonAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/take", (string slug, TakeRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.TakeFromStorageAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapPost("/villages/{slug}/me/goto", (string slug, GoToRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.GoToAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapPost("/villages/{slug}/me/job", (string slug, StartJobRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.StartJobAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapPost("/villages/{slug}/me/research", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.StartResearchAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/me/forecast", (string slug, ForecastRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.ForecastAsync(slug, user.RequireAccountId(), request.Rain, cancellationToken));

        player.MapPost("/villages/{slug}/me/vote", (string slug, VoteRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.VoteAsync(slug, user.RequireAccountId(), request.TechnologyId, cancellationToken));

        player.MapPost("/villages/{slug}/poll", (string slug, OpenPollRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.OpenPollAsync(slug, user.RequireAccountId(), request.TechnologyIds, cancellationToken));

        player.MapDelete("/villages/{slug}/poll", (string slug, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelPollAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/tsar/goto", (string slug, GoToRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.TsarGoToAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapPost("/villages/{slug}/blueprints", (string slug, PlaceBlueprintRequest request, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.PlaceBlueprintAsync(slug, user.RequireAccountId(), request, cancellationToken));

        player.MapDelete("/villages/{slug}/blueprints/{plotId}", (string slug, string plotId, ClaimsPrincipal user, GameService game, CancellationToken cancellationToken) =>
            game.CancelBlueprintAsync(slug, user.RequireAccountId(), plotId, cancellationToken));

        player.MapGet("/villages/{slug}/chat", (string slug, ClaimsPrincipal user, ChatService chat, CancellationToken cancellationToken) =>
            chat.HistoryAsync(slug, user.RequireAccountId(), cancellationToken));

        player.MapPost("/villages/{slug}/chat", (string slug, SendChatRequest request, ClaimsPrincipal user, ChatService chat, CancellationToken cancellationToken) =>
                chat.SendAsync(slug, user.RequireAccountId(), request.Text, cancellationToken))
            .RequireRateLimiting(ChatService.RateLimit);
    }
}
