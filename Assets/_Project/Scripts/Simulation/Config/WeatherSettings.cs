using System;

namespace Marchlands.Simulation.Config
{
    /// <summary>Погода: иногда идёт дождь, и сырой порох даёт осечки.</summary>
    [Serializable]
    public sealed class WeatherSettings
    {
        /// <summary>Сколько длится сухая погода между дождями: случайно от и до, s. 0 — дождя не бывает.</summary>
        public float dryTimeMin;

        public float dryTimeMax;

        /// <summary>Сколько длится дождь: случайно от и до, s.</summary>
        public float rainTimeMin;

        public float rainTimeMax;

        /// <summary>Доля ружей, которые в дождь всё же стреляют (остальные — осечка), 0–1.</summary>
        public float rainFireChance;

        /// <summary>Во сколько раз медленнее идут, бегут в атаку и бегут все отряды под дождём (раскисшая земля).</summary>
        public float rainSpeedMultiplier;
    }
}
