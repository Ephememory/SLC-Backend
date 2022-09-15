using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SteamWebAPI2.Utilities;
using SteamWebAPI2.Interfaces;
using SLC.Data;
using Steam.Models.SteamCommunity;

namespace SLC;

public readonly record struct GameWithOwners( uint Appid, List<ulong> Owners );
public readonly record struct UserGameLibrary( PlayerSummaryModel Player, IEnumerable<OwnedGameModel> Library );

public static class Program
{
	const string ApiKey = "B31BA9A9C29FC66CD0174F8BC2A176D8";
	const string SteamApiUrl = @"https://store.steampowered.com/api";
	public const string AppName = "steamlibcomparer";

	public static HttpClient client = new();
	public static SteamWebInterfaceFactory interfaceFactory;
	public static SteamUser steamInterface;
	public static PlayerService playerService;
	static bool debug = true;

	public static void Log( object info )
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
		builder.Services.AddServerSideBlazor();
		builder.Services.AddSingleton<WeatherForecastService>();

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

		app.MapBlazorHub();
		app.MapFallbackToPage( "/_Host" );

		app.Run();
	}
}
