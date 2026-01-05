using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json.Linq;
using Soltec.Portal.Web.Models.Script;
using Soltec.Portal.Web.Services.IRepository;

namespace Script.Controllers
{
    public class ScriptController : Controller
    {
        private readonly IScriptRepository scriptService;
        private readonly IConfiguration configuration;

        public ScriptController (IScriptRepository _scriptService, IConfiguration configuration)
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

        public async Task<IActionResult> Details(int? id)
        {
			var data = (await scriptService.LoadScripts());
			//data.List.Where(w => w.Activo == true).ToList().ForEach(f => f.Estado = "SI");

			//var _data = data.List.Where(w => w.IdSqlScript == id).FirstOrDefault();
			
            return View(data);
        }

		public async Task<IActionResult> Create(int? id)
        {
            if (id != null)
            {
                var data = (await scriptService.LoadScripts());
                //data.List.Where(w => w.Activo == true).ToList().ForEach(f => f.Estado = "SI");

                //var _data = data.List.Where(w => w.IdSqlScript == id).FirstOrDefault();
				return View(data);
			}
            else
            {
				return View(new SPOS_SQLScripts());
			}
			//return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SPOS_SQLScripts spos_SQLScripts)
        {
    
            return View(new SPOS_SQLScripts());
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            
            return View(new SPOS_SQLScripts());
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit( SPOS_SQLScripts spos_SQLScripts)
        {
           
            return View(new SPOS_SQLScripts());
        }

        public async Task<IActionResult> Delete(int? id)
        {
  

            return View(new SPOS_SQLScripts());
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            return RedirectToAction(nameof(Index));
        }

        private bool MedicoExists(int id)
        {
            return false;
        }

       
    }
}
