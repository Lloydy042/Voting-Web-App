using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using QRCoder;
using VotingAppTest.Models;
using System.IO;


public class CandidatesController : Controller
{
  
    private readonly MongoDbContext _mongo;
    private readonly IWebHostEnvironment _env;

    public CandidatesController(MongoDbContext mongo, IWebHostEnvironment env)
    {
        _mongo = mongo;
        _env = env;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]

    public IActionResult Create(Candidate candidate, IFormFile ImageFile)
    {
        if (ImageFile != null && ImageFile.Length > 0)
        {
            using (var ms = new MemoryStream())
            {
                ImageFile.CopyTo(ms);
                candidate.ImageData = ms.ToArray();
            }
        }
        else
        {
            candidate.ImageData = null;
        }

     
        _mongo.Candidates.InsertOne(candidate);
        //var profileUrl = Url.Action("Profile", "Candidates", new { id = candidate.Id }, Request.Scheme);
        var profileUrl = $"https://maci-subproportional-scandalously.ngrok-free.dev/Candidates/Profile/{candidate.Id}";


        var qrBytes = GenerateCandidateQrCode(profileUrl);

        var update = Builders<Candidate>.Update.Set(c => c.QrCode, qrBytes);
        _mongo.Candidates.UpdateOne(c => c.Id == candidate.Id, update);

        TempData["Success"] = "Candidate added successfully!";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }
    public IActionResult Index()
    {
        var role = HttpContext.Session.GetString("Role");
        var studentId = HttpContext.Session.GetString("User");

        List<Candidate> candidates;

        if (HttpContext.Session.GetString("Role") == "Admin")
        {
            // Admins see ALL candidates
            candidates = _mongo.Candidates.Find(_ => true).ToList();
        }
        else
        {
            var user = _mongo.Users.Find(u => u.StudentId == studentId).FirstOrDefault();

            // Candidates from the voter's group
            var groupCandidates = _mongo.Candidates.Find(c =>
                c.Course == user.Course &&
                c.Section == user.Section &&
                c.YearLevel == user.YearLevel &&
                c.Category != "StudentCouncil" // exclude council here
            ).ToList();

            // ✅ Student Council candidates (visible to everyone)
            var councilCandidates = _mongo.Candidates.Find(c => c.Category == "StudentCouncil").ToList();

            candidates = groupCandidates.Concat(councilCandidates).ToList();
        }

        List<string> votedPositions = new List<string>();

        if (!string.IsNullOrEmpty(studentId))
        {
            var votes = _mongo.Votes.Find(v => v.StudentId == studentId).ToList();
            var candidateIds = votes.Select(v => v.CandidateId).ToList();
            var votedCandidates = _mongo.Candidates.Find(c => candidateIds.Contains(c.Id)).ToList();

            votedPositions = votedCandidates
                .Select(c => $"{c.Position}|{c.Category}") // composite key
                .Distinct()
                .ToList();
        }

        ViewBag.VotedPositions = votedPositions;



        var studentCouncil = candidates
       .Where(c => c.Category == "StudentCouncil")
       .GroupBy(c => new { c.Position})
       .ToDictionary(
           g => $"{g.Key.Position}",
           g => g.ToList()
       );

        var otherCandidates = candidates
            .Where(c => c.Category != "StudentCouncil")
            .GroupBy(c => new { c.Position, c.Course, c.YearLevel, c.Section })
            .ToDictionary(
                g => $"{g.Key.Position} - {g.Key.Course} Year {g.Key.YearLevel}, Section {g.Key.Section}",
                g => g.ToList()
            );

        ViewBag.StudentCouncilGroups = studentCouncil;
        ViewBag.OtherGroups = otherCandidates;


        return View();
    }
    [HttpGet]
    public IActionResult Edit(string id)
    {
        var candidate = _mongo.Candidates.Find(c => c.Id == id).FirstOrDefault();
        if (candidate == null)
        {
            return NotFound();
        }
        return View(candidate);
    }
   
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(Candidate updatedCandidate, IFormFile ImageFile)
    {
        var update = Builders<Candidate>.Update
            .Set(c => c.FullName, updatedCandidate.FullName)
            .Set(c => c.Position, updatedCandidate.Position)
            .Set(c => c.Vision, updatedCandidate.Vision)
            .Set(c => c.Plans, updatedCandidate.Plans)
            .Set(c => c.Description, updatedCandidate.Description);

        if (ImageFile != null && ImageFile.Length > 0)
        {
            using (var ms = new MemoryStream())
            {
                ImageFile.CopyTo(ms);
                var imageBytes = ms.ToArray();

                // ✅ Update the ImageData field in MongoDB
                update = update.Set(c => c.ImageData, imageBytes);
            }
        }

        _mongo.Candidates.UpdateOne(c => c.Id == updatedCandidate.Id, update);

        TempData["Success"] = "Candidate updated successfully!";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string id)
    {
        try
        {
            _mongo.Candidates.DeleteOne(c => c.Id == id);
            TempData["Success"] = "Candidate deleted successfully!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Delete failed: " + ex.Message;
        }
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult Profile(string id)
    {
        var candidate = _mongo.Candidates.Find(c => c.Id == id).FirstOrDefault();
        if (candidate == null)
        {
            return NotFound();
        }
        return View(candidate);
    }
    private byte[] GenerateCandidateQrCode(string url)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(20); 
    }






}