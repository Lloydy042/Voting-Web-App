using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using VotingAppTest.Models;

public class VotesController : Controller
{
    private readonly MongoDbContext _mongo;

    public VotesController(MongoDbContext mongo)
    {
        _mongo = mongo;
    }
    //public IActionResult CastVote()
    //{
    //    return View();
    //}
   
    [HttpGet]
    public IActionResult Cast(string candidateId)
    {
        var candidate = _mongo.Candidates.Find(c => c.Id == candidateId).FirstOrDefault();
        if (candidate == null)
        {
            return NotFound();
        }

        return View(candidate); 
    }

    public IActionResult CastVote(string candidateId)
    {
        var studentId = HttpContext.Session.GetString("User");
        if (string.IsNullOrEmpty(studentId))
        {
            TempData["Error"] = "You must be logged in to vote.";
            return RedirectToAction("Login", "Home");
        }

        var candidate = _mongo.Candidates.Find(c => c.Id == candidateId).FirstOrDefault();
        if (candidate == null)
        {
            TempData["Error"] = "Candidate not found.";
            return RedirectToAction("Index", "Candidates");
        }

        // ✅ Directly check if this student already has a vote for the same position
        var existingVote = _mongo.Votes.Find(v => v.StudentId == studentId && v.Position == candidate.Position && v.Category == candidate.Category).FirstOrDefault();
        if (existingVote != null)
        {
            TempData["Error"] = $"You have already voted for the {candidate.Position} position.";
            return RedirectToAction("Index", "Candidates");
        }


        var vote = new Vote
        {
            StudentId = studentId,
            CandidateId = candidateId,
            CandidateName = candidate.FullName,
            Position = candidate.Position,
            Category = candidate.Category, // ✅ store position directly in the vote
            Timestamp = DateTime.UtcNow
        };
        
        _mongo.Votes.InsertOne(vote);

        // Optional: update user record if you want a global flag
        var update = Builders<User>.Update.Set(u => u.HasVoted, true);
        _mongo.Users.UpdateOne(u => u.StudentId == studentId, update);

        TempData["Success"] = $"Your vote for {candidate.FullName} as {candidate.Position} has been recorded!";
        return RedirectToAction("Index", "Candidates");
    }



    private double CalculatePercentage(string candidateId, List<Candidate> candidates)
    {
        long totalVotes = candidates.Sum(c => _mongo.Votes.CountDocuments(v => v.CandidateId == c.Id));
        long candidateVotes = _mongo.Votes.CountDocuments(v => v.CandidateId == candidateId);

        if (totalVotes == 0) return 0;
        return Math.Round((double)candidateVotes / totalVotes * 100, 2);
    }


    [HttpGet]
    public IActionResult Results()
    {
        var studentId = HttpContext.Session.GetString("User");
        var role = HttpContext.Session.GetString("Role");
        var user = _mongo.Users.Find(u => u.StudentId == studentId).FirstOrDefault();

        if (user == null) return RedirectToAction("Index", "Home");

        Dictionary<string, List<VoteResultViewModel>> groupedResults;

        if (HttpContext.Session.GetString("Role") == "Admin")
        {
            // Admins see ALL candidates
            var candidates = _mongo.Candidates.Find(_ => true).ToList();

            groupedResults = candidates
                .GroupBy(c => $"{c.Category} - {c.Position} - {c.Course} Year {c.YearLevel} Section {c.Section}")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(c => new VoteResultViewModel
                    {
                        Candidate = c,
                        VoteCount = (int)_mongo.Votes.CountDocuments(v => v.CandidateId == c.Id),
                        Percentage = CalculatePercentage(c.Id, g.ToList())
                    }).ToList()
                );

            ViewBag.ShowResults = true; // Admins always see results
        }
        else
        {
            // Voters only see their group
            var settings = _mongo.Settings.Find(s =>
                s.Course == user.Course &&
                s.Section == user.Section &&
                s.YearLevel == user.YearLevel &&
                s.Category == "StudentCouncil" // ✅ optionally filter by category
            ).FirstOrDefault();

            bool showResults = settings?.ShowResults ?? false;

            var candidates = _mongo.Candidates.Find(c =>
                c.Course == user.Course &&
                c.Section == user.Section &&
                c.YearLevel == user.YearLevel &&
                c.Category == "StudentCouncil"
                ).ToList();



            groupedResults = candidates
                .GroupBy(c => $"{c.Category} - {c.Position}")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(c => new VoteResultViewModel
                    {
                        Candidate = c,
                        VoteCount = (int)_mongo.Votes.CountDocuments(v => v.CandidateId == c.Id),
                        Percentage = CalculatePercentage(c.Id, g.ToList())
                    }).ToList()
                );

            ViewBag.ShowResults = showResults;
        }

        return View(groupedResults);
    }

    public IActionResult VoterStats()
    {
        var totalVoters = _mongo.Users.CountDocuments(u => u.Role == "Voter");
        var votersNotVoted = _mongo.Users.CountDocuments(u => u.Role == "Voter" && !u.HasVoted);

        ViewBag.TotalVoters = totalVoters;
        ViewBag.NotVoted = votersNotVoted;
        ViewBag.Voted = totalVoters - votersNotVoted;

        return View();
    }
   
    [HttpPost]
    public IActionResult ToggleResults(string course, string section, int yearLevel, string category, bool show)
    {
        var filter = Builders<ElectionSettings>.Filter.And(
            Builders<ElectionSettings>.Filter.Eq(s => s.Course, course),
            Builders<ElectionSettings>.Filter.Eq(s => s.Section, section),
            Builders<ElectionSettings>.Filter.Eq(s => s.YearLevel, yearLevel),
            Builders<ElectionSettings>.Filter.Eq(s => s.Category, category) // ✅ include category
        );

        var update = Builders<ElectionSettings>.Update.Set(s => s.ShowResults, show);
        _mongo.Settings.UpdateOne(filter, update, new UpdateOptions { IsUpsert = true });

        TempData["Success"] = $"Results visibility updated for {category} - {course} Year {yearLevel} Section {section}";
        return RedirectToAction("Results");
    }




}