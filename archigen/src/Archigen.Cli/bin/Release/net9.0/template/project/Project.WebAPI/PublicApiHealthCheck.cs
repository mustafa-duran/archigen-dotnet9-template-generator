using Microsoft.AspNetCore.Mvc;

using Project.WebAPI.Controllers;

namespace Project.WebAPI;

[Route("/")]
[ApiExplorerSettings(IgnoreApi = true)]
public class PublicApiHealthCheck : BaseController
{
    [HttpGet]
    public string Check()
    {
        return "Hurray, API is running! 🎉";
    }
}