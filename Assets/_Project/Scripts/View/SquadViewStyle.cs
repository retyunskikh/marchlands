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

        [Header("Кольцо перезарядки")]
        [SerializeField] private int ringSegments = 24;
        [SerializeField] private float ringRadius = 0.6f;
        [Tooltip("x — толщина сегмента по радиусу, y — длина по окружности, u.")]
        [SerializeField] private Vector2 ringSegmentSize = new Vector2(0.14f, 0.13f);
        [Tooltip("Зазор между верхним краем отряда и кольцом, u.")]
        [SerializeField] private float ringGap = 0.4f;
        [SerializeField] private Color ringLoadedColor = new Color(0.45f, 0.95f, 0.45f);
        [SerializeField] private Color ringAimingColor = new Color(1f, 0.6f, 0.2f);
        [SerializeField] private Color ringReloadingColor = new Color(1f, 1f, 1f);
        [SerializeField] private Color ringEmptyColor = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] private int ringSortingOrder = 50;

        public Sprite SoldierSprite => soldierSprite;

        public Sprite MarkerSprite => markerSprite;

        public Color SelectionColor => selectionColor;

        public float SelectionPadding => selectionPadding;

        public int MarkerSortingOrder => markerSortingOrder;

        public int SoldierSortingOrder => soldierSortingOrder;

        public int RingSegments => ringSegments;

        public float RingRadius => ringRadius;

        public Vector2 RingSegmentSize => ringSegmentSize;

        public float RingGap => ringGap;

        public Color RingLoadedColor => ringLoadedColor;

        public Color RingAimingColor => ringAimingColor;

        public Color RingReloadingColor => ringReloadingColor;

        public Color RingEmptyColor => ringEmptyColor;

        public int RingSortingOrder => ringSortingOrder;

        public Color GetSideColor(Side side)
        {
            return side == Side.France ? franceColor : austriaColor;
        }
    }
}
