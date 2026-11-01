using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Combat;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Контрольная сумма состояния матча (FNV-1a по битам значений): в сетевой игре стороны сверяют её, чтобы
    /// поймать рассинхронизацию. Одинаковое состояние — одинаковая сумма на любой машине.
    /// </summary>
    public static class StateHash
    {
        private const ulong Offset = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;

        public static ulong Of(Match match)
        {
            ulong hash = Offset;
            Mix(ref hash, match.CurrentTick);
            Mix(ref hash, match.Random.State);
            Mix(ref hash, match.IsRaining ? 1 : 0);
            foreach (PlayerState player in match.Players)
            {
                Mix(ref hash, player.Gold);
                Mix(ref hash, player.GrenadierReserve);
                Mix(ref hash, player.FusilierReserve);
                Mix(ref hash, player.CavalryReserve);
                Mix(ref hash, player.ArtilleryReserve);
                Mix(ref hash, player.HasBalloon ? 1 : 0);
                Mix(ref hash, player.BalloonResearchRemaining);
            }

            foreach (Squad squad in match.Squads)
            {
                Mix(ref hash, squad.Id);
                Mix(ref hash, (int)squad.Side);
                Mix(ref hash, (int)squad.Type);
                Mix(ref hash, squad.Alive);
                Mix(ref hash, squad.Position);
                Mix(ref hash, squad.Facing);
                Mix(ref hash, (int)squad.Formation);
                Mix(ref hash, squad.Morale);
                Mix(ref hash, squad.Kills);
                Mix(ref hash, (int)squad.MoraleState);
                Mix(ref hash, (int)squad.FireState);
                Mix(ref hash, squad.FireTimer);
                Mix(ref hash, (int)squad.Order.Type);
                Mix(ref hash, squad.MeleeOpponents);
            }

            foreach (RoundShotFlight shot in match.RoundShots)
            {
                Mix(ref hash, shot.GunId);
                Mix(ref hash, shot.Impact);
                Mix(ref hash, shot.Age);
            }

            foreach (Building building in match.Buildings)
            {
                Mix(ref hash, building.Id);
                Mix(ref hash, building.Health);
                Mix(ref hash, building.BuildRemaining);
                Mix(ref hash, building.Level);
                Mix(ref hash, building.Houses);
            }

            foreach (Village village in match.Villages)
            {
                Mix(ref hash, village.Owner.HasValue ? (int)village.Owner.Value : -1);
                Mix(ref hash, village.CaptureProgress);
            }

            return hash;
        }

        private static void Mix(ref ulong hash, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                hash ^= (value >> (i * 8)) & 0xFF;
                hash *= Prime;
            }
        }

        private static void Mix(ref ulong hash, int value)
        {
            Mix(ref hash, unchecked((ulong)(uint)value));
        }

        private static void Mix(ref ulong hash, float value)
        {
            Mix(ref hash, math.asint(value));
        }

        private static void Mix(ref ulong hash, float2 value)
        {
            Mix(ref hash, value.x);
            Mix(ref hash, value.y);
        }
    }
}
