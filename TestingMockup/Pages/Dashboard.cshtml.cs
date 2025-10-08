using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

// NuGet: Magick.NET-Q8-AnyCPU
using ImageMagick;

public class DashboardModel : PageModel
{
    private readonly IWebHostEnvironment _env;
    public string? LatestAnnouncement { get; set; }
    // If you deploy to a Windows Server and want a persistent path (e.g., D:\Data\Presentations),
    // set this to that path. For now, we default to wwwroot/presentations to keep it simple.
    private string SlidesRoot =>
        Path.Combine(_env.WebRootPath, "presentations");
    // e.g., return @"D:\Data\Presentations";

    public DashboardModel(IWebHostEnvironment env)
    {
        _env = env;
    }

    // UI state
    [BindProperty(SupportsGet = true)]
    public string? ProjectId { get; set; }  // selected project (folder name-safe)

    public List<ProjectItem> Projects { get; set; } = new(); // list for the dropdown
    public List<string> SlideUrls { get; set; } = new();     // images for the slideshow
    public string? LatestSetId { get; set; }                 // shown set (folder name)
    public string PageMessage { get; set; } = "";
    [BindProperty] public string? Announcement { get; set; }

    // Upload form fields
    [BindProperty]
    public string? NewProjectName { get; set; }

    public class ProjectItem
    {
        public string Id { get; set; } = "";     // folder name
        public string Name { get; set; } = "";   // display label (same as Id here)
    }

    public void OnGet()
    {
        EnsureFolders();
        LoadProjects();

        // Default to the first project if none selected
        if (string.IsNullOrWhiteSpace(ProjectId) && Projects.Count > 0)
            ProjectId = Projects[0].Id;

        if (!string.IsNullOrWhiteSpace(ProjectId))
            LoadLatestSlidesForProject(ProjectId);

        if (TempData.ContainsKey("Announcement"))
            LatestAnnouncement = TempData["Announcement"]?.ToString();
    }
    public IActionResult OnPost()
    {
  // If the binder somehow missed it, try to recover from the form
    if (string.IsNullOrWhiteSpace(ProjectId))
        ProjectId = Request.Form["ProjectId"];

    TempData["Announcement"] = Announcement;
    TempData["ProjectId"] = ProjectId; // safety net
    return RedirectToPage("/Dashboard", new { ProjectId });
    }

    // Upload PDF to a specific project (existing or new). Creates a new "set" timestamp folder.
    public async Task<IActionResult> OnPostUploadPdfAsync(IFormFile pdfFile, string? projectId, string? newProjectName)
    {
        EnsureFolders();
        LoadProjects();

        // Resolve target project: existing from dropdown OR new project text
        var targetProject = ResolveProjectId(projectId, newProjectName);
        ProjectId = targetProject; // keep selection after redirect

        if (pdfFile == null || pdfFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "No file selected.");
            LoadLatestSlidesForProject(targetProject);
            return RedirectToPage(null, new { ProjectId });
        }

        var ext = Path.GetExtension(pdfFile.FileName).ToLowerInvariant();
        if (ext != ".pdf")
        {
            ModelState.AddModelError(string.Empty, "Please upload a .pdf file.");
            LoadLatestSlidesForProject(targetProject);
            return RedirectToPage(null, new { ProjectId });
        }

        if (pdfFile.Length > 100 * 1024 * 1024)
        {
            ModelState.AddModelError(string.Empty, "File too large (max 100 MB).");
            LoadLatestSlidesForProject(targetProject);
            return RedirectToPage(null, new { ProjectId });
        }

        try
        {
            // project/<setId>/
            var setId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var setDir = Path.Combine(SlidesRoot, targetProject, setId);
            Directory.CreateDirectory(setDir);

            // Save original pdf (optional to keep)
            var originalName = Path.GetFileNameWithoutExtension(pdfFile.FileName);
            var pdfPath = Path.Combine(setDir, $"{originalName}.pdf");
            using (var fs = System.IO.File.Create(pdfPath))
                await pdfFile.CopyToAsync(fs);

            // Allow Magick.NET to find Ghostscript if installed in default path
            TrySetGhostscriptDirectory();

            // Convert all pages -> PNG
            var readSettings = new MagickReadSettings
            {
                Density = new Density(200, 200),
                BackgroundColor = MagickColors.White
            };

            using var pages = new MagickImageCollection();
            pages.Read(pdfPath, readSettings);

            int index = 1;
            foreach (var page in pages)
            {
                page.Alpha(AlphaOption.Remove);
                page.Format = MagickFormat.Png;
                var outPath = Path.Combine(setDir, $"slide-{index:D3}.png");
                page.Write(outPath);
                index++;
            }

            // Redirect to GET with selected project so the new set is shown
            return RedirectToPage("/Dashboard", new { ProjectId = targetProject });
        }
        catch (MagickMissingDelegateErrorException)
        {
            ModelState.AddModelError(string.Empty,
                "PDF ? image conversion failed (Ghostscript missing). Install Ghostscript 64-bit on the server.");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Conversion error: {ex.Message}");
        }

        LoadLatestSlidesForProject(targetProject);
        return Page();
    }

    // --- helpers ---

    private void EnsureFolders()
    {
        Directory.CreateDirectory(SlidesRoot);
    }

    private void LoadProjects()
    {
        Projects.Clear();
        if (!Directory.Exists(SlidesRoot)) return;

        foreach (var dir in Directory.GetDirectories(SlidesRoot))
        {
            var id = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(id)) continue;

            Projects.Add(new ProjectItem { Id = id, Name = id });
        }

        Projects = Projects.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void LoadLatestSlidesForProject(string projectId)
    {
        SlideUrls.Clear();
        LatestSetId = null;

        var projectRoot = Path.Combine(SlidesRoot, projectId);
        if (!Directory.Exists(projectRoot))
        {
            PageMessage = $"Project '{projectId}' has no uploads yet.";
            return;
        }

        // find latest set (subfolder)
        var latestSet = Directory.GetDirectories(projectRoot)
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .FirstOrDefault();

        if (latestSet == null)
        {
            PageMessage = $"Project '{projectId}' has no slide sets yet.";
            return;
        }

        LatestSetId = Path.GetFileName(latestSet);

        var pngs = Directory.EnumerateFiles(latestSet, "slide-*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!pngs.Any())
        {
            PageMessage = $"No slides found for project '{projectId}' / set '{LatestSetId}'.";
            return;
        }

        var req = HttpContext.Request;
        var baseUrl = $"{req.Scheme}://{req.Host}";

        foreach (var p in pngs)
        {
            var rel = p.Replace(_env.WebRootPath, "").Replace("\\", "/");
            if (!rel.StartsWith("/")) rel = "/" + rel;
            SlideUrls.Add(baseUrl + rel);
        }
    }

    private static void TrySetGhostscriptDirectory()
    {
        var gsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "gs");
        if (!Directory.Exists(gsRoot)) return;

        var newest = Directory.GetDirectories(gsRoot)
            .OrderByDescending(d => d) // gs10.03.1, etc.
            .FirstOrDefault();

        if (newest == null) return;

        var bin = Path.Combine(newest, "bin");
        if (Directory.Exists(bin))
            MagickNET.SetGhostscriptDirectory(bin);
    }

    private static string ResolveProjectId(string? projectId, string? newProjectName)
    {
        // prefer new project if provided
        var candidate = (newProjectName ?? "").Trim();
        if (candidate.Length > 0)
            return SanitizeProjectId(candidate);

        // fallback to selected project
        candidate = (projectId ?? "").Trim();
        if (candidate.Length > 0)
            return SanitizeProjectId(candidate);

        // default bucket
        return "DefaultProject";
    }

    private static string SanitizeProjectId(string name)
    {
        // Make it a folder-safe ID (letters, numbers, dashes, underscores)
        var cleaned = new string(name.Select(ch =>
            char.IsLetterOrDigit(ch) ? ch :
            (ch == '-' || ch == '_' ? ch : '-')
        ).ToArray());

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "Project";

        return cleaned.Trim('-');
    }
}
