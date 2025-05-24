using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using ZXing;
using ZXing.Common;

namespace WebMVCTest.Controllers
{
    public class QrController : Controller
    {
        private readonly ILogger<QrController> _logger;

        public QrController(ILogger<QrController> logger)
        {
            _logger = logger;
        }

        // GET: Qr/Index - Página principal para escanear QR
        public IActionResult Index()
        {
            return View();
        }

        // GET: Qr/Scanner - Página del escáner con cámara
        public IActionResult Scanner()
        {
            return View();
        }

        // POST: Qr/UploadAndScan - Subir archivo y escanear QR
        [HttpPost]
        public async Task<IActionResult> UploadAndScan(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Por favor, selecciona un archivo de imagen.";
                return RedirectToAction("Index");
            }

            try
            {
                // Validar que sea una imagen
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    TempData["Error"] = "Por favor, sube solo archivos de imagen (JPG, PNG, BMP, GIF).";
                    return RedirectToAction("Index");
                }

                // Validar tamaño máximo (10MB)
                if (file.Length > 10 * 1024 * 1024)
                {
                    TempData["Error"] = "El archivo es demasiado grande. Máximo 10MB.";
                    return RedirectToAction("Index");
                }

                // Leer el archivo en memoria
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Convertir a Bitmap y escanear QR
                using var bitmap = new Bitmap(memoryStream);
                var result = ScanQrCode(bitmap);

                if (!string.IsNullOrEmpty(result.Content))
                {
                    result.FileName = file.FileName;
                    result.FileSize = file.Length;
                    result.ImageWidth = bitmap.Width;
                    result.ImageHeight = bitmap.Height;
                    result.ScanDateTime = DateTime.Now;
                    result.Success = true;

                    return View("Result", result);
                }
                else
                {
                    TempData["Error"] = "No se pudo encontrar ningún código QR en la imagen. Asegúrate de que el código sea visible y esté bien enfocado.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar la imagen {FileName}", file.FileName);
                TempData["Error"] = "Error al procesar la imagen. Inténtalo de nuevo.";
                return RedirectToAction("Index");
            }
        }

        // POST: Qr/ScanFromCamera - Escanear desde datos de cámara (base64)
        [HttpPost]
        public IActionResult ScanFromCamera([FromBody] CameraScanRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ImageData))
                {
                    return Json(new { success = false, message = "No se recibieron datos de imagen." });
                }

                // Convertir base64 a imagen
                var base64Data = request.ImageData.Contains(',')
                    ? request.ImageData.Split(',')[1]
                    : request.ImageData;

                var imageBytes = Convert.FromBase64String(base64Data);

                using var memoryStream = new MemoryStream(imageBytes);
                using var bitmap = new Bitmap(memoryStream);

                var result = ScanQrCode(bitmap);

                if (!string.IsNullOrEmpty(result.Content))
                {
                    return Json(new
                    {
                        success = true,
                        content = result.Content,
                        format = result.Format,
                        scanDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }
                else
                {
                    return Json(new { success = false, message = "No se encontró código QR en la imagen." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al escanear desde cámara");
                return Json(new { success = false, message = "Error al procesar la imagen de la cámara." });
            }
        }

        // GET: Qr/History - Historial de escaneos
        public IActionResult History()
        {
            return View(new List<QrResult>());
        }

        // Método principal para escanear códigos QR
        private QrResult ScanQrCode(Bitmap bitmap)
        {
            var result = new QrResult();

            try
            {
                // Crear el lector especificando el tipo Bitmap
                var reader = new BarcodeReaderGeneric();

                // Configurar opciones de decodificación
                reader.Options = new DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new List<BarcodeFormat>
                    {
                        BarcodeFormat.QR_CODE,
                        BarcodeFormat.DATA_MATRIX,
                        BarcodeFormat.AZTEC,
                        BarcodeFormat.PDF_417
                    }
                };

                // Convertir Bitmap a formato que ZXing puede leer
                var luminanceSource = new ZXing.Windows.Compatibility.BitmapLuminanceSource(bitmap);

                // Intentar decodificar directamente desde LuminanceSource
                var barcodeResult = reader.Decode(luminanceSource);

                if (barcodeResult != null)
                {
                    result.Content = barcodeResult.Text;
                    result.Format = barcodeResult.BarcodeFormat.ToString();
                    return result;
                }

                // Si no encuentra nada, intentar con diferentes configuraciones
                return TryWithEnhancements(bitmap);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al escanear código QR");
                return result; // Retorna resultado vacío
            }
        }

        // Método para intentar con mejoras en la imagen
        private QrResult TryWithEnhancements(Bitmap originalBitmap)
        {
            var result = new QrResult();

            try
            {
                var reader = new BarcodeReaderGeneric();

                // Intentar con diferentes configuraciones
                var configurations = new[]
                {
                    new DecodingOptions { TryHarder = true, PureBarcode = false },
                    new DecodingOptions { TryHarder = true, PureBarcode = true },
                    new DecodingOptions { TryHarder = false, PureBarcode = false }
                };

                foreach (var config in configurations)
                {
                    reader.Options = config;

                    var luminanceSource = new ZXing.Windows.Compatibility.BitmapLuminanceSource(originalBitmap);

                    var barcodeResult = reader.Decode(luminanceSource);
                    if (barcodeResult != null)
                    {
                        result.Content = barcodeResult.Text;
                        result.Format = barcodeResult.BarcodeFormat.ToString();
                        return result;
                    }
                }

                // Intentar con imagen redimensionada
                var newSize = CalculateOptimalSize(originalBitmap.Width, originalBitmap.Height);
                if (newSize.Width != originalBitmap.Width || newSize.Height != originalBitmap.Height)
                {
                    using var resizedBitmap = new Bitmap(originalBitmap, newSize);
                    var luminanceSource = new ZXing.Windows.Compatibility.BitmapLuminanceSource(resizedBitmap);

                    var barcodeResult = reader.Decode(luminanceSource);
                    if (barcodeResult != null)
                    {
                        result.Content = barcodeResult.Text;
                        result.Format = barcodeResult.BarcodeFormat.ToString();
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error aplicando mejoras a la imagen");
            }

            return result; // Retorna resultado vacío si no encuentra nada
        }

        private Size CalculateOptimalSize(int width, int height)
        {
            const int optimalSize = 800;
            var maxDimension = Math.Max(width, height);

            if (maxDimension <= optimalSize)
                return new Size(width, height);

            var scale = (float)optimalSize / maxDimension;
            return new Size((int)(width * scale), (int)(height * scale));
        }
    }

    // Modelos de datos
    public class QrResult
    {
        public string Content { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime ScanDateTime { get; set; }
        public bool Success { get; set; }
        public long FileSize { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }

        public string GetFormattedSize() => FileSize < 1024 * 1024
            ? $"{FileSize / 1024:F1} KB"
            : $"{FileSize / (1024 * 1024):F1} MB";
    }

    public class CameraScanRequest
    {
        public string ImageData { get; set; } = string.Empty;
    }
}