using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
namespace TestingMockup.Pages.Project
{
    public class EditAddModel : PageModel
    {
        [BindProperty] public string Title { get; set; } = "";
        [BindProperty] public string? Description { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            // TODO: save, e.g., TempData + redirect back to Dashboard
            TempData["ProjectTitle"] = Title;
            TempData["ProjectDescription"] = Description;
            return RedirectToPage("/Dashboard");
        }
    }
}
