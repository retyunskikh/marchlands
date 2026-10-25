using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Commands
{
    /// <summary>Заложить казарму. Золото списывается сразу; команда игнорируется, если место не подходит или не хватает золота.</summary>
    public sealed class BuildBarracksCommand : ICommand
    {
        public BuildBarracksCommand(Side side, float2 position)
        {
            Side = side;
            Position = position;
        }

        public Side Side { get; }

        public float2 Position { get; }

        public void Execute(Match match)
        {
            if (!BuildRules.CanBuildBarracks(match, Side, Position))
            {
                return;
            }

            match.GetPlayer(Side).Gold -= match.Settings.buildings.barracksCost;
            match.AddBarracks(Side, Position);
        }
    }
}
