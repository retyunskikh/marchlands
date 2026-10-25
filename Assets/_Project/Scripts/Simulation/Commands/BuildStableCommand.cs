using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Commands
{
    /// <summary>Заложить конюшню. Золото списывается сразу; команда игнорируется, если место не подходит или не хватает золота.</summary>
    public sealed class BuildStableCommand : ICommand
    {
        public BuildStableCommand(Side side, float2 position)
        {
            Side = side;
            Position = position;
        }

        public Side Side { get; }

        public float2 Position { get; }

        public void Execute(Match match)
        {
            if (!BuildRules.CanBuild(match, Side, BuildingType.Stable, Position))
            {
                return;
            }

            match.GetPlayer(Side).Gold -= match.Settings.buildings.stableCost;
            match.AddStable(Side, Position);
        }
    }
}
