using System;
using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;

namespace Marchlands.Bootstrap.Config
{
    /// <summary>Стартовая расстановка матча: за кого играет игрок и какие отряды стоят на поле.</summary>
    [CreateAssetMenu(menuName = "Marchlands/Scenario", fileName = "Scenario")]
    public sealed class ScenarioConfig : ScriptableObject
    {
        [SerializeField] private Side playerSide = Side.France;
        [SerializeField] private List<SquadSpawn> squads = new List<SquadSpawn>();

        public Side PlayerSide => playerSide;

        public IReadOnlyList<SquadSpawn> Squads => squads;

        [Serializable]
        public sealed class SquadSpawn
        {
            [SerializeField] private Side side;
            [SerializeField] private Vector2 position;
            [Tooltip("Направление фронта в градусах против часовой от +X: 0 — вправо, 90 — вверх.")]
            [SerializeField] private float facing;
            [SerializeField] private Formation formation;

            public Side Side => side;

            public Vector2 Position => position;

            public float Facing => facing;

            public Formation Formation => formation;
        }
    }
}
