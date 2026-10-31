using Marchlands.Simulation.Config;

namespace Marchlands.Simulation.Core
{
    /// <summary>
    /// Погода матча: сухие промежутки и дожди случайной длины (через GameRandom — одинаковый seed даёт ту же погоду).
    /// Пока идёт дождь, каждое ружьё стреляет лишь с вероятностью rainFireChance.
    /// </summary>
    internal static class WeatherSystem
    {
        public static void Update(Match match, float deltaTime)
        {
            WeatherSettings weather = match.Settings.weather;
            if (weather == null || weather.dryTimeMax <= 0f)
            {
                return;
            }

            if (!match.WeatherStarted)
            {
                match.WeatherStarted = true;
                match.WeatherTimer = Roll(match, weather.dryTimeMin, weather.dryTimeMax);
            }

            match.WeatherTimer -= deltaTime;
            if (match.WeatherTimer > 0f)
            {
                return;
            }

            match.IsRaining = !match.IsRaining;
            match.WeatherTimer = match.IsRaining
                ? Roll(match, weather.rainTimeMin, weather.rainTimeMax)
                : Roll(match, weather.dryTimeMin, weather.dryTimeMax);
        }

        /// <summary>Сколько из <paramref name="shooters"/> ружей выстрелит: в дождь часть даёт осечку.</summary>
        public static int RollFired(Match match, int shooters)
        {
            if (!match.IsRaining || shooters <= 0)
            {
                return shooters;
            }

            float chance = match.Settings.weather.rainFireChance;
            int fired = 0;
            for (int i = 0; i < shooters; i++)
            {
                if (match.Random.NextFloat() < chance)
                {
                    fired++;
                }
            }

            return fired;
        }

        private static float Roll(Match match, float min, float max)
        {
            return max > min ? match.Random.Range(min, max) : min;
        }
    }
}
