========================================
# Secura — Advanced File Sharing System
========================================
Secura is a secure, cloud-ready file sharing platform that supports large file uploads, chunk-based transfer, pause/resume functionality, and shareable links.

## Features

* Upload large files (500MB+ supported)
* Chunk-based upload system
* Resume upload after refresh or failure
* Pause and resume uploads
* Parallel chunk upload for improved performance
* JWT-based authentication
* Share files via secure links (with expiry and password options)
* File management (rename, delete, preview, download)
* Search and filter functionality
* Drag and drop file upload interface


## How It Works

# Upload Flow

1. File is split into chunks (5MB each)
2. Chunks are uploaded in parallel
3. Upload progress is tracked in real time
4. Upload can be paused or resumed
5. After all chunks are uploaded, the server merges them
6. File metadata is stored in the database

----------------------------------------------------------------

## Tech Stack

# Frontend

* HTML
* CSS
* JavaScript (Vanilla)

# Backend

* ASP.NET Core Web API
* SQL Server
* JWT Authentication

# Storage

* Local storage (designed to support cloud integration)

----------------------------------------------------------------

## Architecture Diagram

User (Browser)
     |
     v
Frontend (HTML + JS)
     |
     |  Split file into chunks (5MB each)
     v
Upload Chunks (parallel)
     |
     v
Backend API (/upload-chunk)
     |
     v
Temp Storage (/temp folder)
     |
     |  After all chunks uploaded
     v
Merge API (/merge)
     |
     v
Final File (/uploads folder)
     |
     +-------------------+
     |                   |
     v                   v
Database (SQL)     File Access APIs
(metadata)         (download / preview / share)
     |                   |
     +---------+---------+
               v
           Frontend UI

---------------------------------------------------------------
## Security

* JWT authentication for protected endpoints
* File access restricted per user
* Share links with optional expiry and password protection

----------------------------------------------------------------

## API Endpoints

| Method | Endpoint       | Description           |
| ------ | -------------- | --------------------- |
| POST   | /upload-chunk  | Upload file chunk     |
| POST   | /merge         | Merge uploaded chunks |
| GET    | /all           | Get user files        |
| GET    | /download/{id} | Download file         |
| DELETE | /{id}          | Delete file           |
| POST   | /share/{id}    | Generate share link   |

--------------------------------------------------

## Future Improvements (currently working on)

* Cloud storage integration (Azure or Supabase)
* Folder system
* Multiple file upload support
* Upload speed and time estimation 
* CDN integration

--------------------------------------------------

## Project Highlights

* Efficient handling of large file uploads
* Scalable upload architecture using chunking
* Implementation of pause and resume functionality
* Secure file access with authentication
* Full-stack integration

--------------------------------------------------

## Author

Mohit Kumar

## License

This project is open for learning and personal use.
