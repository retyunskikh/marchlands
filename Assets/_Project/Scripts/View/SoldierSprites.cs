using System;
using Marchlands.Simulation.Squads;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Роль солдата в отряде — от неё зависит фигурка.</summary>
    public enum SoldierRole
    {
        Private,
        Officer,
        FlagBearer,
        Drummer
    }

    /// <summary>Фигурки солдат одной стороны, вид сверху, фронтом вверх; цвета мундиров уже нарисованы.</summary>
    [Serializable]
    public sealed class SoldierSprites
    {
        [SerializeField] private Sprite grenadier;
        [SerializeField] private Sprite jaeger;
        [SerializeField] private Sprite officer;
        [SerializeField] private Sprite flagBearer;
        [SerializeField] private Sprite drummer;

        public bool IsComplete => grenadier != null && jaeger != null && officer != null && flagBearer != null && drummer != null;

        public Sprite Get(UnitType type, SoldierRole role)
        {
            switch (role)
            {
                case SoldierRole.Officer:
                    return officer;
                case SoldierRole.FlagBearer:
                    return flagBearer;
                case SoldierRole.Drummer:
                    return drummer;
                default:
                    return type == UnitType.Jaegers ? jaeger : grenadier;
            }
        }
    }
}
