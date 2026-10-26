using Marchlands.Simulation.Core;
using Marchlands.Simulation.Economy;
using Marchlands.Simulation.Squads;
using Unity.Mathematics;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>
    /// Толпа ждущих людей у казармы (пехотный резерв) и у конюшни (резерв конницы) — цель для врага.
    /// Толпа — особый отряд рода «рекруты»: стоит перед зданием, не стреляет, не бежит, в рукопашной отбивается слабо.
    /// В начале каждого тика численность толпы сверяется с резервом: погибшие за прошлый тик вычитаются из резерва,
    /// затем толпа принимает численность резерва (наняли, построили отряд).
    /// </summary>
    internal static class RecruitCampSystem
    {
        public static void Update(Match match)
        {
            foreach (Side side in new[] { Side.France, Side.Austria })
            {
                Sync(match, side, cavalry: false);
                Sync(match, side, cavalry: true);
            }
        }

        private static void Sync(Match match, Side side, bool cavalry)
        {
            PlayerState player = match.GetPlayer(side);
            Squad camp = FindCamp(match, side, cavalry);
            if (camp != null)
            {
                int lost = camp.SyncedAlive - camp.Alive;
                if (lost > 0)
                {
                    Deduct(player, cavalry, lost);
                }
            }

            int reserve = cavalry ? player.CavalryReserve : player.GrenadierReserve + player.JaegerReserve;
            Building home = BuildRules.GetFormingBuilding(match, side, cavalry ? UnitType.Cavalry : UnitType.Grenadiers);
            if (home == null || reserve <= 0)
            {
                if (camp != null)
                {
                    // Удалится в конце тика вместе с погибшими отрядами.
                    camp.Alive = 0;
                    camp.SyncedAlive = 0;
                }

                return;
            }

            if (camp == null)
            {
                float2 front = BuildingSystem.GetFrontDirection(match, home);
                float2 position = home.Position + front * match.Settings.buildings.campOffset;
                camp = match.AddSquad(side, position, Angles.Of(front), Formation.Column, UnitType.Recruits, reserve);
                camp.IsCavalryCamp = cavalry;
            }

            camp.Alive = reserve;
            camp.SyncedAlive = reserve;
        }

        private static Squad FindCamp(Match match, Side side, bool cavalry)
        {
            foreach (Squad squad in match.Squads)
            {
                if (squad.Type == UnitType.Recruits && squad.Side == side && squad.IsCavalryCamp == cavalry && squad.Alive > 0)
                {
                    return squad;
                }
            }

            return null;
        }

        /// <summary>Погибшие рекруты: у конницы — из её резерва, у пехоты — поровну из резервов гренадёров и егерей.</summary>
        private static void Deduct(PlayerState player, bool cavalry, int lost)
        {
            if (cavalry)
            {
                player.CavalryReserve = math.max(0, player.CavalryReserve - lost);
                return;
            }

            for (int i = 0; i < lost; i++)
            {
                if (player.GrenadierReserve >= player.JaegerReserve && player.GrenadierReserve > 0)
                {
                    player.GrenadierReserve--;
                }
                else if (player.JaegerReserve > 0)
                {
                    player.JaegerReserve--;
                }
            }
        }
    }
}
