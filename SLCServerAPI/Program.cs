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

/// <summary>
/// We are assuming the front-end app has ensured this is usable data as much as possible but we
/// obviously will do back-end checks here too.
/// </summary>
/// <param name="idList"></param>
internal record struct SteamUserCompareQuery( int userCount, ulong[] idList );

public static class Program
{
	internal const string ApiKey = "B31BA9A9C29FC66CD0174F8BC2A176D8";
	public const string ApiBaseUrl = "https://store.steampowered.com/api";
	public const string AppName = "steamlibcomparer";

	internal static HttpClient client = new();

	internal static SteamWebInterfaceFactory interfaceFactory;
	internal static SteamUser steamInterface;
	internal static PlayerService playerService;
	internal static SteamStore storeService;
	internal static bool debug = true;

	internal static void Log( object info )
	{
		if ( !debug ) return;
		Console.WriteLine( info );
	}

	public static void Main( string[] args )
	{
		// Initialize all the SteamWebAPI2 stuff.
		interfaceFactory = new SteamWebInterfaceFactory( ApiKey );

		steamInterface = interfaceFactory.CreateSteamWebInterface<SteamUser>( new HttpClient() );
		playerService = interfaceFactory.CreateSteamWebInterface<PlayerService>( new HttpClient() );
		storeService = interfaceFactory.CreateSteamStoreInterface( new HttpClient() );

		var builder = WebApplication.CreateBuilder( args );

		// Add services to the container.
		// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

		// CORS...
		builder.Services.AddCors( o => o.AddPolicy( "MyPolicy", builder =>
		{
			builder.AllowAnyOrigin()
			.AllowAnyMethod()
			.AllowAnyHeader();
		} ) );

		//builder.Services.AddEndpointsApiExplorer();
		//builder.Services.AddSwaggerGen();

		var app = builder.Build();

		// Configure the HTTP request pipeline.
		if ( app.Environment.IsDevelopment() )
		{
		}

		app.UseHttpsRedirection();
		app.UseCors( "MyPolicy" );


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

			IEnumerable<GameWithOwners> games = await DoGroupLookup( query );
			await context.Response.WriteAsJsonAsync<List<GameWithOwners>>( games.ToList() );

		} );

		app.MapGet( "/testdata", () =>
		{
			Console.WriteLine( "request received" );
			var testData = "hello world!!";
			return testData;
		} );

		app.Run();
	}

	internal static async Task<IEnumerable<GameWithOwners>> DoGroupLookup( SteamUserCompareQuery query )
	{
		// Fetch their profiles.
		var summariesResponse = await steamInterface.GetPlayerSummariesAsync( query.idList );

		var summaries = summariesResponse.Data;

		var users = new List<UserGameLibrary>();
		foreach ( var player in summaries )
		{
			// includeAppInfo: true to get stuff like the names of the games. Yes that isn't included by default.
			// We might have to also just make another service api request
			// for each game's app id to fetch info for showing a pretty display for the front-end.
			// Maybe we can do all that on the front-end, just send app ids from here.
			// E.g. displaying the game logo/store page thumbnail.

			var response = await playerService.GetOwnedGamesAsync( player.SteamId, includeAppInfo: true, includeFreeGames: false );
			users.Add( new UserGameLibrary( player, response.Data.OwnedGames ) );
		}

		var gamesInCommon = GetGamesInCommon( users );

		foreach ( var game in gamesInCommon )
		{
			Log( $"AppId: {game.Appid}" );
			foreach ( var value in game.Owners )
			{
				Log( $"owned by: {value} " );
			}
		}

		return gamesInCommon;

		// Ok so steam has that problem where it asks you for your DOB for some games.
		// (I think its a European law thing?)
		// The problem is, SteamWebAPI2 just fucking shits the bed if you ask for store details
		// about an app that has this age verification check. It SHOULD just handle the return,
		// (the Steam API does still at least return valid json even with the DOB failure).

		//var namesGamesInCommon = new List<string>( gamesInCommon.Count() );
		//foreach ( var game in gamesInCommon )
		//{
		//	var response = await storeService.GetStoreAppDetailsAsync( game.Appid );
		//	if ( response == null )
		//	{
		//		Log( "Error!" );
		//		return null;
		//	}

		//	namesGamesInCommon.Add( response.Name );
		//}

	}

	internal static IEnumerable<GameWithOwners> GetGamesInCommon( IEnumerable<UserGameLibrary> users )
	{
		// TODO: Error handle, nullable return.
		// Web-related stuff always makes me wish C# had multiple returns like Go, for easier error handling.

		var gameOwnerDict = new Dictionary<uint, List<ulong>>();
		foreach ( var user in users )
		{
			Log( $"Processing user --- SteamId: {user.Player.SteamId} NickName: {user.Player.Nickname}." );
			Log( $"Library size: {user.Library.Count()} games." );

			foreach ( var game in user.Library )
			{
				if ( gameOwnerDict.ContainsKey( game.AppId ) )
					gameOwnerDict[game.AppId].Add( user.Player.SteamId );
				else
					gameOwnerDict.Add( game.AppId, new List<ulong>() { user.Player.SteamId } );
			}
		}

		Log( $"Collective virtual library size (all users combined): {gameOwnerDict.Count()} games." );

		// Process the dictionary, make a list that only includes the games owned by multiple users.
		var processedOwners = new List<GameWithOwners>();
		foreach ( KeyValuePair<uint, List<ulong>> kvp in gameOwnerDict )
		{
			// The game does not have multiple owners.
			if ( kvp.Value.Count <= 1 )
				continue;

			processedOwners.Add( new GameWithOwners( kvp.Key, kvp.Value ) );
		}

		return processedOwners;
	}
}
