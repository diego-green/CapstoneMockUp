using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TestingMockup.Pages.Project
{
    public class EditAddAnnouncementModel : PageModel
    {
        [BindProperty] public string? Announcement { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            // TODO: save, e.g., TempData + redirect back to Dashboard
            TempData["Announcement"] = Announcement;
            return RedirectToPage("/Dashboard");
        }
    }
}
