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
        [Tooltip("Планы деревень, нарисованные под дороги этой карты, — по порядку деревень; пусто — общие раскладки.")]
        [SerializeField] private List<Sprite> villageLayouts = new List<Sprite>();
        [Tooltip("Двери домов на планах деревень (от центра деревни) — из них выходит пополнение; по порядку деревень.")]
        [SerializeField] private List<VillageDoors> villageDoors = new List<VillageDoors>();

        [Tooltip("Проверять условия победы (полный матч). В песочницах выключено.")]
        [SerializeField] private bool victoryConditions;

        [Header("Штабы")]
        [SerializeField] private bool hasHeadquarters;
        [SerializeField] private Vector2 franceHeadquarters;
        [SerializeField] private Vector2 austriaHeadquarters;
        [Tooltip("Блокгаузы у проходов к базе: стоят с начала матча.")]
        [SerializeField] private List<Vector2> franceBlockhouses = new List<Vector2>();
        [SerializeField] private List<Vector2> austriaBlockhouses = new List<Vector2>();

        [Header("Кем управляется сторона")]
        [SerializeField] private bool franceAi;
        [SerializeField] private bool austriaAi = true;

        [Header("Местность (полная карта)")]
        [SerializeField] private bool hasTerrain;
        [Tooltip("Проходимая зона; за ней лес (сверху и снизу) и реки (слева и справа).")]
        [SerializeField] private Vector2 terrainMin;
        [SerializeField] private Vector2 terrainMax;
        [SerializeField] private List<ForestSpawn> forests = new List<ForestSpawn>();
        [SerializeField] private List<RoadSpawn> roads = new List<RoadSpawn>();

        [Header("Точки отхода (для песочниц без штабов)")]
        [SerializeField] private Vector2 franceRetreatPoint;
        [SerializeField] private Vector2 austriaRetreatPoint;

        public Side PlayerSide => playerSide;

        public IReadOnlyList<SquadSpawn> Squads => squads;

        public IReadOnlyList<Vector2> Villages => villages;

        public IReadOnlyList<Sprite> VillageLayouts => villageLayouts;

        /// <summary>Двери домов деревни с этим номером; null — у деревни нет своего плана.</summary>
        public IReadOnlyList<Vector2> GetVillageDoors(int index)
        {
            return index < villageDoors.Count ? villageDoors[index].Points : null;
        }

        public bool HasHeadquarters => hasHeadquarters;

        public bool HasTerrain => hasTerrain;

        public Vector2 TerrainMin => terrainMin;

        public Vector2 TerrainMax => terrainMax;

        public IReadOnlyList<ForestSpawn> Forests => forests;

        public IReadOnlyList<RoadSpawn> Roads => roads;

        public bool VictoryConditions => victoryConditions;

        public IReadOnlyList<Vector2> GetBlockhouses(Side side)
        {
            return side == Side.France ? franceBlockhouses : austriaBlockhouses;
        }

        public Vector2 GetHeadquarters(Side side)
        {
            return side == Side.France ? franceHeadquarters : austriaHeadquarters;
        }

        public bool IsAiControlled(Side side)
        {
            return side == Side.France ? franceAi : austriaAi;
        }

        public Vector2 GetRetreatPoint(Side side)
        {
            return side == Side.France ? franceRetreatPoint : austriaRetreatPoint;
        }

        [Serializable]
        public sealed class VillageDoors
        {
            [SerializeField] private List<Vector2> points = new List<Vector2>();

            public IReadOnlyList<Vector2> Points => points;
        }

        [Serializable]
        public sealed class ForestSpawn
        {
            [SerializeField] private Vector2 center;
            [SerializeField] private float radius;

            public Vector2 Center => center;

            public float Radius => radius;
        }

        [Serializable]
        public sealed class RoadSpawn
        {
            [Tooltip("Точки ломаной дороги по порядку.")]
            [SerializeField] private List<Vector2> points = new List<Vector2>();

            public IReadOnlyList<Vector2> Points => points;
        }

        [Serializable]
        public sealed class SquadSpawn
        {
            [SerializeField] private Side side;
            [SerializeField] private Vector2 position;
            [Tooltip("Направление фронта в градусах против часовой от +X: 0 — вправо, 90 — вверх.")]
            [SerializeField] private float facing;
            [SerializeField] private Formation formation;
            [SerializeField] private UnitType type;

            public Side Side => side;

            public Vector2 Position => position;

            public float Facing => facing;

            public Formation Formation => formation;

            public UnitType Type => type;
        }
    }
}
