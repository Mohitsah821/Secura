using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    public class AuthDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    // 🔐 REGISTER
    [HttpPost("register")]
    public IActionResult Register([FromBody] AuthDto dto)
    {
        try
        {
            string hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            string q = "INSERT INTO Users (Username, PasswordHash) VALUES (@u, @p)";
            using var cmd = new SqlCommand(q, con);

            cmd.Parameters.AddWithValue("@u", dto.Username);
            cmd.Parameters.AddWithValue("@p", hash);

            con.Open();
            cmd.ExecuteNonQuery();

            return Ok(new { message = "User created" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Register failed", error = ex.Message });
        }
    }

    // 🔐 LOGIN
    [HttpPost("login")]
    public IActionResult Login([FromBody] AuthDto dto)
    {
        try
        {
            using var con = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            string q = "SELECT Id, PasswordHash FROM Users WHERE Username=@u";
            using var cmd = new SqlCommand(q, con);

            cmd.Parameters.AddWithValue("@u", dto.Username);

            con.Open();
            using var r = cmd.ExecuteReader();

            if (!r.Read())
                return Unauthorized(new { message = "Invalid credentials" });

            string hash = r["PasswordHash"].ToString();

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, hash))
                return Unauthorized(new { message = "Invalid credentials" });

            int userId = Convert.ToInt32(r["Id"]);

            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);

            var token = new JwtSecurityTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("id", userId.ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            });

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Login failed", error = ex.Message });
        }
    }
}