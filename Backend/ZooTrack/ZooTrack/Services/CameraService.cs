// ZooTrack.WebAPI/Services/CameraService.cs
using System;
using System.IO; // Added for Path.Combine
using OpenCvSharp;
using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using SkiaSharp;

namespace ZooTrack.Services
{
    public class CameraService : IDisposable // Implement IDisposable
    {
        private VideoCapture? capture; // Nullable
        private Yolo? yolo; // Nullable
        private VideoWriter? writer; // Nullable
        private bool recording = false;
        private int width = 640; // Default width
        private int height = 480; // Default height
        private readonly string highlightPath = "highlight.avi";

        public CameraService()
        {
            try
            {
                // Initialize camera (device 0)
                capture = new VideoCapture(0);
                if (!capture.Open(0))
                {
                    Console.WriteLine("Error: Could not open camera.");
                    // Handle camera opening failure (e.g., throw exception, log error)
                    capture = null; // Ensure capture is null if opening failed
                    return;
                }

                // Set desired resolution (optional, camera might support specific resolutions)
                capture.FrameWidth = width;
                capture.FrameHeight = height;

                // Read actual resolution after setting
                width = capture.FrameWidth;
                height = capture.FrameHeight;

                Console.WriteLine($"Camera opened successfully. Resolution: {width}x{height}");

                // Load YOLOv10 ONNX model
                // Ensure 'Models' directory exists and 'yolov10.onnx' is inside
                string modelDirectory = Path.Combine(AppContext.BaseDirectory, "Models");
                string modelPath = Path.Combine(modelDirectory, "yolov10.onnx");

                if (!Directory.Exists(modelDirectory))
                {
                    Console.WriteLine($"Error: Model directory not found at {modelDirectory}");
                    // Handle directory not found
                    return;
                }
                if (!File.Exists(modelPath))
                {
                    Console.WriteLine($"Error: YOLO model not found at {modelPath}");
                    // Handle model file not found
                    return;
                }

                yolo = new Yolo(new YoloOptions
                {
                    OnnxModel = modelPath,
                    ModelType = ModelType.ObjectDetection,
                    Cuda = false // Set to true if GPU is available and configured
                });

                Console.WriteLine("YOLO model loaded successfully.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing CameraService: {ex.Message}");
                // Clean up resources if initialization fails partially
                capture?.Release();
                capture = null;
                yolo = null;
                // Rethrow or handle as appropriate for your application
                throw;
            }
        }

        // Start saving highlight video
        public void StartRecording()
        {
            if (!recording && capture != null && capture.IsOpened()) // Check if capture is valid
            {
                // Use MJPG codec for AVI
                int fourcc = VideoWriter.FourCC('M', 'J', 'P', 'G');
                // Get FPS from camera if possible, otherwise default to 20
                double fps = capture.Get(VideoCaptureProperties.Fps);
                if (fps <= 0) fps = 20.0; // Default FPS if unable to get from camera

                writer = new VideoWriter(highlightPath, fourcc, fps, new OpenCvSharp.Size(width, height));
                if (!writer.IsOpened())
                {
                    Console.WriteLine($"Error: Could not open VideoWriter for path {highlightPath}");
                    writer = null; // Ensure writer is null if opening failed
                    return;
                }
                recording = true;
                Console.WriteLine($"Started recording highlight video to {highlightPath} at {fps} FPS.");
            }
            else if (capture == null || !capture.IsOpened())
            {
                Console.WriteLine("Cannot start recording: Camera not initialized or opened.");
            }
            else
            {
                Console.WriteLine("Recording is already in progress.");
            }
        }

        // Stop and finalize highlight video
        public void StopRecording()
        {
            if (recording)
            {
                recording = false;
                writer?.Release(); // Safely release the writer if it exists
                writer = null; // Set to null after release
                Console.WriteLine("Stopped recording highlight video.");
            }
            else
            {
                Console.WriteLine("Recording is not currently active.");
            }
        }

        // Capture one frame, run detection, draw results, and return JPEG bytes
        public byte[]? GetFrame()
        {
            // Ensure camera and YOLO model are initialized
            if (capture == null || !capture.IsOpened() || yolo == null)
            {
                Console.WriteLine("Camera or YOLO model not initialized.");
                // Return a placeholder image or null
                // Creating a simple black placeholder frame
                using var placeholder = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.Black);
                Cv2.PutText(placeholder, "Camera/Model Error", new Point(10, height / 2), HersheyFonts.HersheySimplex, 1.0, Scalar.White, 2);
                Cv2.ImEncode(".jpg", placeholder, out byte[] placeholderBytes);
                return placeholderBytes;
                // return null; // Or return null if preferred
            }

            using var frame = new Mat();
            // Try reading a frame
            if (!capture.Read(frame) || frame.Empty())
            {
                Console.WriteLine("Warning: Could not read frame from camera.");
                return null; // Return null if reading fails
            }

            // Encode frame to JPEG format in memory
            Cv2.ImEncode(".jpg", frame, out byte[] rawData);

            // Use SkiaSharp to decode the JPEG for YOLO input
            using var skImage = SKImage.FromEncodedData(rawData);
            if (skImage == null)
            {
                Console.WriteLine("Warning: Could not decode frame using SkiaSharp.");
                // Return the raw frame encoded as JPEG if SkiaSharp fails
                return rawData;
            }

            try
            {
                // Run YOLO object detection inference
                var results = yolo.RunObjectDetection(skImage, confidence: 0.25f, iou: 0.7f);

                // Draw detection results onto the OpenCV frame
                foreach (var res in results) // 'res' is of type YoloDotNet.Models.ObjectDetection
                {
                    // *** THE FIX IS HERE ***
                    // Access the BoundingBox property instead of Rectangle
                    var rect = res.BoundingBox;

                    // Extract coordinates, ensuring they are within frame bounds
                    int x = Math.Max(0, (int)rect.Left);
                    int y = Math.Max(0, (int)rect.Top);
                    int w = Math.Min(width - x, (int)(rect.Right - rect.Left)); // Ensure width doesn't exceed frame boundary
                    int h = Math.Min(height - y, (int)(rect.Bottom - rect.Top)); // Ensure height doesn't exceed frame boundary

                    // Ensure width and height are positive
                    if (w <= 0 || h <= 0) continue;

                    string label = res.Label.Name;
                    float conf = (float)res.Confidence;

                    // Draw bounding box rectangle on the frame
                    Cv2.Rectangle(frame, new Rect(x, y, w, h), Scalar.Red, 2);

                    // Prepare text label with confidence score
                    string text = $"{label} {conf:P1}"; // Format confidence as percentage (e.g., 75.2%)
                    var textSize = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, 0.6, 2, out var baseline);

                    // Draw a filled background rectangle for the text for better visibility
                    int textY = y - 5; // Position text slightly above the box
                    int textBgY = textY - textSize.Height; // Background position
                    // Adjust if text goes above the top edge
                    if (textBgY < 0)
                    {
                        textY = y + h + textSize.Height + 5; // Place below the box if it overflows top
                        textBgY = y + h + 5;
                    }
                    Cv2.Rectangle(frame, new Rect(x, textBgY, textSize.Width, textSize.Height + baseline), Scalar.Green, -1); // -1 for filled

                    // Put the text label on the frame
                    Cv2.PutText(frame, text, new Point(x, textY),
                        HersheyFonts.HersheySimplex, 0.6, Scalar.Black, 2); // Black text on green background
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during YOLO detection or drawing: {ex.Message}");
                // Optionally draw an error message on the frame
                Cv2.PutText(frame, "Detection Error", new Point(10, 30), HersheyFonts.HersheySimplex, 1.0, Scalar.Magenta, 2);
            }


            // If recording is active, write the annotated frame to the video file
            if (recording && writer != null && writer.IsOpened())
            {
                try
                {
                    writer.Write(frame);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing frame to video: {ex.Message}");
                    // Consider stopping recording or handling the error
                }
            }

            // Encode the final frame (with annotations) to JPEG format for streaming
            Cv2.ImEncode(".jpg", frame, out byte[] jpegBytes);
            return jpegBytes;
        }

        // Implement IDisposable to release resources
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Prevent finalizer from running
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Console.WriteLine("Disposing CameraService resources...");
                // Stop recording if active
                StopRecording();

                // Release VideoCapture resource
                capture?.Release();
                capture = null;

                // Yolo object doesn't seem to have an explicit Dispose method in YoloDotNet
                // Rely on garbage collection for it, unless the library documentation specifies otherwise.
                yolo = null;
                Console.WriteLine("CameraService resources disposed.");
            }
        }

        // Finalizer (just in case Dispose is not called)
        ~CameraService()
        {
            Dispose(false); // Release unmanaged resources
        }
    }
}
