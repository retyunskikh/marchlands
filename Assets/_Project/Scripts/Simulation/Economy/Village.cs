using Marchlands.Simulation.Core;
using Unity.Mathematics;

namespace Marchlands.Simulation.Economy
{
    /// <summary>Деревня: приносит доход владельцу, захватывается отрядами. Снаружи сборки только для чтения.</summary>
    public sealed class Village
    {
        internal Village(int id, float2 position)
        {
            Id = id;
            Position = position;
        }

        public int Id { get; }

        public float2 Position { get; }

        /// <summary>Владелец; null — ничья.</summary>
        public Side? Owner { get; internal set; }

        /// <summary>Кто сейчас набирает прогресс захвата; null — никто.</summary>
        public Side? CapturingSide { get; internal set; }

        /// <summary>Набранный прогресс захвата, s (до captureTime).</summary>
        public float CaptureProgress { get; internal set; }
    }
}
