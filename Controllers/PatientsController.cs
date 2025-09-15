using Cura.Web.Data;
using Cura.Web.Models;
using Cura.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cura.Web.Controllers
{
    public class PatientsController : Controller
    {
        private readonly IPatientRepo _repo;
        private readonly IAiClient _ai;

        public PatientsController(IPatientRepo repo, IAiClient ai)
        {
            _repo = repo;
            _ai = ai;
        }

        // GET: /Patients
        public IActionResult Index()
        {
            var patients = _repo.GetAll();
            return View(patients);
        }

        // GET: /Patients/Details/patient-001
        public IActionResult Details(string id = "1754bc7d-28cd-4933-fc72-3d9a0d77cf54")
        {
            var sel = _repo.Get(id);
            if (sel is null) return NotFound();

            var vm = new PatientDetailsVM
            {
                Patients = _repo.GetAll(),
                Selected = sel
            };
            return View(vm);
        }

        // POST: /Patients/Ask
        [HttpPost]
        public async Task<IActionResult> Ask(string id, string? question)
        {
            var p = _repo.Get(id);
            if (p is null) return NotFound();

            try
            {
                var answer = await _ai.SummarizeAndDraftAsync(p, question);
                return Json(new { ok = true, answer });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message });
            }
        }
    }
}
