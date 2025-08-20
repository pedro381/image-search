using ImageSearch.Model;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;

namespace ImageSearch.Service
{
  public  class ImageService
    {
        // Paths (adjust if needed)
        private static readonly string ImagesFolder = @"imagens_temp";
        private static readonly string ModelFolder = Path.Combine(ImagesFolder, "model");
        private static readonly string ModelPath = Path.Combine(ModelFolder, "clip-ViT-B-32-vision.onnx");
        private static readonly string IndexPath = Path.Combine(ModelFolder, "clip-ViT-B-32-vision.json");

        // CLIP preprocessing constants
        // ImageNet/CLIP normalization
        private static readonly float[] Mean = new float[] { 0.48145466f, 0.4578275f, 0.40821073f };
        private static readonly float[] Std = new float[] { 0.26862954f, 0.26130258f, 0.27577711f };

        // Target size (most CLIP encoders use 224x224)
        private const int Target = 224;

        private const string InputName = "pixel_values";  
        private const string OutputName = "image_embeds";  

        public static IEnumerable<MatchResult> Get(Stream queryImage)
        {
            try
            {
                Directory.CreateDirectory(ModelFolder);

                if (!File.Exists(ModelPath))
                {
                    Console.WriteLine("Model not found:\n  " + ModelPath);
                    Console.WriteLine("Place a CLIP image-encoder ONNX model at that path and rerun.");
                    return null;
                }

                if (!File.Exists(IndexPath))
                {
                    Console.WriteLine("No index found. Building index...");
                    BuildIndex(ModelPath, ImagesFolder, IndexPath);
                }

                Console.WriteLine("Searching most visually similar image...");
                var best = QueryNearest(ModelPath, IndexPath, queryImage);

                if (best == null)
                {
                    Console.WriteLine("No match found (index empty?).");
                    return null;
                }

                return best;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex);
            }
            return null;
        }

        // Build index: encode all image files in folder (non-recursive) except model & query dirs
        public static void BuildIndex(string modelPath, string imagesFolder, string indexPath)
        {
            // Suporta extensões comuns de imagem
            var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp" };
            var allImages = Directory.EnumerateFiles(imagesFolder, "*.*", SearchOption.TopDirectoryOnly)
                                     .Where(p => supportedExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                                     .Where(p => !IsUnder(p, ModelFolder) && !IsUnder(p, Path.Combine(imagesFolder, "query")))
                                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                     .ToList();

            if (allImages.Count == 0)
            {
                Console.WriteLine("Nenhum arquivo de imagem encontrado em " + imagesFolder);
                return;
            }

            Console.WriteLine($"Encontradas {allImages.Count} imagens. Codificando com o modelo...");
            using var session = new InferenceSession(modelPath);

            var items = new List<IndexItem>(capacity: allImages.Count);

            var done = 0;
            foreach (var imgPath in allImages)
            {
                try
                {
                    var input = Preprocess(imgPath);
                    using var results = session.Run(new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(InputName, input) });

                    var output = results.FirstOrDefault(v => v.Name == OutputName) ?? results.First(); // fallback to first
                    var vec = ToVector((DenseTensor<float>)output.AsTensor<float>());

                    // Normaliza para comprimento unitário (consistência da similaridade cosseno)
                    NormalizeInPlace(vec);

                    items.Add(new IndexItem { Path = imgPath, Embedding = vec });
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Falha: {imgPath} -> {e.Message}");
                }

                done++;
                if (done % 25 == 0) Console.WriteLine($"Codificadas {done}/{allImages.Count}...");
            }

            var index = new ImageIndex { Items = items };
            var json = JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(indexPath, json);

            Console.WriteLine($"Índice salvo: {indexPath}");
            Console.WriteLine($"Itens indexados: {items.Count}");
        }

        // Query the nearest neighbor from index
        public static IEnumerable<MatchResult>? QueryNearest(string modelPath, string indexPath, Stream queryImage)
        {
            if (!File.Exists(indexPath)) return null;

            var index = JsonSerializer.Deserialize<ImageIndex>(File.ReadAllText(indexPath));
            if (index?.Items == null || index.Items.Count == 0) return null;

            using var session = new InferenceSession(modelPath);

            var qInput = Preprocess(queryImage);
            using var results = session.Run(new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(InputName, qInput) });

            var output = results.FirstOrDefault(v => v.Name == OutputName) ?? results.First();
            var qVec = ToVector((DenseTensor<float>)output.AsTensor<float>());
            NormalizeInPlace(qVec);

            // Calcula similaridade para todos os itens
            var matches = index.Items
                .Where(it => it.Embedding != null)
                .Select(it => new MatchResult
                {
                    Path = it.Path,
                    Similarity = Cosine(qVec, it.Embedding)
                })
                .Where(m => m.Similarity >= 0.8f)
                .OrderByDescending(m => m.Similarity)
                .Take(10)
                .ToList();

            return matches.Count > 0 ? matches : null;
        }

        // Preprocess image -> Tensor [1,3,224,224] float32, normalized, NCHW
        public static DenseTensor<float> Preprocess(string imagePath)
        {
            using var image = Image.Load<Rgb24>(imagePath);
            return Preprocess(image);
        }

        public static DenseTensor<float> Preprocess(Stream imageStream)
        {
            using var image = Image.Load<Rgb24>(imageStream);
            return Preprocess(image);
        }

        public static DenseTensor<float> Preprocess(Image<Rgb24> image)
        {
            // Resize with center-crop to 224x224
            var square = ResizeAndCenterCrop(image, Target, Target);

            // Create tensor
            var tensor = new DenseTensor<float>(new[] { 1, 3, Target, Target });
            // Fill in NCHW
            for (var y = 0; y < Target; y++)
            {
                for (var x = 0; x < Target; x++)
                {
                    var pix = square[x, y];
                    // to [0,1]
                    var r = pix.R / 255f;
                    var g = pix.G / 255f;
                    var b = pix.B / 255f;

                    // normalize
                    r = (r - Mean[0]) / Std[0];
                    g = (g - Mean[1]) / Std[1];
                    b = (b - Mean[2]) / Std[2];

                    tensor[0, 0, y, x] = r;
                    tensor[0, 1, y, x] = g;
                    tensor[0, 2, y, x] = b;
                }
            }

            return tensor;
        }

        // Returns a 224x224 cropped image (no alpha), maintaining aspect
        public static Image<Rgb24> ResizeAndCenterCrop(Image<Rgb24> src, int w, int h)
        {
            // Scale shortest side to target, then center-crop
            var scale = Math.Max((float)w / src.Width, (float)h / src.Height);
            var nw = (int)Math.Round(src.Width * scale);
            var nh = (int)Math.Round(src.Height * scale);

            src.Mutate(ctx => ctx.Resize(nw, nh));

            var x = (nw - w) / 2;
            var y = (nh - h) / 2;

            var crop = src.Clone(ctx => ctx.Crop(new Rectangle(x, y, w, h)));
            return crop;
        }

        static float[] ToVector(DenseTensor<float> t)
        {
            // Accepts [1, D] or [D] shapes
            var dims = t.Dimensions.ToArray();
            var d = dims.Aggregate(1, (a, b) => a * b);
            var vec = new float[d];
            t.Buffer.Span.CopyTo(vec);
            return vec;
        }

        static void NormalizeInPlace(float[] v)
        {
            double sumSq = 0;
            for (var i = 0; i < v.Length; i++) sumSq += (double)v[i] * v[i];
            var norm = (float)Math.Sqrt(sumSq);
            if (norm <= 0) return;
            for (var i = 0; i < v.Length; i++) v[i] /= norm;
        }

        static float Cosine(float[] a, float[] b)
        {
            var n = Math.Min(a.Length, b.Length);
            double dot = 0;
            for (var i = 0; i < n; i++) dot += (double)a[i] * b[i];
            return (float)dot; // since both are L2-normalized, cosine = dot
        }

        static bool IsUnder(string path, string folder)
        {
            var p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            var f = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            return p.StartsWith(f, StringComparison.OrdinalIgnoreCase);
        }
    }
}
