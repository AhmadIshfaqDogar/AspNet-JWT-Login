using Microsoft.AspNetCore.Mvc;
using JwtAuthDemo.Data;
using JwtAuthDemo.Models;
using Microsoft.AspNetCore.Identity;
using JwtAuthDemo.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;


namespace JwtAuthDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _hasher;
        private readonly TokenService _tokenService;

        public AuthController(AppDbContext db, IPasswordHasher<User> hasher, TokenService tokenService)
        {
            _db = db;
            _hasher = hasher;
            _tokenService = tokenService;
        }

        // ===================== REGISTER =====================
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest(new { error = "Username already exists" });

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email
            };

            user.PasswordHash = _hasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Registered successfully" });
        }

        // ===================== LOGIN =====================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _db.Users.SingleOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null)
                return Unauthorized(new { error = "Invalid username or password" });

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { error = "Invalid username or password" });

            var accessToken = _tokenService.CreateToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Store refresh token
            _db.RefreshTokens.Add(new RefreshToken
            {
                Token = refreshToken,
                UserId = user.Id,
                Expires = DateTime.UtcNow.AddDays(7),
                IsRevoked = false
            });

            await _db.SaveChangesAsync();

            // Store in httpOnly secure cookie
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // set true in production (HTTPS)
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            return Ok(new
            {
                token = accessToken,
                role = user.Role // send role to frontend
            });
        }


        // ===================== REFRESH TOKEN =====================
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var refreshTokenCookie = Request.Cookies["refreshToken"];
            if (refreshTokenCookie == null)
                return Unauthorized();

            var storedToken = await _db.RefreshTokens
                .SingleOrDefaultAsync(t => t.Token == refreshTokenCookie);

            if (storedToken == null || storedToken.IsRevoked || storedToken.Expires < DateTime.UtcNow)
                return Unauthorized(new { error = "Invalid refresh token" });

            var user = await _db.Users.FindAsync(storedToken.UserId);
            if (user == null)
                return Unauthorized(new { error = "User not found" });

            var newAccessToken = _tokenService.CreateToken(user);
            return Ok(new { token = newAccessToken });
        }


        // ===================== LOGOUT =====================
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshTokenCookie = Request.Cookies["refreshToken"];

            if (refreshTokenCookie != null)
            {
                var token = await _db.RefreshTokens
                    .SingleOrDefaultAsync(t => t.Token == refreshTokenCookie);

                if (token != null)
                {
                    token.IsRevoked = true;
                    await _db.SaveChangesAsync();
                }

                Response.Cookies.Delete("refreshToken");
            }

            return Ok(new { message = "Logged out successfully" });
        }
        // ===================== Creating Admin =====================

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdmin([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var errors = new List<string>();

            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                errors.Add("Username already exists");

            if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
                errors.Add("Email already exists");

            if (errors.Any())
                return BadRequest(new { errors }); // return both errors together

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                Role = "Admin"
            };

            user.PasswordHash = _hasher.HashPassword(user, dto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Admin created successfully" });
        }




    }
}
