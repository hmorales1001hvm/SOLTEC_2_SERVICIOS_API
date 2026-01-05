using Microsoft.AspNetCore.Mvc;
using Soltec.Portal.Web.Models;
using Soltec.Portal.Web.Services.IRepository;
using System.Diagnostics;

namespace Soltec.Portal.Web.Controllers
{
	public class HomeController : Controller
	{
		private readonly IScriptRepository scriptService;
		private readonly IConfiguration configuration;

		public HomeController(IScriptRepository _scriptService, IConfiguration configuration)
		{
			scriptService = _scriptService;
			this.configuration = configuration;
		}


		public async Task<IActionResult> Index()
		{
			var data = (await scriptService.LoadScripts());
			//data.List.Where(w => w.Activo == true).ToList().ForEach(f => f.Estado = "SI");

			return View(data.List);
		}


		public IActionResult Privacy()
		{
			return View();
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}
