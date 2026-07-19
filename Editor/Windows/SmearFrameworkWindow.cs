using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using SmearFramework.Stages;
using SmearFramework.AnimationSampling;
using SmearFramework.DataTypes;
using SmearFramework.PixelArtConversion;
using System.IO;

namespace SmearFramework.Editor
{
    public class SmearFrameworkWindow : EditorWindow
    {
        enum ComparisonStage
        {
            Source3D,
            Pixelated
        }
        #region Fields

        [SerializeField] private GameObject _characterPrefab;
        [SerializeField] private AnimationClip _clip;
        [SerializeField] private SmearEffectsConfig _smearConfig;
        [SerializeField] private SmearOutputConfig _smearOutputConfig;
        [SerializeField] private OutputConfig _outputConfig;
        [SerializeField] private PostProcessConfig _postProcessConfig;
        [SerializeField] private VelocityConfig _velocityConfig;
        [SerializeField] private int _targetFps = 12;
        [SerializeField] private bool _reusePaletteAcrossFrames = true;
        [SerializeField] private bool _needsFullBake;
        private bool _cameraAngleDirty;

        private bool _showSmearDetails;
        private bool _showSmearOutputDetails;
        private bool _showOutputDetails;
        private bool _showVelocityDetails;
        private UnityEditor.Editor _smearEditor;
        private UnityEditor.Editor _smearOutputEditor;
        private UnityEditor.Editor _velocityEditor;

        private Texture2D[] _smearFrames;
        private Texture2D[] _cleanFrames;
        private Texture2D[] _clean3DFrames;
        private Texture2D[] _cleanPixelFrames;
        private int _frameCount;
        private int _smearFrameCount;
        private float _maxIntensity;
        private float _bakeTimeMs;
        private float[] _perFrameIntensity;
        private string _status = "";

        [SerializeField] private MotionData _cachedMotion;
        [SerializeField] private TrajectoryData _cachedTrajectory;
        [SerializeField] private GameObject _cachedPrefab;
        [SerializeField] private AnimationClip _cachedClip;

        private double _lastChangeTime;
        private bool _pendingRebake;
        private bool _pendingAutoPreview;
        [SerializeField] private bool _autoRebake; // off by default -- pixelization is heavy enough to make this lag
        private const double DEBOUNCE_SECONDS = 1.5;

        private const double PREVIEW_CAPTURE_DEBOUNCE_SECONDS = 0.12;
        private bool _pendingCapturePreview;
        private double _pendingCapturePreviewAt;

        private Vector2 _paramScroll;
        private int _frame;
        private bool _playing;
        private double _playStart;
        private double _nextPlaybackRepaintAt;

        [SerializeField] private PreviewViewState _leftView = PreviewViewState.Default;
        [SerializeField] private PreviewViewState _rightView = PreviewViewState.Default;
        private Vector3 _captureCameraEuler;
        [SerializeField] private bool _showHeatmap;
        [SerializeField] private bool _showPivotLine;
        [SerializeField] private ComparisonStage _leftComparisonStage = ComparisonStage.Pixelated;
        [SerializeField] private string _externalExportFolder;
        [SerializeField] private string _externalExportFolderName;
        const string Smear3DTempOutputRoot = SmearFrameworkPaths.Smear3DTempOutput;
        const string ExportFolderPrefKey = "SmearFramework.ExternalExportFolder";
        [SerializeField] private string _smear3DExportFolder;
        [SerializeField] private string _smear3DExportBaseName;
        private CaptureFrame _lastCaptureFrame;
        private Texture2D _heatmapTex;
        private int _lastHeatmapFrame = -1;
        private static readonly Color OutputTint = new Color(0.35f, 0.35f, 0.55f);
        private static readonly Color GroundGuideColor = new Color(0.2f, 1f, 0.35f, 1f);

        // runtime state: high-res source, last results, validation message
        private SmearFrameworkEditorState _state = new SmearFrameworkEditorState();
        // pipeline: preset/stage-list/validation decisions
        private SmearFrameworkEditorController _controller;
        [SerializeField] private PipelineMode _currentMode = PipelineMode.Full;

        // section composition root fields
        private LayoutSection _layoutSection;
        private SmearConfigSection _smearConfigSection;
        private PixelizationSection _pixelizationSection;
        private PreloadSection _preloadSection;
        private ResultsSection _resultsSection;
        private PreviewSection _previewSection;

        [SerializeField] private float _leftRatio = 0.35f;
        private bool _draggingSplitter;
        private const float SplitterWidth = 4f;
        private const float LeftRatioMin = 0.15f;
        private const float LeftRatioMax = 0.75f;
        private const float TwoPanelMinWidth = 600f;
        private static readonly Vector2 DefaultWindowMinSize = new Vector2(900f, 520f);

        private static readonly Color PipelineTint = new Color(0.28f, 0.45f, 0.68f);
        private static readonly Color InputTint = new Color(0.18f, 0.58f, 0.55f);
        private static readonly Color SmearTint = new Color(0.74f, 0.43f, 0.20f);
        private static readonly Color PixelTint = new Color(0.38f, 0.55f, 0.30f);
        private static readonly Color ActionTint = new Color(0.55f, 0.45f, 0.24f);

        private const float CardGap = 8f;
        private const float CardInnerTopGap = 4f;
        private const float CardInnerBottomGap = 6f;
        private const float GroupGap = 10f;
        private const float FieldGap = 2f;
        private const float PrimaryButtonHeight = 40f;
        private const float SecondaryButtonHeight = 28f;
        private const float CompactButtonHeight = 24f;

        // Default export destination -- inside the project's generated output folder so it shows up in Assets.
        internal static string DefaultExternalExportFolder()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", SmearFrameworkPaths.Output));
        }

        #endregion

        #region GUI

        [MenuItem("Smear Generator/Open Smear Generator %#s")]
        static void Open()
        {
            var w = GetWindow<SmearFrameworkWindow>("Smear Generator");
            w.minSize = DefaultWindowMinSize;
            w.maximized = true;
        }

        void OnEnable()
        {
            minSize = DefaultWindowMinSize;
            if (_state == null)      _state      = new SmearFrameworkEditorState();
            if (_controller == null) _controller = new SmearFrameworkEditorController(null, _currentMode);
            EnsureSections();
            LoadDefaultConfigs();
            EnsureExternalExportDefaults();
            RefreshValidation();
            EditorApplication.update -= TickPlayback;
            EditorApplication.update += TickPlayback;
        }

        // Stops editor callbacks owned by this window.
        void OnDisable()
        {
            EditorApplication.update -= TickPlayback;
            EditorApplication.update -= FlushPendingCapturePreview;
        }

        // Repaints playback at the selected capture rate instead of every editor update.
        void TickPlayback()
        {
            if (!_playing || !HasPreviewFrames()) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPlaybackRepaintAt) return;
            _nextPlaybackRepaintAt = now + 1d / Mathf.Max(1, _targetFps);
            Repaint();
        }

        void EnsureSections()
        {
            if (_layoutSection       == null) _layoutSection       = new LayoutSection();
            if (_smearConfigSection  == null) _smearConfigSection  = new SmearConfigSection();
            if (_pixelizationSection == null) _pixelizationSection = new PixelizationSection();
            if (_preloadSection      == null) _preloadSection      = new PreloadSection();
            if (_resultsSection      == null) _resultsSection      = new ResultsSection();
            if (_previewSection      == null) _previewSection      = new PreviewSection();
        }

        void OnGUI()
        {
            EnsureSections();
            if (_pendingRebake && EditorApplication.timeSinceStartup - _lastChangeTime >= DEBOUNCE_SECONDS)
            {
                _pendingRebake = false;
                DoQuickRebake();
            }
            if (_pendingAutoPreview && EditorApplication.timeSinceStartup - _lastChangeTime >= DEBOUNCE_SECONDS)
            {
                _pendingAutoPreview = false;
                if (_characterPrefab != null && _clip != null && string.IsNullOrEmpty(BuildInputProblem()))
                    DoPreviewAnimation();
            }

            if (position.width < TwoPanelMinWidth)
            {
                DrawLeftPanel();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                DrawLeftPanel();
                DrawSplitter();
                DrawRightPanel();
                EditorGUILayout.EndHorizontal();
                HandleSplitterDrag();
            }

            if (_pendingRebake || _pendingAutoPreview) Repaint();
        }

        void DrawLeftPanel()
        {
            float panelW = position.width < TwoPanelMinWidth
                ? position.width
                : Mathf.Round(position.width * _leftRatio);

            EditorGUILayout.BeginVertical(GUILayout.Width(panelW));
            _paramScroll = EditorGUILayout.BeginScrollView(_paramScroll);

            BeginSectionCard("Workflow Mode", PipelineTint);
            DrawDescription("Choose which stages this bake runs. This does not change the output file type.");
            DrawGroupGap();
            DrawWorkflowPresets();
            EndSectionCard();

            if (NeedsHighResPreload())
            {
                BeginSectionCard("High-res Source", PixelTint);
                DrawPreloadPanel();
                EndSectionCard();
            }

            BeginSectionCard("Input", InputTint);
            var previousPrefab = _characterPrefab;
            var previousClip = _clip;
            _characterPrefab = DrawObjectField(new GUIContent("Character", "Skinned character prefab with a SkinnedMeshRenderer. Must be a project asset, not a scene object."), _characterPrefab);
            DrawGroupGap();
            _clip = DrawObjectField(new GUIContent("Clip", "Animation clip to bake. Legacy and generic rigs both work; humanoid retargeting is applied automatically."), _clip);
            if (_clip != null && IsGeneratedClip(_clip))
            {
                _status = $"\"{_clip.name}\" is a generated output clip -- drop a source FBX clip instead.";
                _clip = null;
            }
            AutoDetectClip();
            if (previousPrefab != _characterPrefab || previousClip != _clip)
                HandleInputChanged();
            DrawGroupGap();
            DrawFieldLabel(new GUIContent("Target FPS", "Frames per second captured from the animation clip. Applies to both smear bake and pixel art output."));
            int previousTargetFps = _targetFps;
            _targetFps = Mathf.Max(1, EditorGUILayout.IntField(_targetFps));
            if (previousTargetFps != _targetFps)
                HandleFullBakeRequired();

            if (HasResultsPanel() && _state.ResultsStale)
                EditorGUILayout.HelpBox("Rebake needed -- clip or FPS changed.", MessageType.Warning);

            string inputProblem = BuildInputProblem();
            if (!string.IsNullOrEmpty(inputProblem))
            {
                EditorGUILayout.HelpBox(inputProblem, MessageType.Warning);
                DrawInputFixButtons();
            }

            EditorGUI.BeginDisabledGroup(_characterPrefab == null || _clip == null || !string.IsNullOrEmpty(inputProblem));
            if (GUILayout.Button("Preview animation", EditorStyles.miniButton, GUILayout.ExpandWidth(true), GUILayout.Height(SecondaryButtonHeight)))
                DoPreviewAnimation();
            EditorGUI.EndDisabledGroup();


            EndSectionCard();

            if (HasSmearStage())
                DrawSmearPhaseConfig();
            if (HasPixelizationStage())
                DrawPixelizationPhaseConfig();

            BeginSectionCard("Action", ActionTint);
            if (!string.IsNullOrEmpty(_state.ValidationStatus))
            {
                EditorGUILayout.HelpBox(_state.ValidationStatus, MessageType.Warning);
                EditorGUILayout.Space(FieldGap);
            }

            bool canRun = CanRunCurrentMode();
            EditorGUI.BeginDisabledGroup(!canRun);

            bool hasCache = NeedsCharacterClip() && HasSmearStage() &&
                IsCacheUsable() && _cachedPrefab == _characterPrefab && _cachedClip == _clip;

            if (hasCache)
            {
                string btnLabel = NeedsCharacterClip() ? "Rebake" : "Run pipeline";
                if (GUILayout.Button(new GUIContent(btnLabel,
                    _needsFullBake
                        ? "Runs the full pipeline from scratch -- required because FPS or motion params changed."
                        : "Skips velocity extraction and reuses the cached motion data."),
                    GUILayout.ExpandWidth(true), GUILayout.Height(PrimaryButtonHeight)))
                {
                    if (_needsFullBake) DoBake();
                    else DoQuickRebake();
                }
            }
            else
            {
                string label = NeedsCharacterClip() ? "Bake" : "Run pipeline";
                if (GUILayout.Button(label, GUILayout.ExpandWidth(true), GUILayout.Height(PrimaryButtonHeight)))
                    DoBake();
            }

            EditorGUI.EndDisabledGroup();

            if (!canRun)
                EditorGUILayout.HelpBox(BuildRunRequirementMessage(), MessageType.Info);

            if (_pendingRebake)
            {
                float remaining = (float)(DEBOUNCE_SECONDS - (EditorApplication.timeSinceStartup - _lastChangeTime));
                EditorGUILayout.HelpBox($"Auto re-baking in {remaining:F1}s...", MessageType.None);
            }

            if (!string.IsNullOrEmpty(_status))
            {
                DrawGroupGap();
                DrawDescription(_status);
            }

            EndSectionCard();
            if (HasResultsPanel())
            {
                EditorGUILayout.Space(4);
                DrawOutputPanel();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // Keep internal Unity assets hidden from the main result flow -- export is the user-facing action.
        void DrawOutputPanel()
        {
            BeginSectionCard("Results", OutputTint);
            _resultsSection.Draw(
                _state, _layoutSection,
                CanExportLatestPackage,
                ExportLatestPackage,
                CanExportLatestSmear3DResult,
                ExportLatestSmear3DResult);
            EndSectionCard();
            SyncExportFolderFromSection();
        }

        // Seed export defaults once, then preserve the user's edits until a new result arrives.
        void EnsureExternalExportDefaults()
        {
            if (string.IsNullOrWhiteSpace(_externalExportFolder))
            {
                string stored = EditorPrefs.GetString(ExportFolderPrefKey, "");
                // ignore stale paths that no longer exist or sit outside the project
                _externalExportFolder = IsUsableExportFolder(stored) ? stored : DefaultExternalExportFolder();
            }
            if (string.IsNullOrWhiteSpace(_externalExportFolderName))
                _externalExportFolderName = BuildExternalExportFolderName(_state.LastPixelResult);
            if (_resultsSection != null)
                _resultsSection.SeedDefaults(
                    _externalExportFolder, _externalExportFolderName,
                    _smear3DExportFolder, _smear3DExportBaseName);
        }

        // Pull the latest export fields the user typed in ResultsSection back into the serialized fields.
        void SyncExportFolderFromSection()
        {
            if (_resultsSection == null) return;
            string currentFolder = _resultsSection.ExternalExportFolder;
            if (!string.IsNullOrWhiteSpace(currentFolder) && currentFolder != _externalExportFolder)
            {
                _externalExportFolder = currentFolder;
                EditorPrefs.SetString(ExportFolderPrefKey, currentFolder);
            }
            string currentFolderName = _resultsSection.ExternalExportFolderName;
            if (!string.IsNullOrWhiteSpace(currentFolderName))
                _externalExportFolderName = currentFolderName;
        }

        // Returns true only when the folder exists on disk -- rejects stale paths from previous sessions.
        static bool IsUsableExportFolder(string folder)
        {
            return !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder);
        }

        // Derive the export folder name from the latest generated artifact.
        string BuildExternalExportFolderName(SpriteSheetResult result)
        {
            return PixelPackagePortableExporter.BuildDefaultExportBaseName(result);
        }

        // Re-seed the export fields after a new pixel result is generated.
        void SyncExternalExportDefaults()
        {
            string stored = EditorPrefs.GetString(ExportFolderPrefKey, "");
            _externalExportFolder = IsUsableExportFolder(stored) ? stored
                : (IsUsableExportFolder(_externalExportFolder) ? _externalExportFolder : DefaultExternalExportFolder());
            _externalExportFolderName = BuildExternalExportFolderName(_state.LastPixelResult);
            if (_resultsSection != null)
                _resultsSection.SetPixelExport(_externalExportFolder, _externalExportFolderName);
        }


        bool HasResultsPanel()
        {
            bool hasPixelResult = _state.LastPixelResult != null && !string.IsNullOrEmpty(_state.LastPixelResult.PackageFolder);
            bool hasSmear3DResult = _state.LastSmear3DResult != null && !string.IsNullOrEmpty(_state.LastSmear3DResult.PrefabPath);
            return hasPixelResult || hasSmear3DResult;
        }

        // Keeps completed output visible while making its outdated settings explicit.
        void MarkResultsStale()
        {
            if (HasResultsPanel())
                _state.ResultsStale = true;
        }


        void SelectProjectAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // Export is available only when the latest pixel run produced a package folder and both export fields are set.
        bool CanExportLatestPackage()
        {
            return _state.LastPixelResult != null
                && !string.IsNullOrEmpty(_state.LastPixelResult.PackageFolder)
                && Directory.Exists(_state.LastPixelResult.PackageFolder)
                && !string.IsNullOrWhiteSpace(_externalExportFolder)
                && !string.IsNullOrWhiteSpace(_externalExportFolderName);
        }

        // Copy the latest pixel package to the external folder and keep the status line short.
        void ExportLatestPackage()
        {
            string dest = PixelPackagePortableExporter.ExportToExternalFolder(_state.LastPixelResult, _externalExportFolder, _externalExportFolderName);
            if (!string.IsNullOrEmpty(dest))
            {
                EditorPrefs.SetString(ExportFolderPrefKey, _externalExportFolder);
                _status = "Package exported.";
                EditorUtility.RevealInFinder(dest);
                Repaint();
            }
        }

        // Set defaults for folder and base name when the fields are still blank.
        void EnsureSmear3DExportDefaults()
        {
            if (string.IsNullOrWhiteSpace(_smear3DExportFolder))
                _smear3DExportFolder = _smearOutputConfig != null ? _smearOutputConfig.OutputDirectory : SmearFrameworkPaths.Output;
            if (string.IsNullOrWhiteSpace(_smear3DExportBaseName))
                _smear3DExportBaseName = BuildActiveBaseName();
        }


        // Accepts only the Assets root or one of its descendants.
        static bool IsValidProjectAssetFolder(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return false;
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            return normalized == "Assets" || normalized.StartsWith("Assets/");
        }


        // Returns true only when all required data is available and both export fields are valid.
        bool CanExportLatestSmear3DResult()
        {
            if (!IsValidProjectAssetFolder(_smear3DExportFolder)) return false;
            if (string.IsNullOrWhiteSpace(_smear3DExportBaseName)) return false;
            return _state.LastSmear3DResult != null
                && _state.AvailableSmearMeta != null
                && _cachedMotion != null
                && _characterPrefab != null
                && _clip != null;
        }

        // Re-run 3D asset generation into exportFolder using baseName so references stay self-contained.
        SmearScene3DExporter.Result ExportLatestSmear3DResult(string exportFolder, string baseName)
        {
            if (!IsValidProjectAssetFolder(exportFolder))
            {
                _status = "3D export folder must be inside Assets.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(baseName))
            {
                _status = "3D export filename must not be blank.";
                return null;
            }
            var result = SmearScene3DExporter.Export(
                _characterPrefab, _clip, _state.AvailableSmearMeta, _cachedMotion, exportFolder, baseName);
            if (result != null)
            {
                _state.LastSmear3DResult = result;
                _state.LastSmear3DResultIsTemporary = false;
                _status = "3D folder exported.";
                AssetDatabase.Refresh();
                return result;
            }
            _status = "3D export failed -- see console";
            return null;
        }

        // Export using the configured folder and base name.
        void ExportLatestSmear3DResult()
        {
            EnsureSmear3DExportDefaults();
            ExportLatestSmear3DResult(_smear3DExportFolder, _smear3DExportBaseName);
        }

        // Convert an Assets-relative path to an absolute system path.
        static string ToSystemPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return Application.dataPath;
            string trimmed = assetPath.Replace('\\', '/').TrimEnd('/');
            if (IsValidProjectAssetFolder(trimmed))
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", trimmed));
            return trimmed;
        }

        // Convert an absolute system path to an Assets-relative path when inside the project.
        static string ToAssetPath(string systemPath)
        {
            string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            string normal   = Path.GetFullPath(systemPath).Replace('\\', '/');
            if (normal == dataPath || normal.StartsWith(dataPath + "/"))
                return "Assets" + normal.Substring(dataPath.Length);
            return normal;
        }

        // Schedule validation / quick rebake after config changes.
        void HandleConfigChanged()
        {
            MarkResultsStale();
            RefreshValidation();
            if (_autoRebake && IsCacheUsable() && _cachedPrefab == _characterPrefab && _cachedClip == _clip)
            {
                _lastChangeTime = EditorApplication.timeSinceStartup;
                _pendingRebake = true;
            }
        }

        // Marks the cache stale and flags that the next rebake must be a full bake.
        void HandleFullBakeRequired()
        {
            _needsFullBake = true;
            MarkResultsStale();
            RefreshValidation();
        }

        // Draw settings owned by the Smear Frame phase.
        void DrawSmearPhaseConfig()
        {
            BeginSectionCard("Smear Frame", SmearTint);

            if (HasResultsPanel() && _state.ResultsStale)
                EditorGUILayout.HelpBox("Rebake needed -- smear settings changed.", MessageType.Warning);

            var prevSmear    = _smearConfig;
            var prevVelocity = _velocityConfig;

            _smearConfigSection.Draw(
                _smearConfig, _velocityConfig,
                ref _smearEditor, ref _velocityEditor,
                _layoutSection, HandleConfigChanged);
            _smearConfig    = _smearConfigSection.CurrentSmearConfig;
            _velocityConfig = _smearConfigSection.CurrentVelocityConfig;

            // asset swap -- auto trigger a quick rebake
            bool smearSwapped    = _smearConfig    != prevSmear;
            bool velocitySwapped = _velocityConfig != prevVelocity;
            if ((smearSwapped || velocitySwapped) && _characterPrefab != null && _clip != null
                && IsCacheUsable() && _cachedPrefab == _characterPrefab && _cachedClip == _clip)
            {
                _lastChangeTime = EditorApplication.timeSinceStartup;
                _pendingRebake  = true;
            }

            // velocity params invalidate cached motion -- force full bake
            if (_smearConfigSection.VelocityParamChanged)
                HandleFullBakeRequired();

            DrawGroupGap();
            EditorGUILayout.LabelField("Debug output", EditorStyles.miniBoldLabel);
            bool canHeatmap = _cachedMotion != null && _lastCaptureFrame != null;
            if (!canHeatmap && _showHeatmap) _showHeatmap = false;
            EditorGUI.BeginDisabledGroup(!canHeatmap);
            _showHeatmap = EditorGUILayout.ToggleLeft(
                _showHeatmap ? "Velocity heatmap  (blue=slow, red=fast)" : "Velocity heatmap",
                _showHeatmap);
            EditorGUI.EndDisabledGroup();
            if (!canHeatmap)
                EditorGUILayout.HelpBox("Bake first to view the heatmap.", MessageType.Info);
            EndSectionCard();
        }

        // Draw settings owned by the Pixelization phase.
        void DrawPixelizationPhaseConfig()
        {
            BeginSectionCard("Pixelization", PixelTint);
            if (HasResultsPanel() && _state.ResultsStale)
                EditorGUILayout.HelpBox("Rebake needed -- pixel settings changed.", MessageType.Warning);
            _pixelizationSection.Draw(
                _outputConfig, _postProcessConfig,
                ref _reusePaletteAcrossFrames,
                _layoutSection, ref _showPivotLine, HandleConfigChanged);
            _outputConfig      = _pixelizationSection.CurrentOutputConfig;
            _postProcessConfig = _pixelizationSection.CurrentPostProcessConfig;
        }

        // Draw nested config inspectors in narrow mode without forcing a wide horizontal layout.
        void DrawEmbeddedInspector(UnityEditor.Editor editor)
            => _layoutSection.DrawEmbeddedInspector(editor, position.width < TwoPanelMinWidth);


        // Start a colored inspector card for the left panel.
        void BeginSectionCard(string title, Color tint) => _layoutSection.BeginSectionCard(title, tint);

        // Finish a colored inspector card.
        void EndSectionCard() => _layoutSection.EndSectionCard();


        // Draw a miniBoldLabel and a small gap for a field label.
        void DrawFieldLabel(GUIContent label) => _layoutSection.DrawFieldLabel(label);

        // String overload -- wraps to GUIContent.
        void DrawFieldLabel(string label) => _layoutSection.DrawFieldLabel(label);

        // Draw label-above-field for any UnityObject field.
        T DrawObjectField<T>(GUIContent label, T value) where T : UnityEngine.Object
        {
            DrawFieldLabel(label);
            return (T)EditorGUILayout.ObjectField(value, typeof(T), false);
        }

        // Draw word-wrapped mini text, skipped when empty.
        void DrawDescription(string text) => _layoutSection.DrawDescription(text);

        // Insert a standard group gap between related control groups.
        void DrawGroupGap() => _layoutSection.DrawGroupGap();


        void DrawCapturePresetMenu(Rect buttonRect)
        {
            var menu = new GenericMenu();
            foreach (var preset in CapturePresetUtility.OrderedPresets)
            {
                var capturedPreset = preset;
                menu.AddItem(
                    new GUIContent(CapturePresetUtility.GetLabel(capturedPreset)),
                    false,
                    () => ApplyCapturePreset(capturedPreset));
            }
            menu.DropDown(buttonRect);
        }

        // removed: DrawCaptureLightControls -- capture is always unlit; see 2026-07-08 decision

        void DrawSplitter() => _layoutSection.DrawSplitter(SplitterWidth);

        // Returns a human-readable name for the current stage combination shown in the preview header.
        string GetPipelineLabel()
        {
            bool smear = HasSmearStage();
            bool pixel = HasPixelizationStage();
            if (smear && pixel) return "Pixel Art With Smearing";
            if (smear)          return "3D Animation with Smearing";
            if (pixel)          return "Pixel Art";
            return "Preview";
        }

        // Load the bundled default config assets on first open so the user has a working setup immediately.
        void LoadDefaultConfigs()
        {
            if (_smearConfig == null)
                _smearConfig = AssetDatabase.LoadAssetAtPath<SmearEffectsConfig>(
                    SmearFrameworkPaths.DefaultData + "/SmearEffects/SmearEffects_Default.asset");
            if (_velocityConfig == null)
                _velocityConfig = AssetDatabase.LoadAssetAtPath<VelocityConfig>(
                    SmearFrameworkPaths.DefaultData + "/Velocity/VelocityConfig_Default.asset");
            if (_smearOutputConfig == null)
                _smearOutputConfig = AssetDatabase.LoadAssetAtPath<SmearOutputConfig>(
                    SmearFrameworkPaths.DefaultData + "/Output/SmearOutputConfig_Default.asset");
            if (_outputConfig == null)
                _outputConfig = AssetDatabase.LoadAssetAtPath<OutputConfig>(
                    SmearFrameworkPaths.DefaultData + "/Output/OutputConfig_Default.asset");
            if (_postProcessConfig == null)
                _postProcessConfig = AssetDatabase.LoadAssetAtPath<PostProcessConfig>(
                    SmearFrameworkPaths.DefaultData + "/PostProcess/PostProcessConfig_Default.asset");
        }

        void HandleSplitterDrag()
            => _layoutSection.HandleSplitterDrag(position.width, ref _leftRatio, ref _draggingSplitter, SplitterWidth, LeftRatioMin, LeftRatioMax);

        void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical();

            if (!HasPreviewFrames())
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Bake to see preview", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            DrawTransportRow();

            if (_playing && _frameCount > 0)
            {
                float fps = Mathf.Max(1, _targetFps);
                _frame = (int)((EditorApplication.timeSinceStartup - _playStart) * fps) % _frameCount;
            }

            bool has3DLeft    = _clean3DFrames    != null && _clean3DFrames.Length    > 0;
            bool hasPixelLeft = _cleanPixelFrames != null && _cleanPixelFrames.Length > 0;
            bool pixelMode    = HasPixelizationStage();
            bool smearMode    = HasSmearStage();
            bool allowLeftToggle = smearMode && pixelMode && has3DLeft && hasPixelLeft;

            bool showPixelLeft = _previewSection.CurrentLeftSource == PreviewSection.LeftSource.Pixelated;
            _cleanFrames = (showPixelLeft && hasPixelLeft)
                ? _cleanPixelFrames
                : (has3DLeft ? _clean3DFrames : _cleanPixelFrames);
            bool canHeatmap = _cachedMotion != null && _lastCaptureFrame != null && !showPixelLeft;

            _previewSection.DrawPaneHeaders(allowLeftToggle, has3DLeft, hasPixelLeft, smearMode, pixelMode);

            float rightPaneWidth = position.width < TwoPanelMinWidth
                ? position.width - 16f
                : position.width - Mathf.Round(position.width * _leftRatio) - SplitterWidth - 24f;
            float availW = Mathf.Max(rightPaneWidth, 200f);
            float frameH = Mathf.Clamp(position.height - 96f, 160f, availW * 0.55f);
            var availableRect = new UnityEngine.Rect(0f, 0f, availW, frameH);

            // Draw() sets LeftPaneRect/RightPaneRect but no longer handles input.
            // We call HandleViewportInput here so the overlay rect is from the same frame.
            _previewSection.Draw(
                availableRect, _state, ref _leftView, ref _rightView,
                () => _frameCount,
                _ => (_cleanFrames  != null && _frame < _cleanFrames.Length  ? _cleanFrames[_frame]  : null),
                _ => (_smearFrames  != null && _frame < _smearFrames.Length  ? _smearFrames[_frame]  : null));
            DrawPixelSmearIndicator(_previewSection.RightPaneRect);

            // Overlay rect is now based on LeftPaneRect set by this frame's Draw() call.
            Rect overlayRect = GetViewportOverlayRect(_previewSection.LeftPaneRect);
            bool lv = _previewSection.HandleViewportInput(_previewSection.LeftPaneRect,  overlayRect, ref _leftView);
            bool rv = _previewSection.HandleViewportInput(_previewSection.RightPaneRect, Rect.zero,    ref _rightView);
            if (lv || rv) Repaint();

            // Narrow the pan cursor so it doesn't cover the overlay widget.
            Rect aboveOverlay = new Rect(_previewSection.LeftPaneRect.x, _previewSection.LeftPaneRect.y,
                                         _previewSection.LeftPaneRect.width, overlayRect.y - _previewSection.LeftPaneRect.y);
            if (aboveOverlay.height > 0f)
                EditorGUIUtility.AddCursorRect(aboveOverlay, MouseCursor.Pan);
            else
                EditorGUIUtility.AddCursorRect(_previewSection.LeftPaneRect, MouseCursor.Pan);

            if (_showHeatmap && canHeatmap)
            {
                if (_lastHeatmapFrame != _frame)
                {
                    if (_heatmapTex != null) DestroyImmediate(_heatmapTex);
                    _lastHeatmapFrame = _frame;
                    Texture2D cleanTex = _cleanFrames != null && _frame < _cleanFrames.Length ? _cleanFrames[_frame] : null;
                    _heatmapTex = VelocityHeatmapRenderer.BuildOverlay(_cachedMotion, _frame, _lastCaptureFrame, cleanTex);
                }
                if (_heatmapTex != null)
                    _previewSection.DrawZoomedTexture(_previewSection.LeftPaneRect, _heatmapTex, _leftView);
            }

            float inputGroundY = 0.15f;
            if (_lastCaptureFrame != null && !showPixelLeft)
                inputGroundY = VelocityHeatmapRenderer.GetGroundLineViewportY(_lastCaptureFrame);
            float outputGroundY = _outputConfig != null
                ? Mathf.Clamp01(_outputConfig.PivotNormalized.y)
                : inputGroundY;
            float outputPivotX = _outputConfig != null
                ? Mathf.Clamp01(_outputConfig.PivotNormalized.x)
                : 0.5f;

            DrawViewportOverlay(_previewSection.LeftPaneRect, ref _leftView);
            if (_showPivotLine)
            {
                _previewSection.DrawZoomedHorizontalGuide(
                    _previewSection.LeftPaneRect,
                    inputGroundY,
                    _leftView,
                    GroundGuideColor,
                    GroundGuideColor);
                _previewSection.DrawZoomedPivotCross(
                    _previewSection.RightPaneRect,
                    outputPivotX,
                    outputGroundY,
                    _rightView,
                    GroundGuideColor);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"{_frameCount} frames  |  {_smearFrameCount} smeared  |  {_bakeTimeMs}ms total",
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Bake

        // dispatch: prefer the 3D path whenever a char+clip is dropped, fall back to preload otherwise
        void DoBake()
        {
            string inputProblem = BuildInputProblem();
            if (!string.IsNullOrEmpty(inputProblem))
            {
                _status = inputProblem;
                return;
            }

            if (_characterPrefab != null && _clip != null)
                DoSmearBake();
            else if (_state.AvailableHighRes != null)
                DoPixelArtFromPreload();
            else
                _status = "drop a character + clip, or load a *_highres.png";
        }

        // Run the current stage list against a fresh character instance.
        void DoSmearBake()
        {
            _status = "Bake - computing velocity...";
            _state.LastPixelResult = null;
            _state.LastSmear3DResult = null;
            _state.LastSmear3DResultIsTemporary = false;

            bool userWantsSmear = HasSmearStage();

            var instance = Instantiate(_characterPrefab);
            instance.name = MakeWorkingInstanceName();
            var config = BuildConfig(userWantsSmear, HasPixelizationStage());

            try
            {
                var stages = CloneCurrentStages();

                var pipeline = new SmearPipeline(config, instance, _clip);
                foreach (var st in stages) pipeline.AddStage(st);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                RunPipelineWithProgress(pipeline, "Baking smear frames");
                EnsurePreviewFrames(pipeline.Context);
                sw.Stop();
                float smearTime = sw.ElapsedMilliseconds;

                if (pipeline.Context.Has("motion"))
                    _cachedMotion = pipeline.Context.Get<MotionData>("motion");
                if (pipeline.Context.Has("trajectory"))
                    _cachedTrajectory = pipeline.Context.Get<TrajectoryData>("trajectory");
                _cachedPrefab = _characterPrefab;
                _cachedClip = _clip;
                _needsFullBake = false;

                UpdateAvailableArtifacts(pipeline.Context, "in-session bake");

                _smearFrames = PickPreviewFrames(pipeline.Context);
                CollectSmearStats(pipeline.Context);
                UpdateResultArtifacts(pipeline.Context);

                _clean3DFrames = null;
                _cleanPixelFrames = null;
                if (userWantsSmear)
                {
                    _clean3DFrames = BakeWithCache(useSmear: false, includePixelOutput: false, out _, out _);
                    if (HasPixelizationStage())
                        _cleanPixelFrames = BakeWithCache(useSmear: false, includePixelOutput: true, out _, out _);
                    _cleanFrames = _clean3DFrames ?? _cleanPixelFrames;
                }
                else if (HasPixelizationStage())
                {
                    SetInputPreviewFrames(pipeline.Context);
                }

                // when user picked Smear without Pixelization, also produce a 3D-usable prefab
                // so the smear bake still leaves a reusable output instead of only an intermediate PNG
                if (userWantsSmear && !HasPixelizationStage())
                    _state.LastSmear3DResult = GenerateTemporarySmear3DResult();

                UpdateFrameCount();
                _bakeTimeMs = smearTime;
                _frame = 0;
                _playStart = EditorApplication.timeSinceStartup;
                _playing = true;
                _status = BuildBakeStatus(pipeline.Context, smearTime, _state.LastSmear3DResult);
                RefreshValidation();
                ShowResultPopup(_state.LastPixelResult, _state.LastSmear3DResult, smearTime);
            }
            finally
            {
                DestroyImmediate(instance);
                DestroyImmediate(config);
            }
            _cameraAngleDirty = false;
        }

        // Returns null when skipped or on failure; sets _state.LastSmear3DResultIsTemporary on success.
        SmearScene3DExporter.Result GenerateTemporarySmear3DResult()
        {
            if (_state.AvailableSmearMeta == null || _cachedMotion == null)
                return null;
            if (_characterPrefab == null || _clip == null)
                return null;

            string baseName = BuildActiveBaseName();
            string folder = $"{Smear3DTempOutputRoot}/{baseName}";

            try
            {
                var result = SmearScene3DExporter.Export(
                    _characterPrefab, _clip, _state.AvailableSmearMeta, _cachedMotion, folder, baseName);
                if (result != null)
                    _state.LastSmear3DResultIsTemporary = true;
                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[SmearGenerator] 3D export failed: " + ex);
                _status = "Temporary 3D preview failed -- see console";
                return null;
            }
        }

        // Quick clean bake regardless of the stage list -- lets the artist see the source animation
        // before deciding which stages to wire up. Reuses SmearBakePhase with smear effects off.
        void DoPreviewAnimation()
        {
            if (_characterPrefab == null || _clip == null) return;
            string inputProblem = BuildInputProblem();
            if (!string.IsNullOrEmpty(inputProblem))
            {
                _status = inputProblem;
                return;
            }

            _status = $"Previewing {_clip.name}...";

            var instance = Instantiate(_characterPrefab);
            instance.name = MakeWorkingInstanceName();
            var config = BuildConfig(false, false);

            var pipeline = new SmearPipeline(config, instance, _clip);
            pipeline.AddStage(new HighResCaptureStage());

            var sw = System.Diagnostics.Stopwatch.StartNew();
            pipeline.RunAll();
            sw.Stop();

            if (pipeline.Context.Has("motion"))
                _cachedMotion = pipeline.Context.Get<MotionData>("motion");
            if (pipeline.Context.Has("trajectory"))
                _cachedTrajectory = pipeline.Context.Get<TrajectoryData>("trajectory");
            _cachedPrefab = _characterPrefab;
            _cachedClip = _clip;

            if (pipeline.Context.Has("frames_highres"))
            {
                _state.AvailableHighRes = pipeline.Context.Get<RawFrameData>("frames_highres");
                _state.HighResSourceLabel = "preview";
            }
            if (pipeline.Context.Has("capture_frame"))
                _lastCaptureFrame = pipeline.Context.Get<CaptureFrame>("capture_frame");
            if (_heatmapTex   != null) { DestroyImmediate(_heatmapTex);    _heatmapTex    = null; }
            _lastHeatmapFrame = -1;

            _clean3DFrames = PickPreviewFrames(pipeline.Context);
            _cleanPixelFrames = null;
            _cleanFrames = _clean3DFrames;
            UpdateFrameCount();
            _smearFrameCount = 0;
            _maxIntensity = 0;
            _perFrameIntensity = null;
            _bakeTimeMs = sw.ElapsedMilliseconds;
            _frame = 0;
            _playStart = EditorApplication.timeSinceStartup;
            _playing = true;
            _status = $"Preview done: {_clip.name} - {_bakeTimeMs}ms";

            DestroyImmediate(instance);
            DestroyImmediate(config);
            RefreshValidation();
        }

        // Drop stale frames and motion data when the user swaps the source asset or clip.
        void HandleInputChanged()
        {
            _cachedMotion = null;
            _cachedTrajectory = null;
            _cachedPrefab = null;
            _cachedClip = null;
            _state.AvailableHighRes = null;
            _state.AvailableSmearMeta = null;
            _state.HighResSourceLabel = null;
            _smearFrames = null;
            _cleanFrames = null;
            _clean3DFrames = null;
            _cleanPixelFrames = null;
            _frameCount = 0;
            _smearFrameCount = 0;
            _maxIntensity = 0;
            _perFrameIntensity = null;
            _frame = 0;
            _playing = false;
            _pendingRebake = false;
            _pendingAutoPreview = false;
            _state.LastPixelResult = null;
            _state.LastSmear3DResult = null;
            _state.LastSmear3DResultIsTemporary = false;

            if (_characterPrefab != null && _clip != null)
            {
                _status = $"Input changed: {_characterPrefab.name} + {_clip.name}. Previewing...";
                _pendingAutoPreview = true;
                _lastChangeTime = EditorApplication.timeSinceStartup;
            }
            else
            {
                _pendingAutoPreview = false;
                _status = "Input changed. Drop a character + clip to preview.";
            }
            RefreshValidation();
            Repaint();
        }

        // Pick frames_pixelized when present, otherwise frames_highres.
        Texture2D[] PickPreviewFrames(PipelineContext ctx)
        {
            if (ctx.Has("frames_pixelized"))
                return ctx.Get<RawFrameData>("frames_pixelized").Frames;
            if (ctx.Has("frames_highres"))
                return ctx.Get<RawFrameData>("frames_highres").Frames;
            return null;
        }

        // Keep the left preview populated with the captured source frames for pixel-only workflows.
        void SetInputPreviewFrames(PipelineContext ctx)
        {
            _clean3DFrames = ctx.Has("frames_highres")
                ? ctx.Get<RawFrameData>("frames_highres").Frames
                : null;
            _cleanPixelFrames = null;
            _cleanFrames = _clean3DFrames;
        }

        // fresh stage instances so each run gets a clean state (some stages keep internal scratch)
        List<IPipelineStage> CloneCurrentStages()
        {
            return _controller.CloneStages();
        }

        // any phase that does smear math is in the list
        bool HasSmearStage()
        {
            return _controller.Pipeline.Stages.Any(s => s is SmearBakePhase);
        }

        // user added pixel art conversion as a downstream consumer
        bool HasPixelizationStage()
        {
            return _controller.Pipeline.Stages.Any(s => s is PixelizationPhase);
        }

        // Capture temporary preview frames when the selected phase did not write pixels.
        void EnsurePreviewFrames(PipelineContext ctx)
        {
            if (ctx == null || ctx.Has("frames_highres")) return;
            if (ctx.Target == null || ctx.Clip == null) return;
            new HighResCaptureStage().Execute(ctx);
        }

        // Workflow 2: pixelize whatever frames_highres is currently available (cache or disk)
        void DoPixelArtFromPreload()
        {
            if (_state.AvailableHighRes == null)
            {
                _status = "no high-res sheet available -- run Smear Frame first or load one from disk";
                return;
            }
            _state.LastSmear3DResult = null;
            _state.LastSmear3DResultIsTemporary = false;
            _status = $"Pixelizing ({_state.HighResSourceLabel})...";
            var config = BuildConfig(false, true);

            var pipeline = new SmearPipeline(config, null, null);
            var stages = CloneCurrentStages().Where(st => st is PixelizationPhase).ToList();
            if (stages.Count == 0)
                stages.Add(new PixelizationPhase());
            foreach (var st in stages) pipeline.AddStage(st);

            pipeline.Context.Set("frames_highres", _state.AvailableHighRes);
            if (_state.AvailableSmearMeta != null)
                pipeline.Context.Set("smear_data", _state.AvailableSmearMeta);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            RunPipelineWithProgress(pipeline, "Pixelizing");
            sw.Stop();

            _smearFrames = PickPreviewFrames(pipeline.Context);
            SetInputPreviewFrames(pipeline.Context);
            _frameCount = _smearFrames != null ? _smearFrames.Length : 0;
            _bakeTimeMs = sw.ElapsedMilliseconds;
            _frame = 0;
            _playing = false;
            _status = BuildSpriteSheetStatus(pipeline.Context, _bakeTimeMs, "");
            if (pipeline.Context.Has("sprite_sheet"))
                _state.LastPixelResult = pipeline.Context.Get<SpriteSheetResult>("sprite_sheet");
            _state.ResultsStale = false;
            SyncExternalExportDefaults();

            DestroyImmediate(config);
            RefreshValidation();
            ShowResultPopup(_state.LastPixelResult, _state.LastSmear3DResult, _bakeTimeMs);
        }

        void DoQuickRebake()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _smearFrames = BakeWithCache(useSmear: true, includePixelOutput: HasPixelizationStage(), out _, out var smearCtx);
            _clean3DFrames = BakeWithCache(useSmear: false, includePixelOutput: false, out _, out _);
            _cleanPixelFrames = HasPixelizationStage()
                ? BakeWithCache(useSmear: false, includePixelOutput: true, out _, out _)
                : null;
            _cleanFrames = ResolveLeftComparisonStage(HasSmearStage() && HasPixelizationStage(), _clean3DFrames != null, _cleanPixelFrames != null) == ComparisonStage.Pixelated
                && _cleanPixelFrames != null ? _cleanPixelFrames : _clean3DFrames;
            sw.Stop();

            UpdateAvailableArtifacts(smearCtx, "in-session bake");
            UpdateResultArtifacts(smearCtx);
            UpdateFrameCount();
            _bakeTimeMs = sw.ElapsedMilliseconds;
            _frame = 0;
            _playStart = EditorApplication.timeSinceStartup;
            _playing = true;
            if (HasSmearStage() && !HasPixelizationStage())
                _state.LastSmear3DResult = GenerateTemporarySmear3DResult();
            else
            {
                _state.LastSmear3DResult = null;
                _state.LastSmear3DResultIsTemporary = false;
            }
            _status = BuildBakeStatus(smearCtx, _bakeTimeMs, _state.LastSmear3DResult);
            RefreshValidation();
            _cameraAngleDirty = false;
            ShowResultPopup(_state.LastPixelResult, _state.LastSmear3DResult, _bakeTimeMs);
        }

        ComparisonStage ResolveLeftComparisonStage(bool allowToggle, bool has3DLeft, bool hasPixelLeft)
        {
            if (!allowToggle)
                return hasPixelLeft && !has3DLeft ? ComparisonStage.Pixelated : ComparisonStage.Source3D;
            if (_leftComparisonStage == ComparisonStage.Pixelated && hasPixelLeft) return ComparisonStage.Pixelated;
            if (_leftComparisonStage == ComparisonStage.Source3D && has3DLeft) return ComparisonStage.Source3D;
            return hasPixelLeft ? ComparisonStage.Pixelated : ComparisonStage.Source3D;
        }

        Texture2D[] BakeWithCache(bool useSmear, bool includePixelOutput, out float timeMs, out PipelineContext ctx)
        {
            var instance = Instantiate(_characterPrefab);
            instance.name = MakeWorkingInstanceName();
            var config = BuildConfig(useSmear, includePixelOutput);

            var pipeline = new SmearPipeline(config, instance, _clip);
            if (useSmear)
            {
                foreach (var st in CloneCurrentStages()) pipeline.AddStage(st);
            }
            else if (includePixelOutput)
            {
                pipeline.AddStage(new PixelizationPhase());
            }
            else
            {
                pipeline.AddStage(new HighResCaptureStage());
            }

            if (IsCacheUsable())
            {
                pipeline.Context.Set("motion", _cachedMotion);
                pipeline.Context.Set("trajectory", _cachedTrajectory);
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            pipeline.RunAll();
            EnsurePreviewFrames(pipeline.Context);
            sw.Stop();
            timeMs = sw.ElapsedMilliseconds;
            ctx = pipeline.Context;

            var frames = PickPreviewFrames(pipeline.Context);

            if (useSmear)
                CollectSmearStats(pipeline.Context);

            DestroyImmediate(instance);
            DestroyImmediate(config);
            return frames;
        }
        
        // Cache the reusable artifacts from the latest run for standalone Pixel Art or 3D export.
        void UpdateAvailableArtifacts(PipelineContext ctx, string sourceLabel)
        {
            _state.AvailableHighRes = ctx.Has("frames_highres")
                ? ctx.Get<RawFrameData>("frames_highres")
                : null;
            _state.HighResSourceLabel = _state.AvailableHighRes != null ? sourceLabel : null;
            _state.AvailableSmearMeta = ctx.Has("smear_data")
                ? ctx.Get<SmearFrameData>("smear_data")
                : null;
        }

        // Refresh the result actions so the window always points at the outputs from the latest run.
        void UpdateResultArtifacts(PipelineContext ctx)
        {
            _state.LastPixelResult = ctx.Has("sprite_sheet")
                ? ctx.Get<SpriteSheetResult>("sprite_sheet")
                : null;
            _state.ResultsStale = false;
            if (_state.LastPixelResult != null)
                SyncExternalExportDefaults();
            _lastCaptureFrame = ctx.Has("capture_frame")
                ? ctx.Get<CaptureFrame>("capture_frame")
                : null;
            if (_heatmapTex   != null) { DestroyImmediate(_heatmapTex);    _heatmapTex    = null; }
            _lastHeatmapFrame = -1;
        }

        void CollectSmearStats(PipelineContext ctx)
        {
            _frameCount = ctx.Has("frames_highres")
                ? ctx.Get<RawFrameData>("frames_highres").FrameCount
                : 0;
            _smearFrameCount = 0;
            _maxIntensity = 0;
            _perFrameIntensity = new float[_frameCount];
            if (ctx.Has("smear_data"))
            {
                var smear = ctx.Get<SmearFrameData>("smear_data");
                for (int f = 0; f < smear.FrameCount; f++)
                {
                    _perFrameIntensity[f] = smear.SmearIntensity[f];
                    if (smear.HasSmear[f]) _smearFrameCount++;
                    if (smear.SmearIntensity[f] > _maxIntensity)
                        _maxIntensity = smear.SmearIntensity[f];
                }
            }
        }

        void UpdateFrameCount()
        {
            if (_smearFrames != null)
            {
                _frameCount = _smearFrames.Length;
                return;
            }

            if (_cleanFrames != null)
                _frameCount = _cleanFrames.Length;
        }

        // Check whether either preview pane has frames to show.
        bool HasPreviewFrames()
        {
            return (_smearFrames != null && _smearFrames.Length > 0)
                || (_cleanFrames != null && _cleanFrames.Length > 0);
        }

        // Create a temporary config object for the current run.
        PipelineConfig BuildConfig(bool useSmear, bool includePixelOutput)
        {
            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var so = new SerializedObject(config);

            if (useSmear && _smearConfig != null)
                so.FindProperty("_smearEffects").objectReferenceValue = _smearConfig;
            else if (!useSmear)
            {
                var guids = AssetDatabase.FindAssets("t:SmearEffectsConfig SmearEffects_NoSmear");
                if (guids.Length > 0)
                    so.FindProperty("_smearEffects").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<SmearEffectsConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_smearOutputConfig != null)
                so.FindProperty("_smearOutput").objectReferenceValue = _smearOutputConfig;
            if (includePixelOutput && _outputConfig != null)
                so.FindProperty("_output").objectReferenceValue = _outputConfig;
            if (includePixelOutput && _postProcessConfig != null)
                so.FindProperty("_postProcess").objectReferenceValue = _postProcessConfig;
            so.FindProperty("_reusePaletteAcrossFrames").boolValue = _reusePaletteAcrossFrames;
            if (_velocityConfig != null)
            {
                var vso = new SerializedObject(_velocityConfig);
                vso.FindProperty("_targetFps").intValue = _targetFps;
                vso.ApplyModifiedPropertiesWithoutUndo();
                so.FindProperty("_velocity").objectReferenceValue = _velocityConfig;
            }
            so.FindProperty("_captureCameraEuler").vector3Value = _captureCameraEuler;
            // capture is always unlit -- no light fields written to config

            so.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        #endregion

        #region Export

        void ExportSpriteSheet()
        {
            if (_smearFrames == null || _smearFrames.Length == 0) return;

            int fw = _smearFrames[0].width;
            int fh = _smearFrames[0].height;
            int count = _smearFrames.Length;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);

            var sheet = new Texture2D(cols * fw, rows * fh, TextureFormat.RGBA32, false);
            sheet.filterMode = FilterMode.Point;
            sheet.SetPixels32(new Color32[sheet.width * sheet.height]);

            for (int i = 0; i < count; i++)
            {
                int c = i % cols;
                int r = rows - 1 - (i / cols);
                if (_smearFrames[i] != null)
                    sheet.SetPixels(c * fw, r * fh, fw, fh, _smearFrames[i].GetPixels());
            }
            sheet.Apply();

            string folder = _outputConfig != null ? _outputConfig.OutputDirectory : SmearFrameworkPaths.Output;
            string baseName = BuildActiveBaseName();
            var meta = SpriteSheetMetadataBuilder.Build(
                _characterPrefab != null ? _characterPrefab.name : "export",
                _clip != null ? _clip.name : "clip",
                count,
                fw,
                fh,
                cols,
                rows,
                sheet.width,
                sheet.height,
                _velocityConfig != null ? _velocityConfig.TargetFps : 12,
                _outputConfig != null ? _outputConfig.CaptureResolution : fw,
                BuildPreviewSmearMetadata(count),
                _smearFrameCount > 0,
                _outputConfig == null || _outputConfig.LoopPlayback,
                _outputConfig != null ? _outputConfig.PixelsPerUnit : 32,
                _outputConfig != null ? _outputConfig.PivotNormalized.x : 0.5f,
                _outputConfig != null ? _outputConfig.PivotNormalized.y : 0f,
                "pixel",
                _outputConfig != null ? _outputConfig.Prefix : "",
                _outputConfig != null ? _outputConfig.Suffix : "");
            var (pngPath, jsonPath) = SpriteSheetDiskWriter.Save(folder, baseName, sheet, meta);
            var package = PixelAnimationPackageExporter.Export(
                folder,
                _characterPrefab != null ? _characterPrefab.name : "export",
                _clip != null ? _clip.name : "clip",
                sheet,
                meta);
            DestroyImmediate(sheet);
            EditorUtility.RevealInFinder(pngPath);
        }

        #endregion

        #region Helpers

        void DrawTransportRow()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_playing ? "Ⅱ" : "▶", EditorStyles.miniButton, GUILayout.Width(32f)))
            {
                _playing = !_playing;
                if (_playing) _playStart = EditorApplication.timeSinceStartup;
            }

            var rewindContent = new GUIContent(EditorGUIUtility.IconContent("Animation.FirstKey"));
            rewindContent.tooltip = "Jump to first frame";
            if (rewindContent.image == null)
                rewindContent.text = "⏮";
            if (GUILayout.Button(rewindContent, EditorStyles.miniButton, GUILayout.Width(28f)))
                _frame = 0;

            Rect sliderRect = GUILayoutUtility.GetRect(80f, 18f, GUILayout.ExpandWidth(true));
            float normalized = _frameCount > 1 ? (float)_frame / (_frameCount - 1) : 0f;
            Rect trackRect = new Rect(sliderRect.x, sliderRect.center.y - 2f, sliderRect.width, 4f);
            EditorGUI.DrawRect(trackRect, new Color(0.16f, 0.16f, 0.16f, 1f));
            var fillColor = SmearTint;
            fillColor.a = 0.75f;
            EditorGUI.DrawRect(new Rect(trackRect.x, trackRect.y, trackRect.width * normalized, trackRect.height), fillColor);
            _frame = Mathf.Clamp(
                Mathf.RoundToInt(GUI.HorizontalSlider(sliderRect, _frame, 0, Mathf.Max(0, _frameCount - 1))),
                0,
                Mathf.Max(0, _frameCount - 1));

            EditorGUILayout.LabelField($"{_frame + 1} / {_frameCount}", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(70f));

            float currentIntensity = _perFrameIntensity != null && _frame < _perFrameIntensity.Length
                ? _perFrameIntensity[_frame]
                : 0f;
            string smearLabel = currentIntensity <= 0.01f ? "smear none" : $"smear {currentIntensity:F3}";
            var smearStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13
            };
            EditorGUILayout.LabelField(smearLabel, smearStyle, GUILayout.Width(112f));
            EditorGUILayout.EndHorizontal();
        }

        // Shows per-frame smear activity directly over the pixel result pane.
        void DrawPixelSmearIndicator(Rect pixelPane)
        {
            if (!HasPixelizationStage() || _perFrameIntensity == null || _frame >= _perFrameIntensity.Length)
                return;

            float intensity = Mathf.Clamp01(_perFrameIntensity[_frame]);
            string label = intensity > 0.01f
                ? $"Smear active  {intensity:F3}"
                : "No smear on this frame";
            var rect = new Rect(pixelPane.x + 8f, pixelPane.y + 8f, Mathf.Min(180f, pixelPane.width - 16f), 18f);
            EditorGUI.ProgressBar(rect, intensity, label);
        }


        // Keep the pane label honest when pixelization is comparing against the raw input.
        string ResolveLeftPaneLabel(bool allowLeftSourceToggle, bool has3DLeft, bool hasPixelLeft, bool smearMode, bool pixelMode)
        {
            if (allowLeftSourceToggle)
                return null;
            if (pixelMode && !smearMode)
                return "Input";
            return has3DLeft ? "3D" : hasPixelLeft ? "Pixel" : "Input";
        }


        Rect GetViewportOverlayRect(Rect viewport)
        {
            float presetWidth = Mathf.Ceil(EditorStyles.miniButton.CalcSize(new GUIContent("Top-down RPG ▾")).x);
            float desiredWidth = Mathf.Max(presetWidth + 62f, 222f);
            float width = Mathf.Min(desiredWidth, Mathf.Max(180f, viewport.width - 16f));
            float extraHeight = _cameraAngleDirty && CanRunCurrentMode() ? 34f : 0f;
            return new Rect(viewport.x + 8f, viewport.yMax - 98f - extraHeight, width, 90f + extraHeight);
        }

        void DrawViewportOverlay(Rect viewport, ref PreviewViewState view)
        {
            Rect boxRect = GetViewportOverlayRect(viewport);
            GUI.Box(boxRect, GUIContent.none);

            const float padding = 6f;
            const float rowGap = 6f;
            const float resetButtonWidth = 28f;
            const float rollButtonWidth = 42f;

            float yOff = 0f;
            if (_cameraAngleDirty && CanRunCurrentMode())
            {
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.85f, 0.55f, 0.15f);
                Rect rebakeRect = new Rect(boxRect.x + padding, boxRect.y + padding, boxRect.width - padding * 2f, 28f);
                var bakeIcon = EditorGUIUtility.IconContent("Lightmapping").image;
                var bakeContent = new GUIContent(IsCacheUsable() ? "  Re-bake" : "  Bake", bakeIcon, "Camera angle changed -- click to bake");
                if (GUI.Button(rebakeRect, bakeContent, EditorStyles.miniButton))
                {
                    _cameraAngleDirty = false;
                    if (IsCacheUsable()) DoQuickRebake(); else DoBake();
                }
                GUI.backgroundColor = oldBg;
                yOff = 34f;
            }

            Rect headingRect = new Rect(boxRect.x + padding, boxRect.y + padding + yOff, boxRect.width - padding * 2f, 16f);
            GUI.Label(headingRect, "Camera Perspective", EditorStyles.boldLabel);

            float presetWidth = boxRect.width - padding * 2f - rowGap - resetButtonWidth;
            Rect presetRect = new Rect(boxRect.x + padding, boxRect.y + 24f + yOff, presetWidth, 20f);
            if (EditorGUI.DropdownButton(presetRect, new GUIContent($"{CurrentCapturePresetName()} ▾", "Choose the camera angle used by the next bake."), FocusType.Passive, EditorStyles.miniButton))
                DrawCapturePresetMenu(presetRect);

            var resetContent = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Refresh"));
            resetContent.tooltip = "Reset view and capture angle";
            if (resetContent.image == null)
                resetContent.text = "Reset";
            Rect resetRect = new Rect(presetRect.xMax + rowGap, boxRect.y + 24f + yOff, resetButtonWidth, 20f);
            if (GUI.Button(resetRect, resetContent, EditorStyles.miniButton))
            {
                view.Reset();
                ResetCaptureEuler();
            }

            float angleRowY = boxRect.y + 48f + yOff;
            Rect angleFieldRect = new Rect(boxRect.x + padding, angleRowY, boxRect.width - padding * 2f, 18f);
            EditorGUI.BeginChangeCheck();
            Vector3 nextEuler = EditorGUI.Vector3Field(angleFieldRect, GUIContent.none, _captureCameraEuler);
            if (EditorGUI.EndChangeCheck())
                SetCaptureEuler(nextEuler);

            float labelWidth = boxRect.width - padding * 2f - rowGap * 2f - rollButtonWidth * 2f;
            Rect labelRect = new Rect(boxRect.x + padding, boxRect.y + 70f + yOff, labelWidth, 16f);
            GUI.Label(labelRect, $"zoom {view.Zoom:F2} · roll {view.RollDeg:F0}°", EditorStyles.centeredGreyMiniLabel);

            Rect rollMinusRect = new Rect(labelRect.xMax + rowGap, boxRect.y + 68f + yOff, rollButtonWidth, 18f);
            if (GUI.Button(rollMinusRect, "Roll -", EditorStyles.miniButton))
            { view.RollDeg -= 15f; _cameraAngleDirty = true; }

            Rect rollPlusRect = new Rect(rollMinusRect.xMax + rowGap, boxRect.y + 68f + yOff, rollButtonWidth, 18f);
            if (GUI.Button(rollPlusRect, "Roll +", EditorStyles.miniButton))
            { view.RollDeg += 15f; _cameraAngleDirty = true; }
        }

        // Draws a hard screen-space ground guide after the overlay so it cannot hide behind the preview UI.

        string CurrentCapturePresetName()
        {
            return CapturePresetUtility.GetLabel(CapturePresetUtility.GetPreset(_captureCameraEuler));
        }

        void ApplyCapturePreset(CapturePreset preset)
        {
            SetCaptureEuler(CapturePresetUtility.GetEuler(preset));
        }

        // Adjust the real capture camera; the next bake applies it to 3D capture.
        void AdjustCaptureEuler(Vector3 delta)
        {
            _captureCameraEuler += delta;
            _cameraAngleDirty = true;
            PreviewCaptureCameraChange();
        }

        void SetCaptureEuler(Vector3 absolute)
        {
            _captureCameraEuler = absolute;
            _cameraAngleDirty = true;
            MarkResultsStale();
            PreviewCaptureCameraChange();
        }

        // Reset the real capture camera rotation for the next bake.
        void ResetCaptureEuler()
        {
            SetCaptureEuler(Vector3.zero);
        }
        // Re-capture at the new camera angle but keep existing smear bake results.
        // Debounced so dragging the angle fields does not spin a full preview recapture every tick.
        void PreviewCaptureCameraChange()
        {
            if (_characterPrefab == null || _clip == null) { Repaint(); return; }

            _pendingCapturePreview = true;
            _pendingCapturePreviewAt = EditorApplication.timeSinceStartup + PREVIEW_CAPTURE_DEBOUNCE_SECONDS;
            EditorApplication.update -= FlushPendingCapturePreview;
            EditorApplication.update += FlushPendingCapturePreview;
        }

        void FlushPendingCapturePreview()
        {
            if (!_pendingCapturePreview)
            {
                EditorApplication.update -= FlushPendingCapturePreview;
                return;
            }
            if (EditorApplication.timeSinceStartup < _pendingCapturePreviewAt)
                return;

            _pendingCapturePreview = false;
            EditorApplication.update -= FlushPendingCapturePreview;
            DoPreviewCameraAngle();
        }

        // Re-runs only the capture pass at the current angle.
        // Stashes and restores the smear results so the baked animation is not wiped.
        void DoPreviewCameraAngle()
        {
            var savedSmear      = _smearFrames;
            var savedIntensity  = _perFrameIntensity;
            int savedSmearCount = _smearFrameCount;
            float savedMaxInt   = _maxIntensity;
            int savedFrame      = _frame;
            bool savedPlaying   = _playing;

            DoPreviewAnimation();

            _smearFrames       = savedSmear;
            _perFrameIntensity = savedIntensity;
            _smearFrameCount   = savedSmearCount;
            _maxIntensity      = savedMaxInt;
            _frame             = Mathf.Clamp(savedFrame, 0, _frameCount - 1);
            _playing           = savedPlaying;
            UpdateFrameCount();
        }

        // Restore the default capture settings.
        // removed: ResetCaptureLight, PreviewCaptureLightChange -- capture is always unlit

        // Source for frames_highres -- comes from a previous in-session bake, or load from disk
        void DrawPreloadPanel()
        {
            _preloadSection.Draw(_state, HasValidLiveInput(),
                PickAndLoadHighRes, ClearPreload, DoBake, DoPixelArtFromPreload);
        }

        // open a file picker, slice the chosen sheet, refresh validation
        void PickAndLoadHighRes()
        {
            string startDir = _outputConfig != null ? _outputConfig.OutputDirectory : SmearFrameworkPaths.Output;
            if (!System.IO.Directory.Exists(startDir)) startDir = "Assets";
            string path = EditorUtility.OpenFilePanel("Load high-res sheet", startDir, "png");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var (frames, meta) = HighResDiskLoader.Load(path);
                _state.AvailableHighRes = frames;
                _state.HighResSourceLabel = System.IO.Path.GetFileName(path);
                _state.AvailableSmearMeta = TryRebuildSmearMetadata(meta, frames.FrameCount);
                _status = $"Loaded {frames.FrameCount} frames from {System.IO.Path.GetFileName(path)}";
                RefreshValidation();
            }
            catch (System.Exception ex)
            {
                _status = "load failed: " + ex.Message;
                _state.AvailableHighRes = null;
                _state.AvailableSmearMeta = null;
                _state.HighResSourceLabel = null;
                RefreshValidation();
            }
        }

        // Quick preset actions for the three user-facing workflows.
        void DrawWorkflowPresets()
        {
            EditorGUILayout.BeginHorizontal();
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = PipelineTint;
            if (GUILayout.Button(new GUIContent("Full Workflow", "Runs smear bake followed by pixel-art conversion."), EditorStyles.miniButton, GUILayout.ExpandWidth(true), GUILayout.Height(SecondaryButtonHeight)))
                ApplyPreset(PipelineMode.Full);
            GUI.backgroundColor = SmearTint;
            if (GUILayout.Button(new GUIContent("Smear Only", "Bakes motion-derived smear frames without pixelization."), EditorStyles.miniButton, GUILayout.ExpandWidth(true), GUILayout.Height(SecondaryButtonHeight)))
                ApplyPreset(PipelineMode.SmearBakeOnly);
            GUI.backgroundColor = PixelTint;
            if (GUILayout.Button(new GUIContent("Pixel Art Only", "Pixelizes loaded high-resolution frames without running the smear bake."), EditorStyles.miniButton, GUILayout.ExpandWidth(true), GUILayout.Height(SecondaryButtonHeight)))
                ApplyPreset(PipelineMode.PixelArtOnly);
            GUI.backgroundColor = oldBg;
            EditorGUILayout.EndHorizontal();
        }

        // Replace the stage list with a known workflow preset.
        void ApplyPreset(PipelineMode mode)
        {
            _currentMode = mode;
            _controller.ApplyPreset(mode);
            MarkResultsStale();
            _smearFrames = null;
            _cleanFrames = null;
            _clean3DFrames = null;
            _cleanPixelFrames = null;
            _perFrameIntensity = null;
            _frameCount = 0;
            _smearFrameCount = 0;
            _maxIntensity = 0f;
            _frame = 0;
            _playing = false;
            RefreshValidation();
            _status = "Workflow preset: " + PipelinePresets.DisplayName(mode) + " -- rebake to refresh preview";
            Repaint();
        }

        // Recreate a SmearFrameData stub from the JSON sidecar so the SpriteSheet exporter has metadata
        SmearFrameData TryRebuildSmearMetadata(HighResMetadata meta, int frameCount)
        {
            if (meta == null || meta.smear_intensity == null || meta.smear_intensity.Length == 0)
                return null;
            var smear = new SmearFrameData(frameCount, vertexCount: 0);
            int len = Mathf.Min(meta.smear_intensity.Length, frameCount);
            for (int i = 0; i < len; i++)
            {
                smear.SmearIntensity[i] = meta.smear_intensity[i];
                smear.HasSmear[i] = meta.smear_intensity[i] > 0f;
            }
            return smear;
        }

        // drop the loaded sheet so the run button goes red again
        void ClearPreload()
        {
            _state.AvailableHighRes = null;
            _state.AvailableSmearMeta = null;
            _state.HighResSourceLabel = null;
            _smearFrames = null;
            RefreshValidation();
        }

        // Explain where Pixelization will get frames in the current state.
        string BuildHighResSourceHint(bool hasFrames, bool hasCharClip)
        {
            if (hasFrames)
                return $"Source: {_state.HighResSourceLabel}";
            if (hasCharClip)
                return "Will be generated from the current character + clip at bake time, or load a *_highres.png from disk.";
            return "Load a *_highres.png from disk, or provide a character + clip so the bake can generate it.";
        }

        // Build a clean name for temp instances so output files keep the real character id.
        string MakeWorkingInstanceName()
        {
            string raw = _characterPrefab != null ? _characterPrefab.name : "character";
            return OutputNameUtility.SanitizeSegment(raw, "character");
        }

        // Build the base file name shared by the sprite-sheet and 3D exporters.
        string BuildActiveBaseName()
        {
            string rawCharacter = _characterPrefab != null ? _characterPrefab.name : "smear";
            string rawClip = _clip != null ? _clip.name : "unnamed";
            return OutputNameUtility.BuildBaseName(rawCharacter, rawClip);
        }

        // Rebuild a smear metadata stub from the current preview intensity bar data.
        SmearFrameData BuildPreviewSmearMetadata(int frameCount)
        {
            if (_perFrameIntensity == null || _perFrameIntensity.Length == 0)
                return null;

            var smear = new SmearFrameData(frameCount, vertexCount: 0);
            int len = Mathf.Min(frameCount, _perFrameIntensity.Length);
            for (int i = 0; i < len; i++)
            {
                smear.SmearIntensity[i] = _perFrameIntensity[i];
                smear.HasSmear[i] = _perFrameIntensity[i] > 0f;
            }
            return smear;
        }

        // Keep status short. The Results card owns concrete file actions and paths.
        string BuildBakeStatus(PipelineContext ctx, float timeMs, SmearScene3DExporter.Result scene3DResult)
        {
            string scene3DStatus = BuildSmear3DStatus(scene3DResult, timeMs);
            string spriteStatus = BuildSpriteSheetStatus(ctx, timeMs, null);
            if (!string.IsNullOrEmpty(spriteStatus))
                return !string.IsNullOrEmpty(scene3DStatus) ? spriteStatus + "\n" + scene3DStatus : spriteStatus;

            if (ctx.Has("frames_highres_disk"))
            {
                string pngPath = ctx.Get<string>("frames_highres_disk");
                if (!string.IsNullOrEmpty(pngPath))
                {
                    string highResStatus = $"Smear bake done - {timeMs:F0}ms. High-res frames ready.";
                    return !string.IsNullOrEmpty(scene3DStatus) ? highResStatus + "\n" + scene3DStatus : highResStatus;
                }
            }

            if (!string.IsNullOrEmpty(scene3DStatus))
                return scene3DStatus;

            return $"Done - {timeMs:F0}ms";
        }

        // Build a short status for a 3D smear result, null when there is nothing to report.
        string BuildSmear3DStatus(SmearScene3DExporter.Result result, float timeMs)
        {
            if (result == null || string.IsNullOrEmpty(result.PrefabPath))
                return null;
            return $"3D smear done - {timeMs:F0}ms.";
        }

        // Surface a short summary after Full or Pixel Art runs.
        string BuildSpriteSheetStatus(PipelineContext ctx, float timeMs, string fallback)
        {
            if (ctx.Has("sprite_sheet"))
            {
                var result = ctx.Get<SpriteSheetResult>("sprite_sheet");
                if (result != null && !string.IsNullOrEmpty(result.PngPath))
                {
                    return $"Pixel art done - {timeMs:F0}ms.";
                }
            }
            return fallback;
        }

        // Run the validator against the current stage list, surface the first error.
        // Run the validator against current preloads without treating Pixelization as a manual dependency.
        void RefreshValidation()
        {
            _state.ValidationStatus = null;
            bool hasHighRes = _state.HasHighResSource || HasValidLiveInput();
            var report = _controller.Validate(hasHighRes);
            foreach (var issue in report.Issues)
            {
                if (issue.Severity == ValidationSeverity.Error)
                {
                    _state.ValidationStatus = "Error: " + issue.Message;
                    return;
                }
            }
            string inputProblem = BuildInputProblem();
            if (!string.IsNullOrEmpty(inputProblem))
                _state.ValidationStatus = inputProblem;
        }


        // Smear workflows always require live character and clip input; pixel-only runs may use a loaded sheet.
        bool CanRunCurrentMode()
        {
            if (HasSmearStage())
                return HasValidLiveInput();
            return HasValidLiveInput() || _state.HasHighResSource;
        }

        // Explains the exact missing input for the selected workflow.
        string BuildRunRequirementMessage()
        {
            return HasSmearStage()
                ? "Drop a character + animation clip to run this smear workflow."
                : "Drop a character + animation clip, or load a high-res sheet.";
        }

        // any stage consumes frames_highres before any earlier stage produces it?
        bool NeedsHighResPreload()
        {
            var produced = new HashSet<string>();
            foreach (var stage in _controller.Pipeline.Stages)
            {
                foreach (var input in stage.InputKey)
                {
                    if (input.Key == "frames_highres" && !produced.Contains("frames_highres"))
                        return true;
                }
                foreach (var output in stage.OutputKey)
                    produced.Add(output.Key);
            }
            return false;
        }

        // any phase wants a live character / animation clip.
        bool NeedsCharacterClip()
        {
            if (_controller.Pipeline.Stages.Any(s => s is SmearBakePhase)) return true;
            if (_controller.Pipeline.Stages.Any(s => s is PixelizationPhase) && _state.AvailableHighRes == null) return true;
            return NeedsHighResPreload();
        }

        // Check the live input before running expensive preview or bake work.
        bool HasValidLiveInput()
        {
            return _characterPrefab != null && _clip != null && string.IsNullOrEmpty(BuildInputProblem());
        }

        // Catch common source/target and source/output mixups in the editor UI.
        string BuildInputProblem()
        {
            if (_characterPrefab != null)
            {
                string prefabName = _characterPrefab.name;
                string prefabPath = AssetDatabase.GetAssetPath(_characterPrefab);

                bool generatedReference = prefabPath.Contains("/Generated/Prefabs/") &&
                    (prefabName.Contains("TestTarget") || prefabName.StartsWith("Reference_Target"));
                if (generatedReference)
                    return "Selected character is a reference target prefab. Use MawBigSideHit_TestSource for the imported fighting test.";

                // smear3D / pixel prefabs are outputs, not source characters -- no SkinnedMeshRenderer means black frames
                bool isExportedOutput = prefabName.EndsWith("_smear3D", System.StringComparison.OrdinalIgnoreCase)
                    || prefabName.EndsWith("_2d", System.StringComparison.OrdinalIgnoreCase)
                    || prefabPath.Contains("/Output/");
                if (isExportedOutput)
                    return "This looks like a generated output prefab, not a source character. Drop the original FBX/prefab here. To re-run Pixel Art on an existing bake, use 'Load from disk...' in the High-res Source panel instead.";

                // fallback: catch any prefab with no SkinnedMeshRenderer at all
                var smrs = _characterPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (smrs.Length == 0)
                    return "The selected prefab has no skinned mesh renderers -- the pipeline can't capture it. Use the original rigged character FBX.";
            }

            string retargetProblem = ClipPoseSampler.GetInputProblem(_characterPrefab, _clip);
            if (!string.IsNullOrEmpty(retargetProblem))
                return retargetProblem;

            if (_clip != null && IsGeneratedClip(_clip))
                return "Selected clip is generated output, not a source animation.";

            return null;
        }

        // Show one or two small fix buttons when the input problem is a rig/retarget issue.
        void DrawInputFixButtons()
        {
            var kind = ClipPoseSampler.ClassifyInputProblem(_characterPrefab, _clip);
            if (kind == InputProblemKind.None)
                return;
            EditorGUILayout.BeginHorizontal();
            if (kind == InputProblemKind.HumanoidSetup)
            {
                if (GUILayout.Button("Fix: Set to Humanoid", EditorStyles.miniButton))
                    HumanoidAvatarWindow.OpenWith(_characterPrefab);
            }
            if (GUILayout.Button("Fix: Retarget Pair", EditorStyles.miniButton))
                RetargetCharacterWindow.OpenWith(_characterPrefab);
            EditorGUILayout.EndHorizontal();
        }

        // Returns true when the clip lives in a generated output folder or has a generated name suffix.
        static bool IsGeneratedClip(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            return path.Contains("/Output/") ||
                path.Contains("/Generated/PixelPreview/") ||
                clip.name.Contains("_smear3D") ||
                clip.name.EndsWith("_2d", System.StringComparison.OrdinalIgnoreCase);
        }

        // probe the cached motion to make sure its inner FrameBuffer arrays survived domain reloads
        bool IsCacheUsable()
        {
            if (_cachedMotion == null || _cachedTrajectory == null) return false;
            try
            {
                return _cachedMotion.FrameCount > 0 && _cachedMotion.BoneCount > 0
                    && _cachedMotion.Bones != null && _cachedMotion.Bones[0] != null;
            }
            catch
            {
                return false;
            }
        }

        // runs each pipeline stage individually so we can show a progress bar per step
        void RunPipelineWithProgress(SmearPipeline pipeline, string title)
        {
            try
            {
                int total = pipeline.StageCount;
                for (int i = 0; i < total; i++)
                {
                    EditorUtility.DisplayProgressBar(title, pipeline.GetStageName(i), (float)i / total);
                    pipeline.RunStage(i);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // opens the result popup after a successful bake
        void ShowResultPopup(SpriteSheetResult result, SmearScene3DExporter.Result smear3DResult, float bakeTimeMs)
        {
            SyncExportFolderFromSection();
            EnsureSmear3DExportDefaults();
            SmearResultPopup.Show(new SmearResultPopupModel
            {
                PixelResult              = result,
                PixelExportFolder        = _externalExportFolder,
                PixelExportFolderName    = _externalExportFolderName,
                Smear3DResult            = smear3DResult,
                BakeTimeMs               = bakeTimeMs,
                Smear3DResultIsTemporary = _state.LastSmear3DResultIsTemporary,
                Smear3DExportFolder      = _smear3DExportFolder,
                Smear3DExportBaseName    = _smear3DExportBaseName,
                ExportSmear3D            = ExportLatestSmear3DResult,
            });
        }

        void LoadPreset(string name)
        {
            var guids = AssetDatabase.FindAssets($"t:SmearEffectsConfig {name}");
            if (guids.Length > 0)
            {
                _smearConfig = AssetDatabase.LoadAssetAtPath<SmearEffectsConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
                _smearEditor = null;

                var hasCache = IsCacheUsable() && _cachedPrefab == _characterPrefab && _cachedClip == _clip;
                if (_autoRebake && hasCache)
                {
                    _lastChangeTime = EditorApplication.timeSinceStartup;
                    _pendingRebake = true;
                }
            }
        }

        void AutoDetectClip()
        {
            if (_characterPrefab != null && _clip == null)
            {
                string path = AssetDatabase.GetAssetPath(_characterPrefab);
                if (!string.IsNullOrEmpty(path))
                {
                    foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                        { _clip = c; break; }
                    }
                }
            }
        }

        #endregion
    }
}
