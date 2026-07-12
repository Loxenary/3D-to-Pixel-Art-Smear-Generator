using UnityEngine;

namespace SmearFramework
{
    /// <summary>
    /// Controls which smear effects are active and how strong they are. Each effect can be toggled independently for A/B testing individual styles. Threshold controls the minimum bone speed before any smear kicks in.
    /// </summary>
    [CreateAssetMenu(fileName = "SmearEffectsConfig", menuName = "Smear Framework/Config/Smear Effects")]
    public class SmearEffectsConfig : ScriptableObject
    {
        [Tooltip("Master multiplier for smear strength across all enabled effects.\n1 = authored strength\n2 = stronger displacement / opacity / line length\n0.5 = softer smear overall")]
        [SerializeField, Range(0f, 4f)] private float _smearStrength = 1f;
        
        [Tooltip("Minimum per-vertex speed for multiple in-betweens.\n0 = allow any moving vertex\n2 = only clearly fast parts leave visible copies\n5+ = only extreme hits keep multiples visible")]
        [SerializeField, Range(0f, 30f)] private float _speedThreshold = 2f;

        [Tooltip("Stretches the mesh along the trajectory between frames -- gives a rubber-hose feel to fast limbs. Most recognizable smear type used in cartoon animation.")]
        [SerializeField] private bool _enableElongated = true;

        [Tooltip("How far the mesh stretches along the motion path.\n0.5 = subtle, barely noticeable\n2.0 = strong stretch like a cartoon punch\n5+ = exaggerated, stylized look")]
        [SerializeField, Range(0f, 10f)] private float _elongationMax = 0.5f;

        [Tooltip("Adds Perlin noise to the trailing edge of the stretch, breaking up the uniform deformation. Makes elongated smears look more hand-drawn and organic.")]
        [SerializeField] private bool _elongatedUseNoise = false;

        [Tooltip("Frequency of the Perlin noise applied to elongated smear edges.\n0.5 = large wavy variation\n2 = tight rough breakup (recommended if noise enabled)\n5 = very jagged, fragmented edges")]
        [SerializeField, Range(0.1f, 5f)] private float _noiseScale = 1f;

        [Tooltip("Shows ghost copies of the character at past and future positions -- the 'multiple in-betweens' technique. Common in Looney Tunes style and fighting game animations.")]
        [SerializeField] private bool _enableMultiples = true;

        [Tooltip("Ghost copies drawn behind the current pose.\n0 = no trail\n2 = mild trail (recommended)\n4 = heavy afterimage effect")]
        [SerializeField, Range(0, 5)] private int _pastCopies = 2;

        [Tooltip("Ghost copies drawn ahead of the current pose.\n0 = no leading ghost\n1 = subtle anticipation effect\n3 = strong leading echo")]
        [SerializeField, Range(0, 5)] private int _futureCopies = 1;

        [Tooltip("How densely the ghost copies are stacked along the path.\n1 = each copy at its own distinct position\n3 = copies clustered closer together for a denser trail")]
        [SerializeField, Range(1, 5)] private int _overlapCount = 1;
        
        [Tooltip("Multiplier for how far past copies move along the trajectory.\n1 = paper spacing\n2 = twice as far back\n0.5 = tighter overlap")]
        [SerializeField, Range(0.1f, 4f)] private float _pastDisplacement = 1f;

        [Tooltip("Multiplier for how far future copies move along the trajectory.\n1 = paper spacing\n2 = twice as far ahead\n0.5 = tighter overlap")]
        [SerializeField, Range(0.1f, 4f)] private float _futureDisplacement = 1f;

        [Tooltip("How quickly past copies fade as they get farther from the current pose.\n1 = no extra fade\n0.7 = each farther copy is dimmer\n0.4 = fast fade-out")]
        [SerializeField, Range(0.1f, 1f)] private float _pastOpacityFactor = 0.7f;

        [Tooltip("How quickly future copies fade as they get farther from the current pose.\n1 = no extra fade\n0.7 = each farther copy is dimmer\n0.4 = fast fade-out")]
        [SerializeField, Range(0.1f, 1f)] private float _futureOpacityFactor = 0.7f;

        [Tooltip("Draws thin speed lines trailing behind fast-moving vertices. Common in manga and comic-style action sequences.")]
        [SerializeField] private bool _enableMotionLines = false;

        [Tooltip("Minimum per-vertex speed for motion lines.\n0 = any moving vertex can spawn lines\n2 = only clearly fast parts emit lines\n5+ = only very fast peaks emit lines")]
        [SerializeField, Range(0f, 30f)] private float _motionLineSpeedThreshold = 2f;

        [Tooltip("Number of starting points for motion lines.\n5 = sparse, a few scattered lines\n20 = moderate density (recommended)\n50 = dense line burst")]
        [SerializeField, Range(1, 50)] private int _motionLineSeeds = 20;

        [Tooltip("How far back each line extends, measured in frames.\n0.5 = short stub\n1.5 = medium trail (recommended)\n3 = long dramatic speed lines")]
        [SerializeField, Range(0.1f, 5f)] private float _motionLineMaxLength = 1.5f;

        [Tooltip("Width of each motion line in world units.\n0.005 = hairline, very subtle\n0.01 = visible (recommended)\n0.05 = thick lines, usually too prominent")]
        [SerializeField, Range(0.001f, 0.05f)] private float _motionLineThickness = 0.01f;

        [Tooltip("Color and opacity of motion lines. Black at 50% alpha is the most common look. Lower alpha = subtler effect, higher = more prominent lines.")]
        [SerializeField] private Color _motionLineColor = new Color(0, 0, 0, 0.5f);

        [Tooltip("Reserved overlay tint is disabled. Smear pixels keep the character's own colors in the final pixel art output.")]
        [SerializeField] private Color _smearOverlayColor = new Color(0f, 0f, 0f, 0f);

        public float MultipleSpeedThreshold => _speedThreshold;
        public float SmearStrength => _smearStrength;

        public bool EnableElongated => _enableElongated;
        public float ElongationMax => _elongationMax;
        public bool ElongatedUseNoise => _elongatedUseNoise;
        public float NoiseScale => _noiseScale;

        public bool EnableMultiples => _enableMultiples;
        public int PastCopies => _pastCopies;
        public int FutureCopies => _futureCopies;
        public int OverlapCount => _overlapCount;
        public float PastDisplacement => _pastDisplacement;
        public float FutureDisplacement => _futureDisplacement;
        public float PastOpacityFactor => _pastOpacityFactor;
        public float FutureOpacityFactor => _futureOpacityFactor;

        public float MotionLineSpeedThreshold => _motionLineSpeedThreshold;

        public bool EnableMotionLines => _enableMotionLines;
        public int MotionLineSeeds => _motionLineSeeds;
        public float MotionLineMaxLength => _motionLineMaxLength;
        public float MotionLineThickness => _motionLineThickness;
        public Color MotionLineColor => _motionLineColor;

        public Color SmearOverlayColor => new Color(0f, 0f, 0f, 0f);
    }
}
