using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using System.Drawing;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using VotingAppTest.Models;

namespace VotingAppTest.Controllers
{
    public class HomeController : Controller
    {
        // Temporary in-memory database
        private static List<User> Users = new List<User>();
        private readonly MongoDbContext _mongo;
        private readonly EmailService _emailService;

        public HomeController(MongoDbContext mongo, EmailService emailService)
        {
            _mongo = mongo;
            _emailService = emailService;
        }
        public static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private bool PasswordValidation(string password)
        {
            if(string.IsNullOrWhiteSpace(password) || password.Length < 8)  
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && hasDigit && hasSpecial;

        }
        public IActionResult Landing()
        {
            return View();
        }



        //User Management for Admin
        public IActionResult ManageUsers()
        {
            var users = _mongo.Users.Find(_ => true).ToList();
            return View(users);
        }
        [HttpPost]
        public IActionResult DeleteUser(string id)
        {
            try
            {
                _mongo.Users.DeleteOne(c => c.Id == id);
                TempData["Success"] = "User deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Delete failed: " + ex.Message;
            }
            return RedirectToAction("ManageUsers");
        }
        [HttpPost]
        public IActionResult PromoteToAdmin(string id)
        {
            var update = Builders<User>.Update.Set(u => u.Role, "Admin");
            _mongo.Users.UpdateOne(u => u.Id == id, update);

            TempData["Message"] = "User promoted to Admin successfully.";
            return RedirectToAction("ManageUsers");
        }

        [HttpPost]
        public IActionResult DemoteToStudent(string id)
        {
            var update = Builders<User>.Update.Set(u => u.Role, "Voter");
            _mongo.Users.UpdateOne(u => u.Id == id, update);

            TempData["Message"] = "User demoted to Voter successfully.";
            return RedirectToAction("ManageUsers");
        }
        [HttpGet]
        public IActionResult AddUser()
        {
            return View(); // returns AddUser.cshtml form
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddUser(User newUser)
        {
            try
            {
                // Basic validation
                if (string.IsNullOrWhiteSpace(newUser.StudentId) || string.IsNullOrWhiteSpace(newUser.Email))
                {
                    TempData["Error"] = "Student ID and Email are required.";
                    return View(newUser);
                }

                // Check duplicates
                if (_mongo.Users.Find(u => u.StudentId == newUser.StudentId).Any())
                {
                    TempData["Error"] = "Student ID already exists.";
                    return View(newUser);
                }

                if (_mongo.Users.Find(u => u.Email == newUser.Email).Any())
                {
                    TempData["Error"] = "Email already exists.";
                    return View(newUser);
                }


                newUser.Password = HashPassword(newUser.Password);
                newUser.IsVerified = true;

                _mongo.Users.InsertOne(newUser);

                TempData["Success"] = "User added successfully!";
                return RedirectToAction("ManageUsers");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to add user: " + ex.Message;
                return View(newUser);
            }
        }
        [HttpGet]
        public IActionResult EditUser(string id)
        {
            var user = _mongo.Users.Find(u => u.Id == id).FirstOrDefault();
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("ManageUsers");
            }
            return View(user); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditUser(User updatedUser)
        {
            try
            {
                var update = Builders<User>.Update
                    .Set(u => u.StudentId, updatedUser.StudentId)
                    .Set(u => u.Email, updatedUser.Email)
                    .Set(u => u.Role, updatedUser.Role)
                    .Set(u => u.IsVerified, updatedUser.IsVerified);

                _mongo.Users.UpdateOne(u => u.Id == updatedUser.Id, update);

                TempData["Success"] = "User updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Update failed: " + ex.Message;
            }

            return RedirectToAction("ManageUsers");
        }

        //login and registration
        [HttpGet]
        public IActionResult Login()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            return View();
        }


        [HttpPost]

        public IActionResult Login(string studentId, string password)
        {
            var hashedPassword = HashPassword(password);

            var user = _mongo.Users.Find(u =>
                u.StudentId == studentId && u.Password == hashedPassword).FirstOrDefault();

            if (user == null)
            {
                ViewBag.Error = "Invalid Student ID or password.";
    
                return View();
            }
            if (!user.IsVerified)
            {
                TempData["StudentId"]=studentId;
                ViewBag.Error = "Account not verified. Please verify your account.";
                return RedirectToAction("Verify");
            }

            var code = new Random().Next(100000, 900000).ToString();
            user.VerificationCode = code;

            var update = Builders<User>.Update.Set(u => u.VerificationCode, code);
            _mongo.Users.UpdateOne(u => u.Id == user.Id, update);

            _emailService.SendAuthenticationCode(user.Email, code);
            HttpContext.Session.SetString("User", user.StudentId);
            HttpContext.Session.SetString("Role", user.Role);
            TempData["StudentId"] = studentId;
            return RedirectToAction("Auth");
        }
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        
      

        [HttpPost]
        public IActionResult ForgotPassword(string email)
        {
            var user = _mongo.Users.Find(u => u.Email == email).FirstOrDefault();
            if (user == null)
            {
                ViewBag.Error = "No account found with that email.";
                return View();
            }

            var token = Guid.NewGuid().ToString();
            var expiry = DateTime.UtcNow.AddHours(1);

            var update = Builders<User>.Update
                .Set(u => u.ResetPasswordToken, token)
                .Set(u => u.ResetPasswordExpiry, expiry);

            _mongo.Users.UpdateOne(u => u.Id == user.Id, update);

            var resetUrl = Url.Action("ResetPassword", "Home", new { token = token }, Request.Scheme);
            _emailService.SendPasswordResetEmail(user.Email, resetUrl);

            TempData["Message"] = "Password reset link has been sent to your email.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ResetPassword(string token)
{
    var user = _mongo.Users.Find(u => u.ResetPasswordToken == token && u.ResetPasswordExpiry > DateTime.UtcNow).FirstOrDefault();
    if (user == null)
    {
        TempData["Error"] = "Invalid or expired reset link.";
        return RedirectToAction("ForgotPassword");
    }

    ViewBag.Token = token;
    return View();
}

        [HttpPost]
        public IActionResult ResetPassword(string token, string newPassword)
{
    var user = _mongo.Users.Find(u => u.ResetPasswordToken == token && u.ResetPasswordExpiry > DateTime.UtcNow).FirstOrDefault();
    if (user == null)
    {
        ViewBag.Error = "Invalid or expired reset link.";
        return View();
    }

    if (!PasswordValidation(newPassword))
    {
        ViewBag.Error = "Password must contain 8 characters including uppercase, lowercase, numbers, and special characters.";
        return View();
    }

    var hashedPassword = HashPassword(newPassword);

    var update = Builders<User>.Update
        .Set(u => u.Password, hashedPassword)
        .Set(u => u.ResetPasswordToken, null)
        .Set(u => u.ResetPasswordExpiry, null);

    _mongo.Users.UpdateOne(u => u.Id == user.Id, update);

    TempData["Message"] = "Your password has been reset. Please log in.";
    return RedirectToAction("Login");
}
        public IActionResult Register()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return View();
        }

        [HttpPost]
        public IActionResult Register(string studentId, string email, string password, string role, string course, string section, int yearLevel)
        {
            var regex = new Regex(@"^\d{2}-\d{5}$");
            if (!regex.IsMatch(studentId))
            {
                ViewBag.Error = "Invalid Student ID format.";
                return View();
            }

            if (_mongo.Users.Find(u => u.StudentId == studentId).Any())
            {
                ViewBag.Error = "Student ID already registered.";
                return View();
            }
            //if (_mongo.Users.Find(u => u.Email == email).Any())
            //{
            //    ViewBag.Error = "Email Address already registered.";
            //    return View();
            //}
            if (!PasswordValidation(password))
            {
                ViewBag.Error = "Password must contain 8 characters including(Uppercase, Lowercase, Numbers, Special Characters)";
                return View();
            }
            var token = Guid.NewGuid().ToString();
            //var verifyUrl = Url.Action("Verify", "Home", new { token = token }, Request.Scheme);
            var hashedPassword = HashPassword(password);

            var user = new User
            {
                StudentId = studentId,
                Email = email,
                Password = hashedPassword,
                Course = course,
                Section = section,
                YearLevel = yearLevel,
                Role = role,
                VerificationCode = token,
                IsVerified = false
            };

            _mongo.Users.InsertOne(user);

            var verifyUrl = Url.Action("VerifyAccount", "Home", new { token = token }, Request.Scheme);

            try
            {
                _emailService.SendVerificationEmail(user.Email, verifyUrl);
                TempData["Message"] = "A verification link has been sent to your email.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to send verification email. Please try again.";
                // Log exception here
            }

            _emailService.SendVerificationEmail(user.Email, verifyUrl);

            TempData["StudentId"] = studentId;
            return RedirectToAction("Verify");
        }
        public IActionResult Verify()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            ViewBag.Message = "We sent a verification link to your email. Please click it to verify.";
            return View();
        }
        public IActionResult Auth()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            return View();
        }
      
        [HttpPost]
        public async Task<IActionResult> Auth(string code)
        {
            var studentId = TempData["StudentId"]?.ToString();
            var user = _mongo.Users.Find(u => u.StudentId == studentId).FirstOrDefault();

            if (user == null || user.VerificationCode != code)
            {
                ViewBag.Error = "Invalid Authentication code.";
                return View();
            }

            // Clear verification code
            var update = Builders<User>.Update.Set(u => u.VerificationCode, null);
            _mongo.Users.UpdateOne(u => u.Id == user.Id, update);

    

           
            HttpContext.Session.SetString("User", user.StudentId);
            HttpContext.Session.SetString("Role", user.Role);

            return RedirectToAction("Index", "Dashboard");
        }
        public IActionResult ResendAuthenticationCode()
        {
            // Get studentId from session or TempData
            var studentId = TempData["StudentId"]?.ToString() ?? HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(studentId))
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login", "Account");
            }

            var user = _mongo.Users.Find(u => u.StudentId == studentId).FirstOrDefault();
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Login", "Account");
            }

          
            var newCode = new Random().Next(100000, 999999).ToString();

           
            var update = Builders<User>.Update.Set(u => u.VerificationCode, newCode);
            _mongo.Users.UpdateOne(u => u.Id == user.Id, update);

            _emailService.SendAuthenticationCode(user.Email, newCode);

            TempData["StudentId"] = studentId;
            TempData["Success"] = "A new authentication code has been sent to your email.";

            return RedirectToAction("Auth", "Home");
        }
        [HttpGet]
        public IActionResult VerifyAccount(string token)
        {
            var user = _mongo.Users.Find(u => u.VerificationCode == token).FirstOrDefault();

            if (user == null)
            {
                ViewBag.Error = "Invalid or expired verification link.";
                return RedirectToAction("Register");
            }

            var update = Builders<User>.Update
                .Set(u => u.IsVerified, true)
                .Set(u => u.VerificationCode, null);

            _mongo.Users.UpdateOne(u => u.Id == user.Id, update);

            TempData["Message"] = "Your account has been verified. Please log in.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        public IActionResult ResendVerificationLink()
        {
            var studentId = TempData["StudentId"]?.ToString() ?? HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(studentId))
            {
                TempData["Error"] = "Session expired. Please login again.";
                return RedirectToAction("Login");
            }

            var user = _mongo.Users.Find(u => u.StudentId == studentId).FirstOrDefault();
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Register");
            }

            
            var newToken = Guid.NewGuid().ToString();
            var update = Builders<User>.Update.Set(u => u.VerificationCode, newToken);
            _mongo.Users.UpdateOne(u => u.Id == user.Id, update);

            var verifyUrl = Url.Action("VerifyAccount", "Home", new { token = newToken }, Request.Scheme);

            try
            {
                _emailService.SendVerificationEmail(user.Email, verifyUrl);
                TempData["Message"] = "A new verification link has been sent to your email.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to resend verification email. Please try again.";
               
            }

            TempData["StudentId"] = studentId;
            return RedirectToAction("Verify");
        }


        public IActionResult Logout()
        {
            // Clear the session
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Home");
        }

        public IActionResult MyResults()
        {
            var studentId = HttpContext.Session.GetString("User");
            if (string.IsNullOrEmpty(studentId))
            {
                return RedirectToAction("Login", "Home");
            }

            var myVotes = _mongo.Votes.Find(v => v.StudentId == studentId).ToList();

            if (!myVotes.Any())
            {
                // No votes yet
                ViewBag.CouncilVotes = new List<Candidate>();
                ViewBag.LocalVotes = new List<Candidate>();
                return View(new List<Candidate>());
            }

            var candidateIds = myVotes.Select(v => v.CandidateId).ToList();
            var candidates = _mongo.Candidates.Find(c => candidateIds.Contains(c.Id)).ToList();

            // ✅ Split into Student Council vs Local Group
            var councilVotes = candidates.Where(c => c.Category == "StudentCouncil").ToList();
            var localVotes = candidates.Where(c => c.Category != "StudentCouncil").ToList();

            ViewBag.CouncilVotes = councilVotes;
            ViewBag.LocalVotes = localVotes;

            return View(candidates); // still pass all candidates if needed
        }



        //private byte[] GeneratePieChart(int voted, int notVoted)
        //{
        //    using var bitmap = new SKBitmap(400, 400);
        //    using var canvas = new SKCanvas(bitmap);
        //    canvas.Clear(SKColors.White);

        //    var paintVoted = new SKPaint { Color = SKColors.Green, IsAntialias = true };
        //    var paintNotVoted = new SKPaint { Color = SKColors.Red, IsAntialias = true };

        //    var rect = new SKRect(0, 0, 400, 400);
        //    var total = voted + notVoted;

        //    var sweepVoted = 360f * voted / total;
        //    var sweepNotVoted = 360f * notVoted / total;

        //    canvas.DrawArc(rect, 0, sweepVoted, true, paintVoted);
        //    canvas.DrawArc(rect, sweepVoted, sweepNotVoted, true, paintNotVoted);

        //    using var image = SKImage.FromBitmap(bitmap);
        //    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        //    return data.ToArray();
        //}
        //private byte[] GenerateBarChart(List<(string Name, int Votes)> results)
        //{
        //    int width = 700;
        //    int height = 400;
        //    int margin = 60;
        //    int barSpacing = 20;

        //    using var bitmap = new SKBitmap(width, height);
        //    using var canvas = new SKCanvas(bitmap);
        //    canvas.Clear(SKColors.White);

        //    var paintAxis = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, IsAntialias = true };
        //    var paintText = new SKPaint { Color = SKColors.Black, TextSize = 16, IsAntialias = true };
        //    var paintBar = new SKPaint { Color = SKColors.Blue, IsAntialias = true };

        //    int maxVotes = results.Max(r => r.Votes);
        //    int barCount = results.Count;
        //    int barWidth = (width - 2 * margin - (barCount - 1) * barSpacing) / barCount;
        //    int x = margin;
        //    int yBase = height - margin;

        //    foreach (var result in results)
        //    {
        //        float barHeight = (float)result.Votes / maxVotes * (height - 2 * margin);
        //        canvas.DrawRect(x, yBase - barHeight, barWidth, barHeight, paintBar);

        //        // Center name below bar
        //        var nameWidth = paintText.MeasureText(result.Name);
        //        canvas.DrawText(result.Name, x + (barWidth - nameWidth) / 2, yBase + 20, paintText);

        //        // Center vote count above bar
        //        var voteText = result.Votes.ToString();
        //        var voteWidth = paintText.MeasureText(voteText);
        //        canvas.DrawText(voteText, x + (barWidth - voteWidth) / 2, yBase - barHeight - 5, paintText);

        //        x += barWidth + barSpacing;
        //    }

        //    using var image = SKImage.FromBitmap(bitmap);
        //    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        //    return data.ToArray();
        //}

        public IActionResult DownloadVotingExcel()
        {
            var voters = _mongo.Users.Find(u => u.Role == "Voter").ToList();
            var candidates = _mongo.Candidates.Find(_ => true).ToList();
            var votes = _mongo.Votes.Find(_ => true).ToList();

            
            var distinctVoterIds = votes
                .Where(v => !string.IsNullOrWhiteSpace(v.StudentId))
                .Select(v => v.StudentId)
                .Distinct()
                .ToHashSet();

            var totalVoters = voters.Count;
            var voted = distinctVoterIds.Count;                  
            var notVoted = Math.Max(0, totalVoters - voted);    

            var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("Voting Report");

            // Title/Header
            sheet.Cell("A1").Value = "Colegio de Montalban Voting Report";
            sheet.Range("A1:F1").Merge().Style
                .Font.SetBold()
                .Font.SetFontSize(18)
                .Font.SetFontColor(XLColor.DarkGreen)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        
            sheet.Cell("A3").Value = "Metric";
            sheet.Cell("B3").Value = "Value";
            sheet.Range("A3:B3").Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            sheet.Cell("A4").Value = "Total Registered Voters";
            sheet.Cell("B4").Value = totalVoters;
            sheet.Cell("A5").Value = "Voters Who Have Voted";
            sheet.Cell("B5").Value = voted;
            sheet.Cell("A6").Value = "Voters Who Haven't Voted";
            sheet.Cell("B6").Value = notVoted;
            sheet.Cell("A7").Value = "Turnout Percentage";
            sheet.Cell("B7").Value = totalVoters == 0 ? 0 : (double)voted / totalVoters;
            sheet.Cell("B7").Style.NumberFormat.SetFormat("0.00%");

            var groupedVoters = voters
    .GroupBy(v => new { v.Course, v.Section, v.YearLevel })
    .OrderBy(g => g.Key.Course)
    .ThenBy(g => g.Key.YearLevel)
    .ThenBy(g => g.Key.Section);

            sheet.Cell("A8").Value = "Turnout by Group";
            sheet.Range("A8:G8").Merge().Style
                .Font.SetBold()
                .Font.SetFontSize(14)
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            sheet.Cell("A9").Value = "Course";
            sheet.Cell("B9").Value = "Section";
            sheet.Cell("C9").Value = "Year Level";
            sheet.Cell("D9").Value = "Total Voters";
            sheet.Cell("E9").Value = "Voted";
            sheet.Cell("F9").Value = "Not Voted";
            sheet.Cell("G9").Value = "Turnout %";
            sheet.Range("A9:G9").Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            int groupRow = 10;
            foreach (var group in groupedVoters)
            {
                var totalGroupVoters = group.Count();
                var votedGroup = group.Count(v => votes.Any(x => x.StudentId == v.StudentId));
                var notVotedGroup = totalGroupVoters - votedGroup;
                var turnout = totalGroupVoters == 0 ? 0 : (double)votedGroup / totalGroupVoters;

                sheet.Cell(groupRow, 1).Value = group.Key.Course;
                sheet.Cell(groupRow, 2).Value = group.Key.Section;
                sheet.Cell(groupRow, 3).Value = group.Key.YearLevel;
                sheet.Cell(groupRow, 4).Value = totalGroupVoters;
                sheet.Cell(groupRow, 5).Value = votedGroup;
                sheet.Cell(groupRow, 6).Value = notVotedGroup;
                sheet.Cell(groupRow, 7).Value = turnout;
                sheet.Cell(groupRow, 7).Style.NumberFormat.SetFormat("0.00%");
                groupRow++;
            }


            // Candidate Results Section 
            sheet.Cell("A" + (groupRow + 1)).Value = "Candidate Results";
            sheet.Range("A9:D9").Merge().Style
                .Font.SetBold()
                .Font.SetFontSize(14)
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            sheet.Cell("A10").Value = "Category";
            sheet.Cell("B10").Value = "Position";
            sheet.Cell("C10").Value = "Candidate";
            sheet.Cell("D10").Value = "Total Votes";
            sheet.Cell("E10").Value = "Percentage";
            sheet.Range("A10:E10").Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            int row = 11;

            foreach (var group in candidates.GroupBy(c => c.Position))
            {
                var position = group.Key;
                var totalVotesForPosition = group.Sum(c => votes.Count(v => v.CandidateId == c.Id));

                foreach (var candidate in group)
                {
                    var voteCount = votes.Count(v => v.CandidateId == candidate.Id);
                    var percentage = totalVotesForPosition == 0 ? 0 : (double)voteCount / totalVotesForPosition;

                    sheet.Cell(row, 1).Value = candidate.Category;   // ✅ new column
                    sheet.Cell(row, 2).Value = position;
                    sheet.Cell(row, 3).Value = candidate.FullName;
                    sheet.Cell(row, 4).Value = voteCount;
                    sheet.Cell(row, 5).Value = percentage;
                    sheet.Cell(row, 5).Style.NumberFormat.SetFormat("0.00%");
                    row++;
                }

                // Subtotal row per position
                sheet.Cell(row, 2).Value = $"{position} subtotal";
                sheet.Cell(row, 4).Value = totalVotesForPosition;
                sheet.Range(row, 1, row, 5).Style
                    .Font.SetBold()
                    .Fill.SetBackgroundColor(XLColor.LightBlue);
                row++;
            }

            row += 2;

            // Detailed Voter List (studentID, voted-for candidate, position, timestamp)
            sheet.Cell(row, 1).Value = "Detailed Voter List";
            sheet.Range(row, 1, row, 5).Merge().Style
                .Font.SetBold()
                .Font.SetFontSize(14)
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;

            sheet.Cell(row, 1).Value = "Student ID";
            sheet.Cell(row, 2).Value = "Email";
            sheet.Cell(row, 3).Value = "Course";
            sheet.Cell(row, 4).Value = "Section";
            sheet.Cell(row, 5).Value = "Year Level";
            sheet.Cell(row, 6).Value = "Category";
            sheet.Cell(row, 6).Value = "Voted For";
            sheet.Cell(row, 7).Value = "Position";
            sheet.Cell(row, 8).Value = "Timestamp";
            sheet.Range(row, 1, row, 8).Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;



            // Build quick lookup: latest vote by StudentId (if multiple, take the latest timestamp)
            var votesByStudent = votes
                .Where(v => !string.IsNullOrWhiteSpace(v.StudentId))
                .GroupBy(v => v.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(v => v.Timestamp).First() 
                );


            foreach (var voter in voters.OrderBy(v => v.StudentId))
            {
                var studentVotes = votes.Where(v => v.StudentId == voter.StudentId).OrderBy(v => v.Timestamp).ToList();
              


                if (studentVotes.Any())
                {
                    foreach (var vote in studentVotes)
                    {
                        var candidate = candidates.FirstOrDefault(c => c.Id == vote.CandidateId);
                        var candidateName = !string.IsNullOrWhiteSpace(vote.CandidateName) ? vote.CandidateName : "Unknown";
                        var positionName = !string.IsNullOrWhiteSpace(vote.Position) ? vote.Position : "Unknown";
                        var categoryName = candidate?.Category ?? "Unknown";

                        sheet.Cell(row, 1).Value = voter.StudentId;
                        sheet.Cell(row, 2).Value = voter.Email;
                        sheet.Cell(row, 3).Value = voter.Course;
                        sheet.Cell(row, 4).Value = voter.Section;
                        sheet.Cell(row, 5).Value = voter.YearLevel;
                        sheet.Cell(row, 6).Value = candidateName;
                        sheet.Cell(row, 7).Value = positionName;
                        sheet.Cell(row, 8).Value = vote.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                        row++;
                    }
                }
                else
                {
                    sheet.Cell(row, 1).Value = voter.StudentId;
                    sheet.Cell(row, 2).Value = voter.Email;
                    sheet.Cell(row, 3).Value = voter.Course;
                    sheet.Cell(row, 4).Value = voter.Section;
                    sheet.Cell(row, 5).Value = voter.YearLevel;
                    sheet.Cell(row, 6).Value = "Did not vote yet";
                    sheet.Cell(row, 7).Value = "Did not vote yet";
                    sheet.Cell(row, 8).Value = "Did not vote yet";
                    row++;
                }
            }


            row += 2;

           
            sheet.Cell(row, 1).Value = "Non-Voters";
            sheet.Range(row, 1, row, 3).Merge().Style
                .Font.SetBold()
                .Font.SetFontSize(14)
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;

            sheet.Cell(row, 1).Value = "Student ID";
            sheet.Cell(row, 2).Value = "Email";
            sheet.Cell(row, 3).Value = "Status";
            sheet.Range(row, 1, row, 3).Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.LightGray)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            row++;

            var nonVoters = voters
                .Where(v => !distinctVoterIds.Contains(v.StudentId))
                .OrderBy(v => v.StudentId)
                .ToList();

            foreach (var nv in nonVoters)
            {
                sheet.Cell(row, 1).Value = nv.StudentId;
                sheet.Cell(row, 2).Value = nv.Email;
                sheet.Cell(row, 3).Value = "Did not vote yet";
                row++;
            }

            sheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "VotingReport.xlsx");
        }

    }
}