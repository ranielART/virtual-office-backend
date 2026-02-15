using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entity.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        public AuthController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet("index")]
        public IActionResult Index()
        {
            return Ok(new { message = "Hello World" });
        }

        //public async Task<IActionResult> Register()
        //{

        //}
    }
}