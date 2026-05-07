using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Server;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

[Authorize] // 🔥 SECURE ALL ENDPOINTS
[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IConfiguration _config;

    public FileController(IConfiguration config)
    {
        _config = config;
    }

    // 🔥 GET USER ID
    private int GetUserId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "id");
        if (claim == null) throw new Exception("User ID missing");

        return int.Parse(claim.Value);
    }

    // ================== UPLOAD ==================
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1073741824)] // 1 GB
    [RequestFormLimits(MultipartBodyLengthLimit = 1073741824)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new
                {
                    code = "NO_FILE",
                    message = "No file selected"
                });

            // 🔥 optional size check
            if (file.Length > 1073741824) // 1GB
                return BadRequest(new
                {
                    code = "FILE_TOO_LARGE",
                    message = "File exceeds 1GB limit"
                });

            int userId = GetUserId();

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string storedName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            string fullPath = Path.Combine(folder, storedName);

            // 🔥 async stream (CRITICAL for big files)
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

            string q = "INSERT INTO Files (FileName, FilePath, UserId) VALUES (@n,@p,@u)";
            using var cmd = new SqlCommand(q, con);

            cmd.Parameters.AddWithValue("@n", file.FileName);
            cmd.Parameters.AddWithValue("@p", storedName);
            cmd.Parameters.AddWithValue("@u", userId);

            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return Ok(new
            {
                message = "Uploaded successfully",
                size = file.Length
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                code = "UPLOAD_FAILED",
                message = "Upload failed",
                detail = ex.Message
            });
        }
    }

    // ================== GET ALL ==================
    [HttpGet("all")]
    public IActionResult GetAll()
    {
        int userId = GetUserId();

        var list = new List<object>();

        using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

        string q = @"SELECT f.Id, f.FileName, f.FilePath, f.UploadedAt, u.Username
FROM Files f
JOIN Users u ON f.UserId = u.Id
WHERE f.UserId = @u";
        using var cmd = new SqlCommand(q, con);

        cmd.Parameters.AddWithValue("@u", userId);

        con.Open();
        var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            string storedName = reader["FilePath"].ToString();
            string path = Path.Combine(Directory.GetCurrentDirectory(), "uploads", storedName);

            long sizeBytes = 0;

            if (System.IO.File.Exists(path))
            {
                sizeBytes = new FileInfo(path).Length;
            }

            string sizeDisplay = sizeBytes < 1024 * 1024
                ? $"{sizeBytes / 1024} KB"
                : $"{sizeBytes / (1024 * 1024)} MB";

            list.Add(new
            {
                id = reader["Id"],
                name = reader["FileName"],
                userName = reader["Username"],  // ✅ comma added
                size = sizeDisplay,
                uploaded = reader["UploadedAt"]
            });
        }

        return Ok(list);
    }

    // ================== DOWNLOAD ==================
    [HttpGet("download/{id}")]
    public IActionResult Download(int id)
    {
        try
        {
            int userId = GetUserId();

            string originalName = "";
            string storedName = "";

            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

            string q = "SELECT FileName, FilePath FROM Files WHERE Id=@id AND UserId=@u";
            using var cmd = new SqlCommand(q, con);

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@u", userId);

            con.Open();
            var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return NotFound("File not found or access denied");

            originalName = reader["FileName"].ToString();
            storedName = reader["FilePath"].ToString();

            string path = Path.Combine(Directory.GetCurrentDirectory(), "uploads", storedName);

            if (!System.IO.File.Exists(path))
                return NotFound("File missing");

            var fileBytes = System.IO.File.ReadAllBytes(path);

            Response.Headers["Content-Disposition"] =
                $"attachment; filename=\"{originalName}\"";

            return File(fileBytes, "application/octet-stream");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    // ================== DELETE ==================
    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        try
        {
            int userId = GetUserId();

            string storedName = "";

            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

            // 🔥 CHECK OWNERSHIP
            string check = "SELECT FilePath FROM Files WHERE Id=@id AND UserId=@u";
            using var checkCmd = new SqlCommand(check, con);

            checkCmd.Parameters.AddWithValue("@id", id);
            checkCmd.Parameters.AddWithValue("@u", userId);

            con.Open();
            var result = checkCmd.ExecuteScalar();

            if (result == null)
                return Unauthorized("You cannot delete this file");

            storedName = result.ToString();

            // 🔥 DELETE FILE FROM DISK
            string path = Path.Combine(Directory.GetCurrentDirectory(), "uploads", storedName);
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);

            // 🔥 DELETE FROM DB
            string del = "DELETE FROM Files WHERE Id=@id AND UserId=@u";
            using var delCmd = new SqlCommand(del, con);

            delCmd.Parameters.AddWithValue("@id", id);
            delCmd.Parameters.AddWithValue("@u", userId);

            delCmd.ExecuteNonQuery();

            return Ok("Deleted");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }
    // ================== PREVIEW ==================
    [HttpGet("preview/{id}")]
    public IActionResult Preview(int id)
    {
        try
        {
            int userId = GetUserId();

            string originalName = "";
            string storedName = "";

            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

            string q = "SELECT FileName, FilePath FROM Files WHERE Id=@id AND UserId=@u";
            using var cmd = new SqlCommand(q, con);

            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@u", userId);

            con.Open();
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
                return NotFound("File not found or access denied");

            originalName = reader["FileName"].ToString();
            storedName = reader["FilePath"].ToString();

            string path = Path.Combine(Directory.GetCurrentDirectory(), "uploads", storedName);

            if (!System.IO.File.Exists(path))
                return NotFound("File missing");

            // 🔥 BLOCK NON-PREVIEWABLE FILES
            string ext = Path.GetExtension(originalName).ToLower();

            var blocked = new[] { ".exe", ".zip", ".rar", ".msi", ".bat" };

            if (blocked.Contains(ext))
                return BadRequest("Preview not supported for this file type");

            // 🔥 ALLOWED PREVIEW TYPES ONLY
            var allowed = new Dictionary<string, string>
            {
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".gif"] = "image/gif",
                [".pdf"] = "application/pdf",
                [".mp4"] = "video/mp4"
            };

            if (!allowed.ContainsKey(ext))
                return BadRequest("Preview not supported for this file type");

            string contentType = allowed[ext];

            // 🔥 RETURN INLINE (IMPORTANT)
            Response.Headers["Content-Disposition"] = "inline";

            return PhysicalFile(path, contentType);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Preview failed: " + ex.Message);
        }
    }

    //===== create share ===

    [HttpPost("share/{fileId}")]
    public IActionResult CreateShare(
    int fileId,
    [FromQuery] int? expiryMinutes,
    [FromQuery] string? password)
    {
        string token = Guid.NewGuid().ToString("N");

        DateTime? expiry = expiryMinutes.HasValue
            ? DateTime.Now.AddMinutes(expiryMinutes.Value)
            : null;

        string? hash = null;
        if (!string.IsNullOrEmpty(password))
            hash = BCrypt.Net.BCrypt.HashPassword(password);

        string conStr = _config.GetConnectionString("DefaultConnection");

        using var con = new SqlConnection(conStr);

        // 🔥 CHECK FILE EXISTS
        string checkQuery = "SELECT COUNT(*) FROM Files WHERE Id=@id";
        using var checkCmd = new SqlCommand(checkQuery, con);
        checkCmd.Parameters.AddWithValue("@id", fileId);

        con.Open();

        int exists = (int)checkCmd.ExecuteScalar();

        if (exists == 0)
            return NotFound("File does not exist");

        // 🔥 INSERT SHARE
        string q = @"INSERT INTO FileShares (FileId, ShareToken, Expiry, PasswordHash)
                 VALUES (@f, @t, @e, @p)";

        using var cmd = new SqlCommand(q, con);
        cmd.Parameters.AddWithValue("@f", fileId);
        cmd.Parameters.AddWithValue("@t", token);
        cmd.Parameters.AddWithValue("@e", (object?)expiry ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@p", (object?)hash ?? DBNull.Value);

        cmd.ExecuteNonQuery();

        return Ok(new
        {
            url = $"{Request.Scheme}://{Request.Host}/api/File/share/{token}"
        });
    }


    [AllowAnonymous]
    [HttpGet("share/{token}")]
    public IActionResult DownloadShared(string token, [FromQuery] string? password)
    {
        try
        {
            string conStr = _config.GetConnectionString("DefaultConnection");

            int fileId = 0;
            DateTime? expiry = null;
            string? hash = null;

            // ===== GET SHARE DATA =====
            using (var con = new SqlConnection(conStr))
            {
                string q = "SELECT FileId, Expiry, PasswordHash FROM FileShares WHERE ShareToken=@t";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@t", token);

                con.Open();
                using var r = cmd.ExecuteReader();

                if (!r.Read())
                    return NotFound("Invalid link");

                fileId = Convert.ToInt32(r["FileId"]);

                if (r["Expiry"] != DBNull.Value)
                    expiry = Convert.ToDateTime(r["Expiry"]);

                if (r["PasswordHash"] != DBNull.Value)
                    hash = r["PasswordHash"].ToString();
            }

            // ===== EXPIRY CHECK =====
            if (expiry.HasValue && DateTime.Now > expiry)
                return Content("Link expired");

            // ===== GET FILE INFO =====
            string originalName = "";
            string storedName = "";

            using (var con = new SqlConnection(conStr))
            {
                string q = "SELECT FileName, FilePath FROM Files WHERE Id=@id";
                using var cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@id", fileId);

                con.Open();
                using var r = cmd.ExecuteReader();

                if (!r.Read())
                    return NotFound("File missing");

                originalName = r["FileName"].ToString();
                storedName = r["FilePath"].ToString();
            }

            // ===== PATH + SIZE =====
            string path = Path.Combine(Directory.GetCurrentDirectory(), "uploads", storedName);

            if (!System.IO.File.Exists(path))
                return NotFound("File missing");

            var fileInfo = new FileInfo(path);
            long sizeBytes = fileInfo.Length;

            string sizeDisplay = sizeBytes < 1024 * 1024
                ? $"{sizeBytes / 1024} KB"
                : $"{sizeBytes / (1024 * 1024)} MB";

            // ===== PASSWORD UI =====
            if (hash != null)
            {
                if (string.IsNullOrEmpty(password))
                {
                    return Content($@"
<html>
<body style='font-family:Arial; background:#020617; color:white; display:flex; justify-content:center; align-items:center; height:100vh;'>

    <div style='background:#1e293b; padding:30px; border-radius:16px; width:350px; text-align:center;'>

        <h2>Shared File</h2>

        <div style='margin:15px 0; font-size:14px; color:#94a3b8;'>
            <div><b>Name:</b> {originalName}</div>
            <div><b>Size:</b> {sizeDisplay}</div>
        </div>

        <input id='pass' type='password' placeholder='Enter password'
            style='padding:10px; border-radius:8px; border:none; width:100%; margin-bottom:10px; background:#0f172a; color:white;' />

        <button onclick='go()' style='padding:10px; width:100%; border:none; border-radius:8px; background:#3b82f6; color:white;'>
            Download
        </button>

        <script>
            function go() {{
                let p = document.getElementById('pass').value;
                window.location.href = '?password=' + encodeURIComponent(p);
            }}
        </script>

    </div>

</body>
</html>
", "text/html");
                }

                if (!BCrypt.Net.BCrypt.Verify(password, hash))
                    return Content("Wrong password");
            }

            // ===== FINAL DOWNLOAD =====
            var fileBytes = System.IO.File.ReadAllBytes(path);

            var encodedName = Uri.EscapeDataString(originalName);

            Response.Headers["Content-Disposition"] =
                $"attachment; filename=\"{originalName}\"; filename*=UTF-8''{encodedName}";

            return File(fileBytes, "application/octet-stream");
        }
        catch (Exception ex)
        {
            return Content("ERROR: " + ex.Message);
        }
    }



    //=== test ===

    [AllowAnonymous]
    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok("Working");
    }

    // ======  rename 

    [HttpPut("rename/{id}")]
    public IActionResult Rename(int id, [FromBody] string newName)
    {
        try
        {
            int userId = int.Parse(User.FindFirst("id").Value);

            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

            string q = "UPDATE Files SET FileName=@n WHERE Id=@id AND UserId=@u";
            using var cmd = new SqlCommand(q, con);

            cmd.Parameters.AddWithValue("@n", newName);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@u", userId);

            con.Open();

            int rows = cmd.ExecuteNonQuery();

            if (rows == 0)
                return NotFound("File not found");

            return Ok("Renamed");
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    //=== merge 
    public class MergeDto
    {
        public string fileName { get; set; }
        public int totalChunks { get; set; }
    }

    [HttpPost("merge")]
    
    public async Task<IActionResult> Merge([FromBody] MergeDto dto)
    {
        try
        {
            string tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "temp");
            string finalFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");

            if (!Directory.Exists(finalFolder))
                Directory.CreateDirectory(finalFolder);

            string storedName = Guid.NewGuid() + Path.GetExtension(dto.fileName);
            string finalPath = Path.Combine(finalFolder, storedName);

            using (var finalStream = new FileStream(finalPath, FileMode.Create))
            {
                for (int i = 0; i < dto.totalChunks; i++)
                {
                    string chunkPath = Path.Combine(tempFolder, $"{dto.fileName}.part{i}");

                    if (!System.IO.File.Exists(chunkPath))
                        return BadRequest($"Missing chunk {i}");

                    using (var chunkStream = new FileStream(chunkPath, FileMode.Open))
                    {
                        await chunkStream.CopyToAsync(finalStream);
                    }

                    System.IO.File.Delete(chunkPath);
                }
            }

            // 🔥 SAVE TO DATABASE (THIS WAS MISSING)
            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

            string q = "INSERT INTO Files (FileName, FilePath, UserId) VALUES (@n,@p,@u)";
            using var cmd = new SqlCommand(q, con);

            int userId = GetUserId(); // ⚠️ TEMP FIX (see below)

            cmd.Parameters.AddWithValue("@n", dto.fileName);
            cmd.Parameters.AddWithValue("@p", storedName);
            cmd.Parameters.AddWithValue("@u", userId);

            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();

            return Ok(new { message = "File uploaded & saved" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Merge failed: " + ex.Message);
        }
    }
    //== task 

    [AllowAnonymous] // 🔥 allow chunk upload
    [HttpPost("upload-chunk")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadChunk(
        [FromForm] IFormFile chunk,
        [FromForm] string fileName,
        [FromForm] int chunkIndex)
    {
        try
        {
            if (chunk == null || chunk.Length == 0)
                return BadRequest("Chunk missing");

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "temp");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string chunkPath = Path.Combine(folder, $"{fileName}.part{chunkIndex}");

            using (var stream = new FileStream(chunkPath, FileMode.Create))
            {
                await chunk.CopyToAsync(stream);
            }

            return Ok(new { message = $"Chunk {chunkIndex} uploaded" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Chunk upload error: " + ex.Message);
        }
    }





}