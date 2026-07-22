using System;
using System.Collections.Generic;
using System.Linq;

namespace Cs2Admin.API.Services
{
    public static class EloService
    {
        private const double KFactorBase = 32.0;
        private const double ScaleFactor = 400.0;

        /// <summary>
        /// Calculates new Elo ratings for two teams after a match.
        /// Uses the average Elo of each team to compute expected scores,
        /// then applies a dynamic K-factor based on the difference between
        /// individual player Elo and the team average (rewarding underdogs more).
        /// </summary>
        /// <param name="team1Elos">Dictionary of SteamId -> current Elo for Team 1</param>
        /// <param name="team2Elos">Dictionary of SteamId -> current Elo for Team 2</param>
        /// <param name="team1Won">True if Team 1 won, false if Team 2 won</param>
        /// <returns>Dictionary of SteamId -> new Elo for ALL players</returns>
        public static Dictionary<string, int> CalculateNewRatings(
            Dictionary<string, int> team1Elos,
            Dictionary<string, int> team2Elos,
            bool team1Won)
        {
            var results = new Dictionary<string, int>();

            double team1Avg = team1Elos.Values.Average();
            double team2Avg = team2Elos.Values.Average();

            double expectedTeam1 = ExpectedScore(team1Avg, team2Avg);
            double expectedTeam2 = 1.0 - expectedTeam1;

            double actualTeam1 = team1Won ? 1.0 : 0.0;
            double actualTeam2 = team1Won ? 0.0 : 1.0;

            foreach (var (steamId, elo) in team1Elos)
            {
                double k = DynamicKFactor(elo, team1Avg, team2Avg);
                int delta = (int)Math.Round(k * (actualTeam1 - expectedTeam1));
                results[steamId] = Math.Max(0, elo + delta);
            }

            foreach (var (steamId, elo) in team2Elos)
            {
                double k = DynamicKFactor(elo, team2Avg, team1Avg);
                int delta = (int)Math.Round(k * (actualTeam2 - expectedTeam2));
                results[steamId] = Math.Max(0, elo + delta);
            }

            return results;
        }

        /// <summary>
        /// Standard Elo expected score formula.
        /// </summary>
        private static double ExpectedScore(double ratingA, double ratingB)
        {
            return 1.0 / (1.0 + Math.Pow(10.0, (ratingB - ratingA) / ScaleFactor));
        }

        /// <summary>
        /// Dynamic K-factor: players far below their team's average get a slightly
        /// higher K (they were the "underdogs" within their own team), while
        /// higher-rated players have a smaller K so they don't swing too much.
        /// Also accounts for opponent strength.
        /// </summary>
        private static double DynamicKFactor(double playerElo, double ownTeamAvg, double opponentTeamAvg)
        {
            double eloDiffFromTeam = ownTeamAvg - playerElo;
            double teamDiff = Math.Abs(ownTeamAvg - opponentTeamAvg);

            double modifier = 1.0 + (eloDiffFromTeam / ScaleFactor * 0.5);
            modifier = Math.Clamp(modifier, 0.5, 1.5);

            double strengthBonus = 1.0 + (teamDiff / ScaleFactor * 0.15);
            strengthBonus = Math.Clamp(strengthBonus, 1.0, 1.3);

            return KFactorBase * modifier * strengthBonus;
        }

        /// <summary>
        /// Balances players into two teams by minimizing the Elo difference
        /// between teams. Uses a greedy "snake draft" approach sorted by Elo.
        /// </summary>
        public static (List<string> team1, List<string> team2) BalanceTeams(Dictionary<string, int> playerElos)
        {
            var sorted = playerElos.OrderByDescending(p => p.Value).ToList();
            var team1 = new List<string>();
            var team2 = new List<string>();
            int team1Total = 0;
            int team2Total = 0;

            for (int i = 0; i < sorted.Count; i++)
            {
                if (team1Total <= team2Total && team1.Count < sorted.Count / 2)
                {
                    team1.Add(sorted[i].Key);
                    team1Total += sorted[i].Value;
                }
                else
                {
                    team2.Add(sorted[i].Key);
                    team2Total += sorted[i].Value;
                }
            }

            return (team1, team2);
        }
    }
}
