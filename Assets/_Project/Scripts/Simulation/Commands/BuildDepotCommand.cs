using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Commands
{
    /// <summary>Заложить артиллерийское депо. Золото списывается сразу; команда игнорируется, если место не подходит или не хватает золота.</summary>
    public sealed class BuildDepotCommand : ICommand
    {
        public BuildDepotCommand(Side side, float2 position)
        {
            Side = side;
            Position = position;
        }

        public Side Side { get; }

        public float2 Position { get; }

        public void Execute(Match match)
        {
            if (!BuildRules.CanBuild(match, Side, BuildingType.ArtilleryDepot, Position))
            {
                return;
            }

            match.GetPlayer(Side).Gold -= match.Settings.buildings.depotCost;
            match.AddDepot(Side, Position);
        }
    }
}
