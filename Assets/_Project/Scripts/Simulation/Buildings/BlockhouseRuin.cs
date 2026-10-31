using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>Руины разрушенного блокгауза: здесь рабочие поставят его заново, когда базу отобьют.</summary>
    public sealed class BlockhouseRuin
    {
        internal BlockhouseRuin(Side owner, float2 position)
        {
            Owner = owner;
            Position = position;
        }

        public Side Owner { get; }

        public float2 Position { get; }

        /// <summary>Сколько секунд рядом с руинами нет врагов.</summary>
        internal float Quiet { get; set; }
    }
}
