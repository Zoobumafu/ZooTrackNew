# ZooTrack 🐾

> An intelligent wildlife monitoring and tracking platform featuring real-time YOLOv10 object detection, continuous MIL tracking, and a live streaming web dashboard.

ZooTrack is a comprehensive, full-stack monitoring system designed to analyze live camera feeds for specific animal species. It processes video streams in real-time, detects target animals, tracks their movement paths across frames, automatically records highlight clips, and provides administrators with a centralized dashboard for statistics, alerts, and historical data.

---

## 🚀 Key Features

* **Real-Time Object Detection:** Utilizes a custom-trained YOLOv10 model via ONNX runtime to detect specific animal species (e.g., lions, elephants, tigers) with high accuracy.
* **Continuous Object Tracking:** Implements OpenCV's Multiple Instance Learning (MIL) tracking to follow animals between detection frames, calculating Intersection over Union (IoU) to maintain persistent object IDs and map movement routes.
* **Live Video Streaming:** Uses SignalR WebSockets to stream processed frames with bounding boxes directly to the client with minimal latency.
* **Automated Highlight Recording:** Automatically extracts frames and saves video clips (configurable time windows) when target animals are detected, logging them as distinct `Events`.
* **Interactive Dashboard:** A comprehensive Blazor-based frontend featuring live camera feeds, system logs, device health status, and statistical heatmaps.
* **Role-Based Access Control (RBAC):** Secure JWT authentication with distinct user roles (Admin, Zoo Manager, Wildlife Observer) and customizable notification preferences.

---

## 💻 Tech Stack

| Layer | Technologies Used |
| :--- | :--- |
| **Frontend** | Blazor (WebAssembly/Server), HTML/CSS, C# |
| **Backend API** | ASP.NET Core Web API, C# |
| **Real-Time Comm.** | SignalR (WebSockets) |
| **Database & ORM** | Entity Framework Core, SQL/SQLite |
| **Computer Vision** | OpenCVSharp, YoloDotNet |
| **Machine Learning** | Ultralytics YOLOv10, PyTorch, ONNX, Roboflow |

---

## 🧠 Machine Learning Pipeline

The detection engine is powered by a **YOLOv10** model optimized for wildlife recognition.

1.  **Dataset Preparation:** The dataset was curated and preprocessed using Roboflow, featuring bounding box annotations for various target species.
2.  **Model Training:** Trained using the Ultralytics framework (`yolov10n.pt` / `yolov10s.pt` base models) with customized hyperparameters (e.g., 50 epochs, mosaic augmentation, bounding box loss optimization).
3.  **Tracking & Validation:** Training metrics were logged and validated using Weights & Biases (wandb).
4.  **Export & Inference:** The final trained model was exported to the `.onnx` format, allowing for seamless, dependency-light integration into the C# backend using `YoloDotNet`.

---

## ⚙️ Core Architecture

The system is highly asynchronous, separating frame ingestion, AI inference, and database logging to maintain high frames-per-second (FPS).

* **CameraService:** Manages physical/IP camera connections via OpenCV `VideoCapture`. It processes frames through the ONNX model, draws bounding boxes, and handles local highlight recording.
* **DetectionMediaService & MilTrackerService:** When an animal is detected, these services take over. The MIL Tracker predicts the animal's location in subsequent frames without running the heavy YOLO model every millisecond. IoU calculations determine if a detection is a new animal or an existing tracked object.
* **SignalR Hub:** Processed frames (encoded to JPEG) and status updates are broadcast to connected Blazor clients in real-time.
* **DashboardService:** Aggregates analytics, calculates uptime, counts true/false positives, and serves data to the frontend widgets.

---

## 🛠️ Getting Started

### Prerequisites
* .NET 8.0 SDK or later
* Python 3.10+ (If re-training the YOLO model)
* A webcam or accessible IP camera stream
* Visual Studio 2022 or VS Code

### Installation & Setup

1.  **Clone the repository:**
    ```bash
    git clone [https://github.com/YourUsername/ZooTrack.git](https://github.com/YourUsername/ZooTrack.git)
    cd ZooTrack
    ```

2.  **Add the ML Model:**
    Place your exported `yolov10.onnx` file into the `Models` directory of the backend project. 
    *(Note: You can run the `Training.ipynb` notebook to train your own custom model).*

3.  **Database Migration:**
    Apply the Entity Framework migrations to build the database schema and insert the seed data (Default Admin/Manager accounts, test devices, etc.).
    ```bash
    dotnet ef database update
    ```

4.  **Run the Backend:**
    ```bash
    cd ZooTrack.Backend
    dotnet run
    ```

5.  **Run the Frontend:**
    ```bash
    cd ZooTrack.Client
    dotnet run
    ```

### Default Credentials
Upon database creation, the following seed accounts are available:
* **Admin:** `Admin` / *(Check DbContext seed for password)*
* **Manager:** `manager@zootrack.com`
* **Observer:** `observer@zootrack.com`

---

## 📂 Project Structure

* `/Controllers`: RESTful API endpoints for managing devices, users, logs, and statistics.
* `/Services`: Core business logic (Camera processing, MIL tracking, Notifications).
* `/Hubs`: SignalR endpoints for live video streaming.
* `/Models`: Shared data models and Entity Framework schema.
* `/Client`: Blazor frontend pages, UI components, and ViewModels.
* `/ML`: Jupyter notebooks and scripts for YOLOv10 dataset preparation and training.

---

*Designed and developed as a capstone project demonstrating advanced full-stack integration and applied computer vision.*
