namespace VotingAppTest.Models
{
    public class VoteResultViewModel
    {
        public Candidate Candidate { get; set; }
        public int VoteCount { get; set; }

        public double Percentage { get; set; }

    }
}
