using UnityEngine;

namespace Marchlands.View
{
    /// <summary>
    /// Вид местности в другое время года: картинки и цвета, которыми сценарий подменяет летние настройки сцены
    /// (поле, деревья, кусты и трава, лес по краям, берега рек, холмы). Пустое поле — остаётся как в сцене.
    /// </summary>
    [CreateAssetMenu(fileName = "Season", menuName = "Marchlands/Season Palette")]
    public sealed class SeasonPalette : ScriptableObject
    {
        [Header("Поле")]
        [SerializeField] private Color fieldColor = new Color(0.42f, 0.55f, 0.3f);
        [Tooltip("Цвет за краем карты (фон камеры).")]
        [SerializeField] private Color outsideColor = new Color(0.18f, 0.24f, 0.15f);
        [SerializeField] private Color[] patchColors;

        [Header("Деревья и кусты")]
        [SerializeField] private Sprite[] treeSprites;
        [SerializeField] private Color[] treeTints;
        [SerializeField] private Sprite[] bushSprites;
        [SerializeField] private Sprite[] tuftSprites;
        [SerializeField] private Color undergrowthColor = new Color(0.2f, 0.32f, 0.14f, 0.45f);

        [Header("Лес по краям")]
        [SerializeField] private Sprite borderCanopySprite;
        [SerializeField] private Color borderCanopyColor = new Color(0.82f, 0.9f, 0.8f);
        [SerializeField] private Color borderTreeShade = new Color(0.55f, 0.66f, 0.55f);

        [Header("Реки")]
        [SerializeField] private Sprite westRiverSprite;
        [SerializeField] private Sprite eastRiverSprite;
        [SerializeField] private Texture2D fieldRiverTexture;
        [SerializeField] private Color riverBankColor = new Color(0.62f, 0.55f, 0.38f);
        [SerializeField] private Color riverReedColor = new Color(0.27f, 0.4f, 0.18f, 0.55f);

        [Header("Холмы")]
        [SerializeField] private Color hillLightColor = new Color(0.86f, 0.84f, 0.5f, 0.22f);
        [SerializeField] private Color hillShadowColor = new Color(0.12f, 0.2f, 0.06f, 0.2f);
        [SerializeField] private Color hillContourColor = new Color(0.36f, 0.3f, 0.16f, 0.22f);

        public Color FieldColor => fieldColor;
        public Color OutsideColor => outsideColor;
        public Color[] PatchColors => patchColors;
        public Sprite[] TreeSprites => treeSprites;
        public Color[] TreeTints => treeTints;
        public Sprite[] BushSprites => bushSprites;
        public Sprite[] TuftSprites => tuftSprites;
        public Color UndergrowthColor => undergrowthColor;
        public Sprite BorderCanopySprite => borderCanopySprite;
        public Color BorderCanopyColor => borderCanopyColor;
        public Color BorderTreeShade => borderTreeShade;
        public Sprite WestRiverSprite => westRiverSprite;
        public Sprite EastRiverSprite => eastRiverSprite;
        public Texture2D FieldRiverTexture => fieldRiverTexture;
        public Color RiverBankColor => riverBankColor;
        public Color RiverReedColor => riverReedColor;
        public Color HillLightColor => hillLightColor;
        public Color HillShadowColor => hillShadowColor;
        public Color HillContourColor => hillContourColor;
    }
}
