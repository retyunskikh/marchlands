using System;
using Marchlands.Simulation.Core;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>Внешний вид отрядов-заглушек. Это не баланс: на симуляцию не влияет.</summary>
    [Serializable]
    public sealed class SquadViewStyle
    {
        [SerializeField] private Sprite soldierSprite;
        [SerializeField] private Sprite markerSprite;
        [SerializeField] private Color franceColor = new Color(0.2f, 0.35f, 0.85f);
        [SerializeField] private Color austriaColor = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color selectionColor = new Color(1f, 0.9f, 0.3f, 0.3f);
        [Tooltip("Насколько рамка выделения шире габаритов отряда с каждой стороны, u.")]
        [SerializeField] private float selectionPadding = 0.3f;
        [SerializeField] private int markerSortingOrder = 10;
        [SerializeField] private int soldierSortingOrder = 20;

        public Sprite SoldierSprite => soldierSprite;

        public Sprite MarkerSprite => markerSprite;

        public Color SelectionColor => selectionColor;

        public float SelectionPadding => selectionPadding;

        public int MarkerSortingOrder => markerSortingOrder;

        public int SoldierSortingOrder => soldierSortingOrder;

        public Color GetSideColor(Side side)
        {
            return side == Side.France ? franceColor : austriaColor;
        }
    }
}
