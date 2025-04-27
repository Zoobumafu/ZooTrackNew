using ZooTrack.Data;
using ZooTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace ZooTrack.Services
{
    public class DetectionMediaService
    {
        /* This service will:
         *      1. listen to detection events
         *      2. extract frames from media at low fps
         *      3. save only those frames into disk
         *      4. register them into Media table connected to the Detection
         */

        // TODO: call helper to actually extract frames

        private readonly ZootrackDbContext _context;
        private readonly IWebHostEnvironment _environment;
        
        public DetectionMediaService(ZootrackDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task ExtractFramesAsync(Detection detection)
        {
            // load media related to detection
            var media = await _context.Media.FindAsync(detection.MediaId);
            if (media == null)
                throw new Exception($"Media with this ID {detection.MediaId} not found.");

            // find full path
            var mediaPath = Path.Combine(_environment.ContentRootPath, "MediaFiles", media.FilePath);
            if (!File.Exists(mediaPath))
                throw new Exception($"Media file not found: {mediaPath}.");
        
            // create new directory
            var outputDir = Path.Combine(_environment.ContentRootPath, "SavedDetections", detection.MediaId.ToString());
            Directory.CreateDirectory(outputDir);

            // TODO: call helper to actually extract frames
            await ExtractFramesFromVideoAsync(mediaPath, outputDir);

            await _context.SaveChangesAsync();
        }

        private async Task ExtractFramesFromVideoAsync(string videoPath, string outputDir)
        {
            // For now just placeholder
            // Later will use libraries such as: OpenCvSharp, FFmpeg wrappers, etc.

            await Task.Delay(100);
            Console.WriteLine($"[simulated] Extracting frames from {videoPath} to {outputDir}");
        }


    }
}
