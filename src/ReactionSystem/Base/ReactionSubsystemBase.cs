using System;
using Appalachia.Core.Behaviours;
using Appalachia.Core.Types.Enums;
using Appalachia.Utility.Logging;
using Sirenix.OdinInspector;
using Unity.Profiling;
using UnityEngine;

namespace Appalachia.Simulation.ReactionSystem.Base
{
    [ExecuteAlways]
    [Serializable]
    public abstract class ReactionSubsystemBase : AppalachiaBehaviour
    {
        #region Fields and Autoproperties

        public ReactionSystem mainSystem;

        [SerializeField] private int _groupIndex;

        [SerializeField]
        [FoldoutGroup("Texture")]
        [OnValueChanged(nameof(Initialize))]
        public RenderTextureFormat renderTextureFormat;

        [SerializeField]
        [FoldoutGroup("Texture")]
        [OnValueChanged(nameof(Initialize))]
        public RenderQuality renderTextureQuality = RenderQuality.High_1024;

        [SerializeField]
        [FoldoutGroup("Texture")]
        [OnValueChanged(nameof(Initialize))]
        public FilterMode filterMode;

        [SerializeField]
        [FoldoutGroup("Texture")]
        [OnValueChanged(nameof(Initialize))]
        [ValueDropdown(nameof(depths))]
        public int depth;

        private ValueDropdownList<int> depths = new()
        {
            0,
            8,
            16,
            24,
            32
        };

        private bool updateLoopInitialized;

        #endregion

        [InlineProperty]
        [ShowInInspector]
        [PreviewField(ObjectFieldAlignment.Center, Height = 256)]
        [FoldoutGroup("Preview")]
        [ShowIf(nameof(showRenderTexture))]
        public abstract RenderTexture renderTexture { get; }

        protected abstract bool showRenderTexture { get; }

        protected abstract string SubsystemName { get; }

        protected ReactionSubsystemGroup Group => mainSystem ? mainSystem.groups[_groupIndex] : null;

        #region Event Functions

        //public abstract void GetRenderingPosition(out Vector3 minimumPosition, out Vector3 size);

        protected override void Awake()
        {
            using (_PRF_Awake.Auto())
            {
                base.Awake();
                updateLoopInitialized = false;
                Initialize();
            }
        }

        protected void Update()
        {
            using (_PRF_Update.Auto())
            {
                try
                {
                    if (!updateLoopInitialized)
                    {
                        updateLoopInitialized = InitializeUpdateLoop();
                    }

                    if (!updateLoopInitialized)
                    {
                        return;
                    }

                    DoUpdateLoop();
                }
                catch (Exception ex)
                {
                    AppaLog.Error(ex);
                }
            }
        }

        protected override void OnEnable()
        {
            using (_PRF_OnEnable.Auto())
            {
                base.OnEnable();

                updateLoopInitialized = false;
                Initialize();
            }
        }

        protected override void OnDisable()
        {
            using (_PRF_OnDisable.Auto())
            {
                base.OnDisable();

                TeardownSubsystem();
                updateLoopInitialized = false;
            }
        }

        #endregion

        [Button]
        public override void Initialize()
        {
            using (_PRF_Initialize.Auto())
            {
                base.Initialize();

                gameObject.name = SubsystemName;

                OnInitialization();
            }
        }

        public void InitializeSubsystem(ReactionSystem system, int groupIndex)
        {
            using (_PRF_InitializeSubsystem.Auto())
            {
                mainSystem = system;
                _groupIndex = groupIndex;

                Initialize();
            }
        }

        public void UpdateGroupIndex(int i)
        {
            _groupIndex = i;
        }

        protected abstract void DoUpdateLoop();

        protected abstract bool InitializeUpdateLoop();

        protected abstract void OnInitialization();

        protected abstract void TeardownSubsystem();

        #region Profiling

        private const string _PRF_PFX = nameof(ReactionSubsystemBase) + ".";

        private static readonly ProfilerMarker _PRF_InitializeSubsystem =
            new ProfilerMarker(_PRF_PFX + nameof(InitializeSubsystem));

        private static readonly ProfilerMarker _PRF_Initialize =
            new ProfilerMarker(_PRF_PFX + nameof(Initialize));

        private static readonly ProfilerMarker _PRF_Awake = new ProfilerMarker(_PRF_PFX + nameof(Awake));

        private static readonly ProfilerMarker _PRF_Update = new ProfilerMarker(_PRF_PFX + nameof(Update));

        private static readonly ProfilerMarker
            _PRF_OnEnable = new ProfilerMarker(_PRF_PFX + nameof(OnEnable));

        private static readonly ProfilerMarker _PRF_OnDisable =
            new ProfilerMarker(_PRF_PFX + nameof(OnDisable));

        #endregion
    }
}
