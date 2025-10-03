using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication;

namespace TestingMockup.Pages.Account   // <-- same here
{
    public class SignOutModel : PageModel
    {
        public IActionResult OnGet()
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = "/Dashboard" },
                new[] { "Cookies", "OpenIdConnect" }
            );
        }
    }
}
