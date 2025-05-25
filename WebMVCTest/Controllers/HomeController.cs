using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Text.Json;

namespace WebMVCTest.Controllers
{
    public class FaceController : Controller
    {
        private readonly ILogger<FaceController> _logger;
        private readonly IWebHostEnvironment _environment;

        public FaceController(ILogger<FaceController> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        // GET: Face/Index - Página principal de reconocimiento facial
        public IActionResult Index()
        {
            return View();
        }

        // GET: Face/Detection - Detección facial en tiempo real
        public IActionResult Detection()
        {
            return View();
        }

        // GET: Face/Recognition - Reconocimiento facial con base de datos
        public IActionResult Recognition()
        {
            return View();
        }

        // GET: Face/Upload - Subir y analizar imagen
        public IActionResult Upload()
        {
            return View();
        }

        // POST: Face/AnalyzeImage - Analizar imagen subida
        [HttpPost]
        public async Task<IActionResult> AnalyzeImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Por favor, selecciona un archivo de imagen.";
                return RedirectToAction("Upload");
            }

            try
            {
                // Validar que sea una imagen
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["Error"] = "Por favor, sube solo archivos de imagen (JPG, PNG, BMP).";
                    return RedirectToAction("Upload");
                }

                // Validar tamaño máximo (5MB)
                if (file.Length > 5 * 1024 * 1024)
                {
                    TempData["Error"] = "El archivo es demasiado grande. Máximo 5MB.";
                    return RedirectToAction("Upload");
                }

                // Guardar imagen temporalmente para análisis
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsPath))
                {
                    Directory.CreateDirectory(uploadsPath);
                }

                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var result = new FaceAnalysisResult
                {
                    FileName = file.FileName,
                    FileSize = file.Length,
                    TempImagePath = $"/uploads/{fileName}",
                    AnalysisDateTime = DateTime.Now,
                    Success = true
                };

                return View("AnalysisResult", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar imagen para análisis facial");
                TempData["Error"] = "Error al procesar la imagen. Inténtalo de nuevo.";
                return RedirectToAction("Upload");
            }
        }

        // POST: Face/SaveFaceData - Guardar datos de rostro detectado
        [HttpPost]
        public async Task<IActionResult> SaveFaceData([FromBody] SaveFaceDataRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.FaceDescriptor) || string.IsNullOrEmpty(request.PersonName))
                {
                    return Json(new { success = false, message = "Datos incompletos." });
                }

                // Aquí puedes guardar en base de datos
                // Por ahora, guardamos en un archivo JSON local
                var dataPath = Path.Combine(_environment.WebRootPath, "facedata");
                if (!Directory.Exists(dataPath))
                {
                    Directory.CreateDirectory(dataPath);
                }

                var faceData = new FaceData
                {
                    Id = Guid.NewGuid().ToString(),
                    PersonName = request.PersonName,
                    FaceDescriptor = request.FaceDescriptor,
                    CreatedDate = DateTime.Now,
                    ImagePath = request.ImagePath
                };

                var filePath = Path.Combine(dataPath, $"{faceData.Id}.json");
                var jsonData = JsonSerializer.Serialize(faceData, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(filePath, jsonData);

                return Json(new { success = true, message = "Rostro guardado exitosamente.", faceId = faceData.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar datos faciales");
                return Json(new { success = false, message = "Error al guardar los datos." });
            }
        }

        // GET: Face/GetSavedFaces - Obtener rostros guardados
        [HttpGet]
        public IActionResult GetSavedFaces()
        {
            try
            {
                var dataPath = Path.Combine(_environment.WebRootPath, "facedata");
                var faces = new List<FaceData>();

                if (Directory.Exists(dataPath))
                {
                    var files = Directory.GetFiles(dataPath, "*.json");
                    foreach (var file in files)
                    {
                        var jsonData = System.IO.File.ReadAllText(file);
                        var faceData = JsonSerializer.Deserialize<FaceData>(jsonData);
                        if (faceData != null)
                        {
                            faces.Add(faceData);
                        }
                    }
                }

                return Json(new { success = true, faces = faces });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener rostros guardados");
                return Json(new { success = false, message = "Error al cargar los datos." });
            }
        }

        // POST: Face/DeleteFace - Eliminar rostro guardado
        [HttpPost]
        public IActionResult DeleteFace([FromBody] DeleteFaceRequest request)
        {
            try
            {
                var dataPath = Path.Combine(_environment.WebRootPath, "facedata");
                var filePath = Path.Combine(dataPath, $"{request.FaceId}.json");

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    return Json(new { success = true, message = "Rostro eliminado exitosamente." });
                }
                else
                {
                    return Json(new { success = false, message = "Rostro no encontrado." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar rostro");
                return Json(new { success = false, message = "Error al eliminar el rostro." });
            }
        }

        // GET: Face/Statistics - Estadísticas de reconocimiento
        public IActionResult Statistics()
        {
            try
            {
                var dataPath = Path.Combine(_environment.WebRootPath, "facedata");
                var stats = new FaceStatistics();

                if (Directory.Exists(dataPath))
                {
                    var files = Directory.GetFiles(dataPath, "*.json");
                    stats.TotalFaces = files.Length;
                    stats.LastRegistration = files.Length > 0
                        ? files.Select(f => new FileInfo(f).CreationTime).Max()
                        : (DateTime?)null;
                }

                return View(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas");
                return View(new FaceStatistics());
            }
        }

        // Método auxiliar para limpiar archivos temporales
        private void CleanupTempFiles()
        {
            try
            {
                var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
                if (Directory.Exists(uploadsPath))
                {
                    var files = Directory.GetFiles(uploadsPath);
                    var cutoffTime = DateTime.Now.AddHours(-1); // Eliminar archivos de más de 1 hora

                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.CreationTime < cutoffTime)
                        {
                            fileInfo.Delete();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al limpiar archivos temporales");
            }
        }
    }

    // Modelos de datos para reconocimiento facial
    public class FaceAnalysisResult
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string TempImagePath { get; set; } = string.Empty;
        public DateTime AnalysisDateTime { get; set; }
        public bool Success { get; set; }
        public int DetectedFaces { get; set; }
        public List<DetectedFace> Faces { get; set; } = new List<DetectedFace>();

        public string GetFormattedSize() => FileSize < 1024 * 1024
            ? $"{FileSize / 1024:F1} KB"
            : $"{FileSize / (1024 * 1024):F1} MB";
    }

    public class DetectedFace
    {
        public string Id { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public FaceAttributes Attributes { get; set; } = new FaceAttributes();
        public FaceRectangle Rectangle { get; set; } = new FaceRectangle();
    }

    public class FaceAttributes
    {
        public string Gender { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Emotion { get; set; } = string.Empty;
        public bool HasGlasses { get; set; }
        public bool HasMask { get; set; }
    }

    public class FaceRectangle
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class FaceData
    {
        public string Id { get; set; } = string.Empty;
        public string PersonName { get; set; } = string.Empty;
        public string FaceDescriptor { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string ImagePath { get; set; } = string.Empty;
    }

    public class SaveFaceDataRequest
    {
        public string PersonName { get; set; } = string.Empty;
        public string FaceDescriptor { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
    }

    public class DeleteFaceRequest
    {
        public string FaceId { get; set; } = string.Empty;
    }

    public class FaceStatistics
    {
        public int TotalFaces { get; set; }
        public DateTime? LastRegistration { get; set; }
        public int RecognitionsToday { get; set; }
        public double AverageConfidence { get; set; }
    }
}