// ZooTrack.WebAPI/Controllers/CameraController.cs
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ZooTrack.Services;

namespace ZooTrackBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CameraController : ControllerBase
    {
        private readonly CameraService _cameraService;
        public CameraController(CameraService cameraService)
        {
            _cameraService = cameraService;
        }

        // GET: api/camera/stream
        [HttpGet("stream")]
        public async Task Stream()
        {
            HttpContext.Response.ContentType = "multipart/x-mixed-replace; boundary=--frame";
            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                byte[] frame = _cameraService.GetFrame();
                if (frame != null)
                {
                    await HttpContext.Response.WriteAsync("--frame\r\n");
                    await HttpContext.Response.WriteAsync("Content-Type: image/jpeg\r\n");
                    await HttpContext.Response.WriteAsync($"Content-Length: {frame.Length}\r\n\r\n");
                    await HttpContext.Response.Body.WriteAsync(frame, 0, frame.Length);
                    await HttpContext.Response.WriteAsync("\r\n");
                    await HttpContext.Response.Body.FlushAsync();
                }
                await Task.Delay(50); // ~20 FPS
            }
        }

        // POST: api/camera/highlight/start
        [HttpPost("highlight/start")]
        public IActionResult StartHighlight()
        {
            _cameraService.StartRecording();
            return Ok(new { message = "Highlight recording started" });
        }

        // POST: api/camera/highlight/stop
        [HttpPost("highlight/stop")]
        public IActionResult StopHighlight()
        {
            _cameraService.StopRecording();
            return Ok(new { message = "Highlight recording stopped" });
        }

        // GET: api/camera/highlight
        [HttpGet("highlight")]
        public IActionResult GetHighlight()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "highlight.avi");
            if (!System.IO.File.Exists(path))
                return NotFound("Highlight video not found.");

            // Return file as download
            return PhysicalFile(path, "video/avi", "highlight.avi");
        }
    }
}
