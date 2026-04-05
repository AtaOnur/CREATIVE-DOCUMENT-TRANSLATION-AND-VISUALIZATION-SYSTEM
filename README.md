🚀 Creative Document Translation and Visualization System

An ASP.NET Core MVC-based document processing application developed as a graduation project.
This project demonstrates a simple yet functional pipeline for PDF processing, OCR, and AI-based text transformations.

🎓 Graduation Project Note

This repository is designed as a Minimum Viable Product (MVP) for a final-year project presentation.
The architecture and codebase are intentionally kept simple, readable, and easy to explain.

✨ Features
📄 Upload PDF documents
🔍 Select a region inside the PDF and extract text via OCR
🤖 Perform AI-based operations (mock):
Translation
Summarization
Rewriting
Creative writing
Visualization
📝 Save results to notebook/history
⚙️ User settings management
👤 Demo account & pre-seeded data

🏗️ Architecture Overview
📌 Controllers
Home
Account
Dashboard
Documents
Ai
Notebook
Settings
⚙️ Services
IDocumentService / DocumentService
→ Handles document, OCR, AI results, notebook, and settings logic
IOcrService
MockOcrService
TesseractCliOcrService
→ OCR processing for selected PDF regions
IAiService / MockAiService
→ AI processing (mock implementation)
ISimpleAccountStore / SimpleAccountStore
→ Lightweight demo authentication

💾 Data Layer
Database: SQLite (Data/app.db)
ORM: Entity Framework Core

📊 Entities
Document
ActivityEntry
OcrResult
AiResult
UserSettings

🖥️ Frontend
Bootstrap 5
PDF.js (client-side rendering)
Vanilla JavaScript (workspace & loading states)

🛠️ Technologies Used
ASP.NET Core MVC (.NET 8)
Entity Framework Core
SQLite
Bootstrap 5
PDF.js
Tesseract OCR (optional)
Poppler (pdftoppm)

⚡ Getting Started
🔧 Prerequisites
.NET 8 SDK installed
▶️ Run the Project:

dotnet restore
dotnet run

Open in browser:

https://localhost:xxxx


🔍 Optional: Enable Real OCR (Tesseract)

By default, OCR runs in mock mode.

To enable real OCR:

Install Tesseract OCR (Windows)
Install Poppler (for pdftoppm)
Add executables to PATH
or define paths in appsettings.json:
"Ocr": {
  "UseMock": false,
  "Language": "tur+eng",
  "TesseractPath": "C:\\path\\to\\tesseract.exe",
  "PdfToPpmPath": "C:\\path\\to\\pdftoppm.exe"
}
Restart the application

🗄️ Database Info
📁 DB file: Data/app.db
📂 Upload folder: Data/uploads/
Database initialized with EnsureCreated() at startup

🧪 Demo Data

Pre-seeded data includes:

👤 Demo Account

demo@university.edu
password: demo123
📄 1 demo PDF
📝 1 notebook entry
📊 1 activity record
⚙️ Default user settings

settings
🎨 UI / UX Highlights
Shared layout (navbar & footer)
Reusable alert component (_AppAlerts.cshtml)
Loading states for POST actions
Empty state UI for pages
Clean and consistent validation feedback

⚠️ Limitations
AI layer is mock-based
Real OCR requires local Tesseract + Poppler installation
Uses EnsureCreated() instead of migrations
Simple authentication (no ASP.NET Identity)

🚫 Out of Scope (This Version)
Team collaboration features
Admin panel
Advanced AI provider orchestration
Queue / background processing systems

📌 Future Improvements (Optional Ideas)
Replace mock AI with real LLM APIs
Add ASP.NET Identity authentication
Introduce background job processing (e.g., Hangfire)
Support multi-user collaboration
Improve OCR accuracy with preprocessing

📄 License

This project is developed for academic purposes.
