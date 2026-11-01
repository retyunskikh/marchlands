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
        // Цвет крыши: цвет стороны, у деревянной казармы — дерево с оттенком стороны.
        private Color roofColor;
        private SpriteRenderer roofDetail;
        private SpriteRenderer[] annexDetails;
        private Vector2 size;
        private SpriteRenderer scaffold;

        private SpriteRenderer body;
        private SpriteRenderer inner;
        private SpriteRenderer selection;
        private SpriteRenderer progressBackground;
        private SpriteRenderer progressFill;
        private SpriteRenderer healthBackground;
        private SpriteRenderer healthFill;
        private SpriteRenderer rangeRing;
        private SpriteRenderer[] holes = new SpriteRenderer[0];
        private SpriteRenderer[] fires = new SpriteRenderer[0];
        private SpriteRenderer[] smokes = new SpriteRenderer[0];
        private Vector2[] spots = new Vector2[0];
        private bool selected;
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
            roofColor = sideColor;
            transform.position = new Vector3(building.Position.x, building.Position.y, 0f);

            bool isHeadquarters = building.Type == BuildingType.Headquarters;
            bool isStable = building.Type == BuildingType.Stable;
            bool isDepot = building.Type == BuildingType.ArtilleryDepot;
            bool isBlockhouse = building.Type == BuildingType.Blockhouse;
            size = style.SizeOf(building);

            selection = CreateRenderer("Selection", style.BodySortingOrder - 1, style.SelectionColor);
            selection.transform.localScale = new Vector3(size.x + style.SelectionPadding * 2f, size.y + style.SelectionPadding * 2f, 1f);
            selection.enabled = false;

            // Артдепо — такой же навес, как у конюшни, но во дворе пушки и пирамиды ядер.
            Sprite baseSprite = isHeadquarters ? style.HeadquartersBaseSprite : isStable ? style.StableBaseSprite
                : isDepot ? style.DepotBaseSprite
                : isBlockhouse ? style.BlockhouseBaseSprite : style.BarracksBaseSprite;
            Sprite roofSprite = isHeadquarters ? style.HeadquartersRoofSprite : isStable || isDepot ? style.StableRoofSprite
                : isBlockhouse ? style.BlockhouseRoofSprite : style.BarracksRoofSprite;
            Sprite detailSprite = style.RoofDetailOf(building.Type);
            // Казарма, штаб и блокгауз до усиления — деревянные (тёс, частокол), после — каменные, под черепицей.
            Sprite woodRoof = style.WoodRoofOf(building.Type);
            if (!building.IsUpgraded && woodRoof != null)
            {
                roofSprite = woodRoof;
                detailSprite = null;
                roofColor = Color.Lerp(Color.white, sideColor, style.WoodRoofSideTint);
                roofColor.a = sideColor.a;
                baseSprite = style.WoodBaseOf(building.Type) ?? baseSprite;
            }
            else if (building.IsUpgraded)
            {
                baseSprite = style.StoneBaseOf(building.Type) ?? baseSprite;
            }

            if (baseSprite != null && roofSprite != null)
            {
                CreateEstate(baseSprite, roofSprite, isHeadquarters);
                if (detailSprite != null)
                {
                    roofDetail = CreateDetail("Roof Detail", detailSprite, body.transform.localPosition);
                    roofDetail.flipX = body.flipX;
                    roofDetail.transform.localScale = FitScale(detailSprite);
                }

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

            rangeRing = CreateRenderer("Sentry Range", style.BodySortingOrder - 2, style.SentryRangeColor);
            rangeRing.sprite = style.CircleSprite;
            rangeRing.transform.localScale = Vector3.one * (Volley.Of(building, settings.buildings).Range * 2f);
            rangeRing.enabled = false;
            if (style.ScaffoldSprite != null)
            {
                scaffold = CreateRenderer("Scaffold", style.BodySortingOrder + 4, style.ScaffoldColor);
                scaffold.sprite = style.ScaffoldSprite;
                scaffold.transform.localScale = new Vector3(size.x * style.ScaffoldPadding, size.y * style.ScaffoldPadding, 1f);
                scaffold.enabled = !building.IsBuilt || building.IsUpgrading;
            }

            CreateDamage();
        }

        /// <summary>Проломы в крыше, огонь и дым на местах повреждений; видны, когда здание потеряло достаточно здоровья.</summary>
        private void CreateDamage()
        {
            Vector2[] normalized = style.DamageSpots;
            if (normalized == null)
            {
                return;
            }

            float direction = building.Position.x > 0f ? -1f : 1f;
            spots = new Vector2[normalized.Length];
            holes = new SpriteRenderer[normalized.Length];
            fires = new SpriteRenderer[normalized.Length];
            smokes = new SpriteRenderer[normalized.Length];
            for (int i = 0; i < normalized.Length; i++)
            {
                spots[i] = new Vector2(normalized[i].x * size.x * direction, normalized[i].y * size.y);
                holes[i] = CreateRenderer($"Hole {i}", style.BodySortingOrder + 2, style.HoleColor);
                holes[i].sprite = style.SmokeSprite != null ? style.SmokeSprite : style.SquareSprite;
                holes[i].transform.localPosition = spots[i];
                holes[i].transform.localRotation = Quaternion.Euler(0f, 0f, i * 47f);
                holes[i].transform.localScale = Vector3.one * style.HoleSize;
                fires[i] = CreateRenderer($"Fire {i}", style.BodySortingOrder + 3, style.FireColor);
                fires[i].sprite = style.SmokeSprite != null ? style.SmokeSprite : style.SquareSprite;
                fires[i].transform.localPosition = spots[i];
                smokes[i] = CreateRenderer($"Smoke {i}", style.BodySortingOrder + 4, style.SmokeColor);
                smokes[i].sprite = style.SmokeSprite != null ? style.SmokeSprite : style.SquareSprite;
                holes[i].enabled = fires[i].enabled = smokes[i].enabled = false;
            }
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

            body = CreateRenderer("Roofs", style.BodySortingOrder + 1, roofColor);
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
                if (style.AnnexRoofDetailSprite != null)
                {
                    annexDetails ??= new SpriteRenderer[offsets.Length];
                    annexDetails[i] = CreateDetail($"Annex Detail {i}", style.AnnexRoofDetailSprite, position);
                    annexDetails[i].transform.localScale = FitScale(style.AnnexRoofDetailSprite, style.AnnexSize);
                    annexDetails[i].enabled = false;
                }
            }
        }

        /// <summary>Детали поверх крыши: тот же порядок отрисовки, что у крыши, но чуть ближе к камере — поверх неё.</summary>
        private SpriteRenderer CreateDetail(string name, Sprite sprite, Vector2 position)
        {
            SpriteRenderer detail = CreateRenderer(name, style.BodySortingOrder + 1, Color.white);
            detail.sprite = sprite;
            detail.transform.localPosition = new Vector3(position.x, position.y, DetailDepth);
            return detail;
        }

        // Насколько детали крыши ближе к камере, чем сама крыша (при равном порядке отрисовки рисуются поверх).
        private const float DetailDepth = -0.01f;

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
                if (annexDetails != null && annexDetails[i] != null)
                {
                    annexDetails[i].enabled = visible;
                    annexDetails[i].color = new Color(1f, 1f, 1f, visible && !built ? style.ConstructionAlpha : 1f);
                }

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

        /// <summary>Под курсором мыши: рамка бледнее, чем у выделенного.</summary>
        internal void SetHovered(bool hovered)
        {
            this.hovered = hovered;
            selection.enabled = selected || hovered;
            Color color = style.SelectionColor;
            if (!selected)
            {
                color.a *= HoverAlpha;
            }

            selection.color = color;
        }

        private bool hovered;

        // Насколько бледнее рамка здания под курсором.
        private const float HoverAlpha = 0.45f;

        internal void SetSelected(bool selected)
        {
            this.selected = selected;
            SetHovered(hovered);
        }

        /// <param name="underAssault">На здание идёт штурм: видны полоска здоровья и круг дальности часовых.</param>
        internal void Render(Match match, bool underAssault)
        {
            float health = building.MaxHealth > 0f ? math.saturate(building.Health / building.MaxHealth) : 1f;
            // Повреждённое здание темнеет.
            Color tint = Color.Lerp(style.DamagedTint, Color.white, health);
            Color bodyColor = roofColor * tint;
            bodyColor.a = roofColor.a;
            Color shownInnerColor = innerColor * tint;
            shownInnerColor.a = innerColor.a;
            if (!building.IsBuilt)
            {
                bodyColor.a *= style.ConstructionAlpha;
                shownInnerColor.a *= style.ConstructionAlpha;
            }

            body.color = bodyColor;
            inner.color = shownInnerColor;
            if (roofDetail != null)
            {
                roofDetail.color = new Color(1f, 1f, 1f, building.IsBuilt ? 1f : style.ConstructionAlpha);
            }
            if (flag != null)
            {
                flag.color = bodyColor;
            }

            // Пока здание строится или его усиливают, над ним строительные леса.
            bool scaffolding = !building.IsBuilt || building.IsUpgrading;
            if (scaffold != null && scaffold.enabled != scaffolding)
            {
                scaffold.enabled = scaffolding;
            }

            RenderAnnexes();
            RenderDamage(health);
            RenderHealth(health, underAssault || selected);
            rangeRing.enabled = building.IsBuilt && Volley.Of(building, settings.buildings).Shots > 0 && (underAssault || selected);
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
            else if (building.IsUpgrading)
            {
                float upgradeTime = settings.buildings.barracksUpgradeTime;
                progress = upgradeTime > 0f ? 1f - building.UpgradeRemaining / upgradeTime : -1f;
            }
            else if (building.IsOrderingGun)
            {
                float interval = settings.buildings.gunCrewHireInterval;
                progress = interval > 0f ? 1f - building.GunOrderRemaining / interval : -1f;
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

        /// <summary>Полоска здоровья видна у повреждённого здания, у выделенного и у того, на которое идёт штурм.</summary>
        private void RenderHealth(float health, bool always)
        {
            bool shown = always || health < 1f;
            RenderBar(healthBackground, healthFill, shown ? health : -1f, HealthBarY);
        }

        /// <summary>
        /// Ниже smokeHealth в крыше проломы и над ними дым (чем меньше здоровья, тем больше мест), ниже fireHealth —
        /// горит везде. Клубы дыма поднимаются и тают по кругу, огонь дрожит.
        /// </summary>
        private void RenderDamage(float health)
        {
            int damaged = health >= style.SmokeHealth || spots.Length == 0
                ? 0
                : health < style.FireHealth
                    ? spots.Length
                    : math.max(1, (int)math.ceil(spots.Length * (style.SmokeHealth - health) / math.max(1e-3f, style.SmokeHealth - style.FireHealth)));
            bool burning = health < style.FireHealth;
            float time = Time.time;
            for (int i = 0; i < spots.Length; i++)
            {
                bool shown = i < damaged;
                holes[i].enabled = shown;
                smokes[i].enabled = shown;
                fires[i].enabled = shown && burning;
                if (!shown)
                {
                    continue;
                }

                float phase = time / math.max(0.1f, style.SmokePeriod) + i * 0.37f;
                float t = phase - math.floor(phase);
                Vector2 drift = new Vector2(math.sin(phase * 2.1f + i) * 0.2f, t * style.SmokeRise);
                smokes[i].transform.localPosition = spots[i] + drift;
                smokes[i].transform.localScale = Vector3.one * math.lerp(style.SmokeSize.x, style.SmokeSize.y, t);
                Color smoke = style.SmokeColor;
                smoke.a *= (1f - t) * math.saturate(t * 4f);
                smokes[i].color = smoke;

                if (burning)
                {
                    float flicker = 0.8f + 0.3f * math.sin(time * 13f + i * 2f) * math.sin(time * 7f + i);
                    fires[i].transform.localScale = Vector3.one * (style.FireSize * flicker);
                    Color fire = style.FireColor;
                    fire.a *= 0.75f + 0.25f * flicker;
                    fires[i].color = fire;
                }
            }
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
