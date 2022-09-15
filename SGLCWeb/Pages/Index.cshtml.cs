using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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
			if ( string.IsNullOrEmpty(IdList) )
				return Page();

			GroupLibCompare.Program.Log( IdList );
			return Page();
		}
	}
}
