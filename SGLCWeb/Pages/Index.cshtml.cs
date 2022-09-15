using GroupLibCompare;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Steam.Models.SteamStore;
using System.Text;

namespace SGLCWeb.Pages
{
	public class IndexModel : PageModel
	{
		private readonly ILogger<IndexModel> _logger;

		[BindProperty( SupportsGet = true )]
		public string IdList { get; set; }

		public IndexModel( ILogger<IndexModel> logger )
		{
			_logger = logger;
		}

		public void OnGet()
		{
		}

		public async Task<IActionResult> OnPostAsync()
		{
			if ( string.IsNullOrEmpty( IdList ) )
				return Page();

			var processedList = IdList.Split( " " );
			var steamIds = new List<ulong>( processedList.Length );

			foreach ( var i in processedList )
			{
				//GroupLibCompare.Program.Log( i );
				steamIds.Add( ulong.Parse( i ) );
			}

			IEnumerable<GameWithOwners> games = await GroupLibCompare.Program.DoGroupLookup( steamids: steamIds );

			StringBuilder output = new();
			foreach ( var game in games )
				output.Append( $"{game.Appid}\n" );

			//var c = new HttpClient();
			//var output = new StringBuilder();
			//foreach ( var game in games )
			//{
			//	StoreAppDetailsDataModel x = await c.GetFromJsonAsync<StoreAppDetailsDataModel>( $"https://store.steampowered.com/api/appdetails?appids={game.Appid}" );
			//	if ( x != null )
			//		output.Append( $"{x.Name}\n" );
			//	else
			//		output.Append( game.Appid );
			//}

			@ViewData["Output"] = output.ToString();
			return Page();
		}
	}
}
