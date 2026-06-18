namespace VotingAppTest.Models
{
    public class CandidateIndexViewModel
    {
        public List<Candidate> Candidates { get; set; } = new();
        public HashSet<string> VotedKeys { get; set; } = new();
        // composite key: Position|Category
    }
}
