using System;
using System.Collections.Generic;
using Marchlands.Simulation.Core;
using Marchlands.Simulation.Squads;
using UnityEngine;

namespace Marchlands.Bootstrap.Config
{
    /// <summary>Стартовая расстановка матча: за кого играет игрок, какие отряды стоят на поле и куда бегут разбитые.</summary>
    [CreateAssetMenu(menuName = "Marchlands/Scenario", fileName = "Scenario")]
    public sealed class ScenarioConfig : ScriptableObject
    {
        [SerializeField] private Side playerSide = Side.France;
        [SerializeField] private List<SquadSpawn> squads = new List<SquadSpawn>();
        [Tooltip("Положения деревень; на старте все ничьи.")]
        [SerializeField] private List<Vector2> villages = new List<Vector2>();

        [Header("Кем управляется сторона")]
        [SerializeField] private bool franceAi;
        [SerializeField] private bool austriaAi = true;

        [Header("Точки отхода (до появления штабов)")]
        [SerializeField] private Vector2 franceRetreatPoint;
        [SerializeField] private Vector2 austriaRetreatPoint;

        public Side PlayerSide => playerSide;

        public IReadOnlyList<SquadSpawn> Squads => squads;

        public IReadOnlyList<Vector2> Villages => villages;

        public bool IsAiControlled(Side side)
        {
            return side == Side.France ? franceAi : austriaAi;
        }

        public Vector2 GetRetreatPoint(Side side)
        {
            return side == Side.France ? franceRetreatPoint : austriaRetreatPoint;
        }

        [Serializable]
        public sealed class SquadSpawn
        {
            [SerializeField] private Side side;
            [SerializeField] private Vector2 position;
            [Tooltip("Направление фронта в градусах против часовой от +X: 0 — вправо, 90 — вверх.")]
            [SerializeField] private float facing;
            [SerializeField] private Formation formation;
            [Tooltip("Огонь по готовности с начала боя — так манекен стреляет сам при каждой готовности.")]
            [SerializeField] private bool fireAtWill;

            public Side Side => side;

            public Vector2 Position => position;

            public float Facing => facing;

            public Formation Formation => formation;

            public bool FireAtWill => fireAtWill;
        }
    }
}
