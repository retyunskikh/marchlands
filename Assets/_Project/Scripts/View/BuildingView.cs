using Marchlands.Simulation.Buildings;
using Marchlands.Simulation.Config;
using Marchlands.Simulation.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Здание: тело цвета стороны (полупрозрачное во время постройки), полоска постройки или прихода
    /// следующего рекрута, полоска здоровья повреждённого здания и рамка выделения. Только читает состояние здания.
    /// </summary>
    public sealed class BuildingView : MonoBehaviour
    {
        private Building building;
        private SimulationSettings settings;
        private BuildingViewStyle style;
        private Color sideColor;
        private Vector2 size;

        private SpriteRenderer body;
        private SpriteRenderer inner;
        private SpriteRenderer selection;
        private SpriteRenderer progressBackground;
        private SpriteRenderer progressFill;
        private SpriteRenderer healthBackground;
        private SpriteRenderer healthFill;
        private float barsTop;
        private SpriteRenderer flag;
        private Color innerColor;
        private SpriteRenderer[] annexBases = new SpriteRenderer[0];
        private SpriteRenderer[] annexRoofs = new SpriteRenderer[0];

        public Building Building => building;

        internal void Initialize(Building building, SimulationSettings settings, BuildingViewStyle style, Color sideColor)
        {
            this.building = building;
            this.settings = settings;
            this.style = style;
            this.sideColor = sideColor;
            transform.position = new Vector3(building.Position.x, building.Position.y, 0f);

            bool isHeadquarters = building.Type == BuildingType.Headquarters;
            bool isStable = building.Type == BuildingType.Stable;
            size = isHeadquarters ? style.HeadquartersSize : isStable ? style.StableSize : style.BarracksSize;

            selection = CreateRenderer("Selection", style.BodySortingOrder - 1, style.SelectionColor);
            selection.transform.localScale = new Vector3(size.x + style.SelectionPadding * 2f, size.y + style.SelectionPadding * 2f, 1f);
            selection.enabled = false;

            Sprite baseSprite = isHeadquarters ? style.HeadquartersBaseSprite : isStable ? style.StableBaseSprite : style.BarracksBaseSprite;
            Sprite roofSprite = isHeadquarters ? style.HeadquartersRoofSprite : isStable ? style.StableRoofSprite : style.BarracksRoofSprite;
            if (baseSprite != null && roofSprite != null)
            {
                CreateEstate(baseSprite, roofSprite, isHeadquarters);
                if (isHeadquarters)
                {
                    CreateAnnexes();
                }
            }
            else
            {
                body = CreateRenderer("Body", style.BodySortingOrder, sideColor);
                body.transform.localScale = new Vector3(size.x, size.y, 1f);

                // Без спрайтов штаб — квадрат с тёмной серединой; казарма — прямоугольник с тёмным коньком.
                inner = CreateRenderer("Inner", style.BodySortingOrder + 1, style.InnerColor);
                innerColor = style.InnerColor;
                inner.transform.localScale = isHeadquarters
                    ? new Vector3(size.x * style.HeadquartersInnerScale, size.y * style.HeadquartersInnerScale, 1f)
                    : new Vector3(size.x, size.y * 0.15f, 1f);
            }

            barsTop = size.y * 0.5f + style.ProgressBarOffset;
            progressBackground = CreateRenderer("Progress", style.OverlaySortingOrder, style.ProgressBackground);
            progressBackground.transform.localPosition = new Vector3(0f, barsTop, 0f);
            progressBackground.transform.localScale = new Vector3(style.ProgressBarSize.x, style.ProgressBarSize.y, 1f);
            progressFill = CreateRenderer("Progress Fill", style.OverlaySortingOrder + 1, style.ProgressColor);

            healthBackground = CreateRenderer("Health", style.OverlaySortingOrder, style.ProgressBackground);
            healthBackground.transform.localPosition = new Vector3(0f, HealthBarY, 0f);
            healthBackground.transform.localScale = new Vector3(style.ProgressBarSize.x, style.ProgressBarSize.y, 1f);
            healthFill = CreateRenderer("Health Fill", style.OverlaySortingOrder + 1, style.HealthColor);
        }

        /// <summary>
        /// Усадьба вид сверху: двор или плац с оградой (inner), крыши цвета стороны (body), у штаба — флаг во дворе.
        /// Ворота смотрят к центру карты: здание справа отражается по горизонтали.
        /// </summary>
        private void CreateEstate(Sprite baseSprite, Sprite roofSprite, bool withFlag)
        {
            bool flip = building.Position.x > 0f;
            float direction = flip ? -1f : 1f;

            inner = CreateRenderer("Grounds", style.BodySortingOrder, Color.white);
            inner.sprite = baseSprite;
            inner.flipX = flip;
            inner.transform.localScale = FitScale(baseSprite);
            innerColor = Color.white;

            body = CreateRenderer("Roofs", style.BodySortingOrder + 1, sideColor);
            body.sprite = roofSprite;
            body.flipX = flip;
            body.transform.localScale = FitScale(roofSprite);
            if (!withFlag)
            {
                return;
            }

            Vector2 polePosition = Vector2.Scale(style.HeadquartersFlagPosition, size);
            polePosition.x *= direction;
            Vector2 flagSize = style.HeadquartersFlagSize;
            flag = CreateRenderer("Flag", style.BodySortingOrder + 2, sideColor);
            flag.transform.localPosition = polePosition + new Vector2(direction * flagSize.x * 0.5f, 0f);
            flag.transform.localScale = new Vector3(flagSize.x, flagSize.y, 1f);
            SpriteRenderer pole = CreateRenderer("Pole", style.BodySortingOrder + 3, style.PoleColor);
            pole.transform.localPosition = polePosition;
            pole.transform.localScale = new Vector3(style.HeadquartersPoleSize, style.HeadquartersPoleSize, 1f);
        }

        /// <summary>Места для домов-пристроек вокруг штаба; показываются по мере постройки.</summary>
        private void CreateAnnexes()
        {
            Vector2[] offsets = style.AnnexOffsets;
            if (style.AnnexBaseSprite == null || style.AnnexRoofSprite == null || offsets == null)
            {
                return;
            }

            float direction = building.Position.x > 0f ? -1f : 1f;
            annexBases = new SpriteRenderer[offsets.Length];
            annexRoofs = new SpriteRenderer[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2 position = new Vector2(offsets[i].x * direction, offsets[i].y);
                annexBases[i] = CreateRenderer($"Annex {i}", style.BodySortingOrder, Color.white);
                annexBases[i].sprite = style.AnnexBaseSprite;
                annexBases[i].transform.localPosition = position;
                annexBases[i].transform.localScale = FitScale(style.AnnexBaseSprite, style.AnnexSize);
                annexRoofs[i] = CreateRenderer($"Annex Roof {i}", style.BodySortingOrder + 1, sideColor);
                annexRoofs[i].sprite = style.AnnexRoofSprite;
                annexRoofs[i].transform.localPosition = position;
                annexRoofs[i].transform.localScale = FitScale(style.AnnexRoofSprite, style.AnnexSize);
                annexBases[i].enabled = annexRoofs[i].enabled = false;
            }
        }

        private static Vector3 FitScale(Sprite sprite, Vector2 target)
        {
            Vector2 bounds = sprite.bounds.size;
            return new Vector3(target.x / bounds.x, target.y / bounds.y, 1f);
        }

        /// <summary>Построенные дома — сплошные, строящийся — полупрозрачный, остальные места пусты.</summary>
        private void RenderAnnexes()
        {
            for (int i = 0; i < annexBases.Length; i++)
            {
                bool built = i < building.Houses;
                bool underConstruction = i == building.Houses && building.IsBuildingHouse;
                bool visible = built || underConstruction;
                annexBases[i].enabled = annexRoofs[i].enabled = visible;
                if (!visible)
                {
                    continue;
                }

                float alpha = built ? 1f : style.ConstructionAlpha;
                annexBases[i].color = new Color(1f, 1f, 1f, alpha);
                Color roof = sideColor;
                roof.a *= alpha;
                annexRoofs[i].color = roof;
            }
        }

        /// <summary>Масштаб, при котором спрайт занимает габариты здания.</summary>
        private Vector3 FitScale(Sprite sprite)
        {
            Vector2 bounds = sprite.bounds.size;
            return new Vector3(size.x / bounds.x, size.y / bounds.y, 1f);
        }

        internal void SetSelected(bool selected)
        {
            selection.enabled = selected;
        }

        internal void Render(Match match)
        {
            Color bodyColor = sideColor;
            Color shownInnerColor = innerColor;
            if (!building.IsBuilt)
            {
                bodyColor.a *= style.ConstructionAlpha;
                shownInnerColor.a *= style.ConstructionAlpha;
            }

            body.color = bodyColor;
            inner.color = shownInnerColor;
            if (flag != null)
            {
                flag.color = bodyColor;
            }

            RenderAnnexes();
            RenderHealth();
            float progress = -1f;
            if (!building.IsBuilt)
            {
                progress = building.BuildProgress;
            }
            else if (building.IsBuildingHouse)
            {
                float buildTime = settings.buildings.houseBuildTime;
                progress = buildTime > 0f ? 1f - building.HouseBuildRemaining / buildTime : -1f;
            }
            else if (building.IsHiringAny)
            {
                float interval = BuildRules.GetHireInterval(match, building);
                progress = interval > 0f ? 1f - building.HireRemaining / interval : -1f;
            }

            RenderBar(progressBackground, progressFill, progress, barsTop);
        }

        /// <summary>Над полоской постройки или найма.</summary>
        private float HealthBarY => barsTop + style.ProgressBarSize.y * 1.6f;

        /// <summary>Полоска здоровья видна, только когда здание повреждено.</summary>
        private void RenderHealth()
        {
            float health = building.MaxHealth > 0f && building.Health < building.MaxHealth ? building.Health / building.MaxHealth : -1f;
            RenderBar(healthBackground, healthFill, health, HealthBarY);
        }

        /// <param name="value">Доля заполнения; отрицательная — полоска скрыта.</param>
        private void RenderBar(SpriteRenderer background, SpriteRenderer fill, float value, float y)
        {
            bool shown = value >= 0f;
            background.enabled = shown;
            fill.enabled = shown;
            if (!shown)
            {
                return;
            }

            Vector2 bar = style.ProgressBarSize;
            float width = bar.x * math.saturate(value);
            fill.transform.localPosition = new Vector3(-bar.x * 0.5f + width * 0.5f, y, 0f);
            fill.transform.localScale = new Vector3(width, bar.y, 1f);
        }

        private SpriteRenderer CreateRenderer(string objectName, int sortingOrder, Color color)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = style.SquareSprite;
            spriteRenderer.sortingOrder = sortingOrder;
            spriteRenderer.color = color;
            return spriteRenderer;
        }
    }
}
