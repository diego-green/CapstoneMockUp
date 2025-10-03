using Microsoft.AspNetCore.Mvc.RazorPages;

public class DashboardModel : PageModel
{
    public ProjectVm? LatestProject { get; set; }
    public string? LatestAnnouncement { get; set; }

    public void OnGet()
    {
        if (TempData.ContainsKey("ProjectTitle"))
        {
            LatestProject = new ProjectVm
            {
                Title = TempData["ProjectTitle"]?.ToString() ?? "",
                Description = TempData["ProjectDescription"]?.ToString()
            };
        }
        if (TempData.ContainsKey("Announcement"))
        {
            LatestAnnouncement = TempData["Announcement"]?.ToString();
            }
     }
    

    public class ProjectVm
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }

    }
}
