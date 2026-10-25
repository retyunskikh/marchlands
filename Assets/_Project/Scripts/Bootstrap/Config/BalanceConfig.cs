using Marchlands.Simulation.Config;
using UnityEngine;

namespace Marchlands.Bootstrap.Config
{
    /// <summary>
    /// Параметры баланса для симуляции. Значения правятся в инспекторе и подхватываются при следующем
    /// запуске матча без перекомпиляции.
    /// </summary>
    [CreateAssetMenu(menuName = "Marchlands/Balance Config", fileName = "Balance")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [SerializeField] private SimulationSettings simulation = new SimulationSettings();

        /// <summary>
        /// Независимая копия настроек: правка ассета во время игры не меняет уже идущий матч,
        /// иначе пропал бы детерминизм.
        /// </summary>
        public SimulationSettings CreateSettings()
        {
            return JsonUtility.FromJson<SimulationSettings>(JsonUtility.ToJson(simulation));
        }
    }
}
