using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace TestingMockup.Pages
{
    [Authorize]
    public class EngineerModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
