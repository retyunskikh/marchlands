using Marchlands.Simulation.Config;

namespace Marchlands.Simulation.Squads
{
    /// <summary>Характеристики отряда с учётом его рода войск.</summary>
    public static class UnitStats
    {
        public static UnitTypeSettings Of(Squad squad, SimulationSettings settings)
        {
            return settings.Unit(squad.Type);
        }

        public static float EffectiveRange(Squad squad, SimulationSettings settings)
        {
            return settings.combat.effectiveRange * Of(squad, settings).rangeMultiplier;
        }

        public static float MaxRange(Squad squad, SimulationSettings settings)
        {
            return settings.combat.maxRange * Of(squad, settings).rangeMultiplier;
        }

        public static float PointBlankRange(Squad squad, SimulationSettings settings)
        {
            return settings.combat.pointBlankRange * Of(squad, settings).rangeMultiplier;
        }

        /// <summary>Скорость марша в строю <paramref name="formation"/>, u/s.</summary>
        public static float Speed(Squad squad, Formation formation, SimulationSettings settings)
        {
            return SquadMovementSystem.GetSpeed(formation, settings.squads) * Of(squad, settings).speedMultiplier;
        }

        /// <summary>Самый большой множитель скорости среди родов войск.</summary>
        public static float FastestSpeedMultiplier(SimulationSettings settings)
        {
            return System.Math.Max(System.Math.Max(settings.grenadiers.speedMultiplier, settings.fusiliers.speedMultiplier), settings.cavalry.speedMultiplier);
        }

        /// <summary>Сколько людей нужно на самый малочисленный пехотный отряд.</summary>
        public static int SmallestInfantrySquadSize(SimulationSettings settings)
        {
            return System.Math.Min(settings.grenadiers.squadSize, settings.fusiliers.squadSize);
        }

        public static bool CanFire(Squad squad, SimulationSettings settings)
        {
            return Of(squad, settings).canFire;
        }

        /// <summary>Скорость штыковой атаки (у конницы — атаки в сабли), u/s.</summary>
        public static float ChargeSpeed(Squad squad, SimulationSettings settings)
        {
            return settings.melee.chargeSpeed * Of(squad, settings).speedMultiplier;
        }
    }
}
