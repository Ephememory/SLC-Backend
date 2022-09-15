using SteamWebAPI2.Utilities;
using SteamWebAPI2.Interfaces;
using Steam.Models.SteamCommunity;

namespace GroupLibCompare;

public readonly record struct GameWithOwners( uint Appid, List<ulong> Owners );
public readonly record struct UserGameLibrary( PlayerSummaryModel Player, IEnumerable<OwnedGameModel> Library );

public static class Program
{
	const string ApiKey = "B31BA9A9C29FC66CD0174F8BC2A176D8";
	const string SteamApiUrl = @"https://store.steampowered.com/api";

	public static HttpClient client = new();
	static SteamWebInterfaceFactory interfaceFactory;
	static SteamUser steamInterface;
	static PlayerService playerService;
	static bool debug = true;

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

		var builder = WebApplication.CreateBuilder( args );

		// Add services to the container.
		builder.Services.AddRazorPages();

		var app = builder.Build();

		// Configure the HTTP request pipeline.
		if ( !app.Environment.IsDevelopment() )
		{
			app.UseExceptionHandler( "/Error" );
			// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
			app.UseHsts();
		}

		app.UseHttpsRedirection();
		app.UseStaticFiles();
		app.UseRouting();
		app.UseAuthorization();
		app.MapRazorPages();
		app.Run();
	}

	public static async Task DoGroupLookup( List<ulong> steamids )
	{
		// Fetch their profiles.
		var summariesResponse = await steamInterface.GetPlayerSummariesAsync( steamids );

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
	}

	public static IEnumerable<GameWithOwners> GetGamesInCommon( IEnumerable<UserGameLibrary> users )
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
