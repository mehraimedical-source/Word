using System.IO;

namespace MedicalWord.Web.Services
{
    public interface IDocumentImporter
    {
        bool CanImport(string extension);
        string ImportToHtml(Stream input);
    }
}