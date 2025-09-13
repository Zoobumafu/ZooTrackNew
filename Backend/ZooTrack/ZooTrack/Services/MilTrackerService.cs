using OpenCvSharp;
using OpenCvSharp.Tracking;

namespace ZooTrackBackend.Services
{
    public class MilTrackerService
    {
        private TrackerMIL _tracker;
        private bool _isInitialized = false;

        public MilTrackerService()
        {
            _tracker = TrackerMIL.Create();
        }

        /// <summary>
        /// Initialize the tracker with the first frame and bounding box
        /// </summary>
        /// <param name="frame">Initial frame (Mat)</param>
        /// <param name="boundingBox">Initial bounding box as Rect2d</param>
        /// <returns>True if initialization successful</returns>
        public bool Initialize(Mat frame, Rect boundingBox)
        {
            if (frame == null || frame.Empty())
                return false;

            try
            {
                _tracker.Init(frame, boundingBox);
                _isInitialized = true;
                return _isInitialized;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tracker initialization failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Update the tracker with a new frame
        /// </summary>
        /// <param name="frame">New frame to process</param>
        /// <returns>Updated bounding box, or null if tracking failed</returns>
        public Rect? Update(Mat frame)
        {
            if (!_isInitialized || frame == null || frame.Empty())
                return null;

            try
            {
                var boundingBox = new Rect();
                bool success = _tracker.Update(frame, ref boundingBox);

                if (success)
                    return boundingBox;
                else
                    return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tracker update failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reset the tracker (for reinitialization)
        /// </summary>
        public void Reset()
        {
            _tracker?.Dispose();
            _tracker = TrackerMIL.Create();
            _isInitialized = false;
        }

        /// <summary>
        /// Check if tracker is initialized and ready
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Convert Rect to Rect2d (helper method)
        /// </summary>
        /// <param name="rect">Standard Rect</param>
        /// <returns>Rect2d</returns>
        public static Rect2d RectToRect2d(Rect rect)
        {
            return new Rect2d(rect.X, rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// Convert Rect2d to Rect (helper method)
        /// </summary>
        /// <param name="rect2d">Rect2d</param>
        /// <returns>Standard Rect</returns>
        public static Rect Rect2dToRect(Rect2d rect2d)
        {
            return new Rect((int)rect2d.X, (int)rect2d.Y, (int)rect2d.Width, (int)rect2d.Height);
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            _tracker?.Dispose();
        }
    }
}