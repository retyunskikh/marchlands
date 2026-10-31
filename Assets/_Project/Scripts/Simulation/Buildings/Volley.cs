using Marchlands.Simulation.Config;

namespace Marchlands.Simulation.Buildings
{
    /// <summary>
    /// Залп здания: сколько ружей, как часто, с какой меткостью и дальностью. У штаба, казарм и конюшен — часовые,
    /// у блокгауза — свои, более сильные параметры.
    /// </summary>
    public readonly struct Volley
    {
        public Volley(int shots, float interval, float hitChance, float range)
        {
            Shots = shots;
            Interval = interval;
            HitChance = hitChance;
            Range = range;
        }

        public int Shots { get; }

        public float Interval { get; }

        public float HitChance { get; }

        public float Range { get; }

        public static Volley Of(Building building, BuildingSettings settings)
        {
            return building.Type == BuildingType.Blockhouse
                ? new Volley(settings.blockhouseShots, settings.blockhouseInterval, settings.blockhouseHitChance, settings.blockhouseRange)
                : new Volley(building.IsUpgraded ? settings.upgradedSentryShots : settings.sentryShots,
                    settings.sentryInterval, settings.sentryHitChance, settings.sentryRange);
        }
    }
}
