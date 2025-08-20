using Microsoft.AspNetCore.Mvc;

namespace ImageSearch.Model
{
    public class ImageUploadRequest
    {
        /// <summary>
        /// Arquivo de imagem enviado via multipart/form-data.
        /// </summary>
        [FromForm(Name = "image")]
        public IFormFile? Image { get; set; }
    }

}
