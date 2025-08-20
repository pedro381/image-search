using ImageSearch.Model;
using ImageSearch.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Annotations;

namespace ImageSearch.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ImageController : ControllerBase
    {
        [HttpPost]
        [AllowAnonymous]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [SwaggerResponse(200, "Lista de imagens encontradas", typeof(List<ImageData>))]
        [SwaggerResponse(400, "Arquivo de imagem não enviado")]
        [SwaggerResponse(404, "Imagem não encontrada")]
        public IActionResult Post([FromForm] ImageUploadRequest request)
        {
            if (request?.Image == null || request.Image.Length == 0)
                return BadRequest("Arquivo de imagem não enviado.");

            using var stream = request.Image.OpenReadStream();
            IEnumerable<MatchResult> result = ImageService.Get(stream);

            var imageDataList = new List<ImageData>();

            foreach (var match in result)
            {
                if (string.IsNullOrEmpty(match.Path) || !System.IO.File.Exists(match.Path))
                    continue;

                if (string.IsNullOrEmpty(match.Path) || !System.IO.File.Exists(match.Path))
                    continue;

                var imageBytes = System.IO.File.ReadAllBytes(match.Path);
                var extension = Path.GetExtension(match.Path)?.ToLowerInvariant();
                var mimeType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".tiff" => "image/tiff",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream"
                };

                var imageData = new ImageData
                {
                    Name = Path.GetFileName(match.Path),
                    Type = mimeType,
                    Image = imageBytes
                };
                imageDataList.Add(imageData);
            }

            if (imageDataList.Count == 0)
                return NotFound("Imagem não encontrada.");

            return Ok(imageDataList);
        }
    }
}
