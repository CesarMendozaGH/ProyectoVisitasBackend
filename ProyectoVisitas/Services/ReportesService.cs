using Microsoft.AspNetCore.Mvc;

namespace ProyectoVisitas.Services
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
