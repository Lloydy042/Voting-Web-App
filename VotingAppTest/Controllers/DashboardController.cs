using Microsoft.AspNetCore.Mvc;

namespace VotingSystem.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            var user = HttpContext.Session.GetString("User");

            if (user == null)
                return RedirectToAction("Login", "Home");

            ViewBag.Username = user;
            ViewBag.Role = HttpContext.Session.GetString("Role");

            return View();
        }


        public IActionResult Candidate()
        {
            return RedirectToPage("/Candidates/Index");
        }

        public IActionResult Vote()
        {
            return View();
        }

        public IActionResult Result()
        {
            return View();
        }

        //public IActionResult MyResults()
        //{
        //    var userId = User.FindFirst("UserId")?.Value;
        //    if (string.IsNullOrEmpty(userId))
        //    {
        //        return RedirectToAction("Login", "Home");
        //    }

        //    // Get votes of this user
        //    var myVotes = _mongo.Votes.Find(v => v.VoterId == userId).ToList();

        //    // Join with candidate info
        //    var candidateIds = myVotes.Select(v => v.CandidateId).ToList();
        //    var candidates = _mongo.Candidates.Find(c => candidateIds.Contains(c.Id)).ToList();

        //    return View(candidates);
        //}

    }
}
