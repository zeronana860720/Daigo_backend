namespace DemoShopApi.services
{
    public class ImageUploadService
    {
        private readonly IWebHostEnvironment _env;

        public ImageUploadService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string?> SaveProductImageAsync(IFormFile? image)
        {
            if (image == null || image.Length == 0)
                return null;

            var uploadDir = Path.Combine(
                _env.WebRootPath,
                "uploads",
                "products");

            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            var ext = Path.GetExtension(image.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            return $"/uploads/products/{fileName}";
        }

        public void DeleteImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return;

            var fullPath = Path.Combine(
                _env.WebRootPath,
                imagePath.TrimStart('/'));

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
    }
}
