using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;

namespace Marchlands.Simulation.Commands
{
    /// <summary>
    /// Изучить в штабе воздушный шар: платят сразу, изучают balloonResearchTime секунд, потом туман войны для
    /// стороны снят до конца матча. Только при включённом тумане и один раз.
    /// </summary>
    public sealed class ResearchBalloonCommand : ICommand
    {
        public ResearchBalloonCommand(Side side)
        {
            Side = side;
        }

        public Side Side { get; }

        public void Execute(Match match)
        {
            if (!BuildRules.CanResearchBalloon(match, Side))
            {
                return;
            }

            var player = match.GetPlayer(Side);
            player.Gold -= match.Settings.vision.balloonCost;
            float time = match.Settings.vision.balloonResearchTime;
            player.BalloonResearchRemaining = time > 0f ? time : 0f;
            player.HasBalloon = time <= 0f;
        }
    }
}
