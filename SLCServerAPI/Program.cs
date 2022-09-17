using SteamWebAPI2.Utilities;
using SteamWebAPI2.Interfaces;
using Steam.Models.SteamCommunity;
using System.Text;
using Microsoft.AspNetCore.Cors.Infrastructure;
using System.Net;
using System.Text.Json;

namespace SLC;

internal readonly record struct GameWithOwners( uint Appid, List<ulong> Owners );
internal readonly record struct UserGameLibrary( PlayerSummaryModel Player, IEnumerable<OwnedGameModel> Library );
internal readonly record struct TestData( string? message, DateTime time );

/// <summary>
/// We are assuming the front-end app has ensured this is usable data as much as possible but we
/// obviously will do back-end checks here too.
/// </summary>
/// <param name="idList"></param>
internal record struct SteamUserCompareQuery( int userCount, ulong[] idList );

internal sealed class InvalidSteamAPIKeyException : Exception
{
	internal InvalidSteamAPIKeyException( string? message ) : base( message ) { }
}

public static class Program
{
	public const string ApiBaseUrl = "https://store.steampowered.com/api";
	public const string AppName = "steamlibcomparer";

	internal static HttpClient client = new();

	internal static SteamWebInterfaceFactory _steamInterfaceFactory;
	internal static SteamUser _steamUser;
	internal static PlayerService _playerService;
	internal static SteamStore _storeService;

	internal static bool _logDebug = false;
	internal static bool _verboseDebug = false;

	internal static void Log( object? info )
	{
		if ( !_logDebug ) return;
		if ( info == null ) return;
		Console.WriteLine( info );
	}

	public static void Main( string[] args )
	{
		// Parse ENV Vars to ints.
		_verboseDebug = int.TryParse( Environment.GetEnvironmentVariable( "verbose" ), out var verboseInt ) && verboseInt > 0;

		// Override _logDebug if verbose is on.
		_logDebug = _verboseDebug ? 
			_verboseDebug : int.TryParse( Environment.GetEnvironmentVariable( "log" ), out int logInt ) && logInt > 0;

		var apiKey = Environment.GetEnvironmentVariable( "STEAM_WEB_API_KEY" );
		if ( string.IsNullOrEmpty( apiKey ) )
		{
			throw new InvalidSteamAPIKeyException( "Steam Web API Key not set/found in env vars." );
		}

		// Initialize all the SteamWebAPI2 stuff.
		_steamInterfaceFactory = new SteamWebInterfaceFactory( apiKey );

		_steamUser = _steamInterfaceFactory.CreateSteamWebInterface<SteamUser>( new HttpClient() );
		_playerService = _steamInterfaceFactory.CreateSteamWebInterface<PlayerService>( new HttpClient() );
		_storeService = _steamInterfaceFactory.CreateSteamStoreInterface( new HttpClient() );

		var builder = WebApplication.CreateBuilder( args );

		// CORS...
		builder.Services.AddCors( o => o.AddPolicy( "MyPolicy", builder =>
		{
			builder.AllowAnyOrigin()
			.AllowAnyMethod()
			.AllowAnyHeader();
		} ) );

		var app = builder.Build();

		// Configure the HTTP request pipeline.
		if ( app.Environment.IsDevelopment() )
		{
		}

		app.UseHttpsRedirection();
		app.UseCors( "MyPolicy" );

		// POST endpoint for submitting the id list.
		app.MapPost( "/idlist", async ( context ) =>
		{
			Console.WriteLine( "POST received" );

			if ( !context.Request.HasJsonContentType() )
			{
				context.Response.StatusCode = (int)HttpStatusCode.UnsupportedMediaType;
				return;
			}

			var query = await context.Request.ReadFromJsonAsync<SteamUserCompareQuery>();

			if ( query.userCount < 0 )
			{
				context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
				return;
			}

			// TODO: We'll need to handle errors/NREs for failed respones.
			// I think steamwebapi2 doesn't do good null-handling...
			// Might need to surround with pragmas to prevent failure shutdown.

			// Fetch their profiles.
			var summariesResponse = await _steamUser.GetPlayerSummariesAsync( query.idList );
			var summaries = summariesResponse.Data;
			var users = new List<UserGameLibrary>();

			foreach ( var player in summaries )
			{
				// includeAppInfo: true to get stuff like the names of the games. Yes that isn't included by default.
				var response = await _playerService.GetOwnedGamesAsync( player.SteamId, includeAppInfo: true, includeFreeGames: false );
				users.Add( new UserGameLibrary( player, response.Data.OwnedGames ) );
			}

			IEnumerable<GameWithOwners> games = GetGamesInCommon( users );
			await context.Response.WriteAsJsonAsync<List<GameWithOwners>>( games.ToList() );
		} );

		// GET test.
		app.MapGet( "/testdata", () =>
		{
			Log( "GET received in /testdata" );
			var testData = new TestData( "Hello from SLC-Backend", DateTime.Now );
			return testData;
		} );

		app.Run();
	}

	internal static IEnumerable<GameWithOwners> GetGamesInCommon( IEnumerable<UserGameLibrary> users )
	{
		if ( users.Count() < 0 )
			throw new Exception( "UserGameLibrary list is empty." );

		var gamesAndOwners = new Dictionary<uint, List<ulong>>();
		foreach ( var user in users )
		{
			Log( $"Processing user --- SteamId: {user.Player.SteamId} NickName: {user.Player.Nickname}." );
			Log( $"Library size: {user.Library.Count()} games." );

			foreach ( var game in user.Library )
			{
				if ( gamesAndOwners.ContainsKey( game.AppId ) )
					gamesAndOwners[game.AppId].Add( user.Player.SteamId );
				else
					gamesAndOwners.Add( game.AppId, new List<ulong>() { user.Player.SteamId } );
			}
		}

		Log( $"Collective virtual library size (all users combined): {gamesAndOwners.Count()} games." );

		// Process the dictionary, make a list that only includes the games owned by multiple users.
		var processedOwners = new List<GameWithOwners>();
		foreach ( KeyValuePair<uint, List<ulong>> kvp in gamesAndOwners )
		{
			// The game does not have multiple owners.
			if ( kvp.Value.Count <= 1 )
				continue;

			processedOwners.Add( new GameWithOwners( kvp.Key, kvp.Value ) );
		}

		if ( _verboseDebug )
		{
			foreach ( var game in processedOwners )
			{
				Log( $"AppId: {game.Appid}" );
				foreach ( var value in game.Owners )
				{
					Log( $"Owned by: {value} " );
				}
			}
		}

		return processedOwners;
	}
}
