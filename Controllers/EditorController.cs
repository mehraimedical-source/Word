using MedicalWord.Web.Services;
using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MedicalWord.Web.Controllers
{
    public class EditorController : Controller
    {
        private static readonly IDocumentImporter[] Importers =
        {
            new DocxDocumentImporter()
        };

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Import(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                return Json(new { ok = false, error = "No file was selected." });

            var extension = Path.GetExtension(file.FileName) ?? string.Empty;
            var importer = Importers.FirstOrDefault(x => x.CanImport(extension));

            if (importer == null)
            {
                var message = extension.Equals(".doc", StringComparison.OrdinalIgnoreCase)
                    ? "Legacy .doc files need a conversion provider. DOCX is supported in this phase."
                    : "Unsupported file type. Please select a .docx file.";

                return Json(new { ok = false, error = message });
            }

            try
            {
                var html = importer.ImportToHtml(file.InputStream);
                return Json(new { ok = true, html });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = "Could not import this document: " + ex.Message });
            }
        }
    }
}