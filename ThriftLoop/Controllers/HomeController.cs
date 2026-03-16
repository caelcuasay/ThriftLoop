using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ThriftLoop.Controllers;

[Authorize]
public class HomeController : Controller
{
    public IActionResult Index() => View();
}