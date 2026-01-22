using System.Collections.Generic;
using System.Text;

namespace Rooster.Models
{
    public class MatchReport
    {
        public int TotalScore { get; set; }
        public List<string> Breakdown { get; set; } = new List<string>();

        public void AddScore(string reason, int points)
        {
            TotalScore += points;
            Breakdown.Add($"+{points}: {reason}");
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Total Score: {TotalScore}");
            foreach (var item in Breakdown)
            {
                sb.AppendLine(item);
            }
            return sb.ToString();
        }
    }
}
