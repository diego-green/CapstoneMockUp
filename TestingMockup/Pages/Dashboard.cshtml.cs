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

    public DashboardModel(IWebHostEnvironment env)
    {
        _env = env;
    }

    public string? ErrorMessage { get; set; }
    public List<string> SlideUrls { get; set; } = new();

    public void OnGet()
    {
        if (TempData.ContainsKey("Announcement"))
        {
            LatestAnnouncement = TempData["Announcement"]?.ToString();
        }
        LoadLatestSlides();
    }

    // 1) Upload PDF  2) Convert each page -> PNG with Magick.NET  3) Store under wwwroot/presentations/<setId>/
    public async Task<IActionResult> OnPostUploadPdfAsync(IFormFile pdfFile)
    {
        if (pdfFile == null || pdfFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "No file selected.");
            LoadLatestSlides();
            return Page();
        }

        var ext = Path.GetExtension(pdfFile.FileName).ToLowerInvariant();
        if (ext != ".pdf")
        {
            ModelState.AddModelError(string.Empty, "Please upload a .pdf file.");
            LoadLatestSlides();
            return Page();
        }

        if (pdfFile.Length > 100 * 1024 * 1024)
        {
            ModelState.AddModelError(string.Empty, "File too large (max 100 MB).");
            LoadLatestSlides();
            return Page();
        }

        var root = _env.WebRootPath;
        var baseDir = Path.Combine(root, "presentations");
        Directory.CreateDirectory(baseDir);

        var setId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var setDir = Path.Combine(baseDir, setId);
        Directory.CreateDirectory(setDir);

        var originalName = Path.GetFileNameWithoutExtension(pdfFile.FileName);
        var pdfPath = Path.Combine(setDir, $"{originalName}.pdf");
        using (var fs = System.IO.File.Create(pdfPath))
            await pdfFile.CopyToAsync(fs);

        try
        {
            // Try to locate Ghostscript automatically (required by Magick.NET for PDF rasterizing on Windows)
            TrySetGhostscriptDirectory();

            // Render each page at a decent DPI onto white (for transparency-safe output)
            var readSettings = new MagickReadSettings
            {
                Density = new Density(200, 200), // DPI
                BackgroundColor = MagickColors.White
            };

            using var pages = new MagickImageCollection();
            pages.Read(pdfPath, readSettings); // read all pages

            int index = 1;
            foreach (var page in pages)
            {
                page.Alpha(AlphaOption.Remove);
                page.Format = MagickFormat.Png;
                var outPath = Path.Combine(setDir, $"slide-{index:D3}.png");
                page.Write(outPath);
                index++;
            }
        }
        catch (MagickMissingDelegateErrorException)
        {
            // Ghostscript not found
            ModelState.AddModelError(string.Empty,
                "PDF -> image conversion failed: Ghostscript not found. Please install Ghostscript (64-bit) and restart the app.");
            LoadLatestSlides();
            return Page();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Conversion error: {ex.Message}");
            LoadLatestSlides();
            return Page();
        }

        return RedirectToPage("/Dashboard");
    }

    private void LoadLatestSlides()
    {
        SlideUrls.Clear();

        var baseDir = Path.Combine(_env.WebRootPath, "presentations");
        if (!Directory.Exists(baseDir)) return;

        var latestSet = Directory.GetDirectories(baseDir)
            .OrderByDescending(Directory.GetCreationTimeUtc)
            .FirstOrDefault();
        if (latestSet == null) return;

        var pngs = Directory.EnumerateFiles(latestSet, "slide-*.png", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p)
            .ToList();
        if (!pngs.Any()) return;

        var req = HttpContext.Request;
        var baseUrl = $"{req.Scheme}://{req.Host}";

        foreach (var path in pngs)
        {
            var rel = path.Replace(_env.WebRootPath, "").Replace("\\", "/");
            if (!rel.StartsWith("/")) rel = "/" + rel;
            SlideUrls.Add(baseUrl + rel);
        }
    }

    private static void TrySetGhostscriptDirectory()
    {
        // If Ghostscript is installed under the default path, this will point Magick.NET to it.
        // You can hardcode a path if you prefer: MagickNET.SetGhostscriptDirectory(@"C:\Program Files\gs\gs10.03.1\bin");
        var gsRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "gs");
        if (!Directory.Exists(gsRoot)) return;

        var newest = Directory.GetDirectories(gsRoot)
            .OrderByDescending(d => d) // folder names like gs10.03.1 sort OK lexicographically
            .FirstOrDefault();

        if (newest == null) return;

        var bin = Path.Combine(newest, "bin");
        if (Directory.Exists(bin))
        {
            MagickNET.SetGhostscriptDirectory(bin);
        }
    }
}
