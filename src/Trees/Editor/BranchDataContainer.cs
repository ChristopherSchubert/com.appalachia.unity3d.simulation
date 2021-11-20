using System;
using System.Collections.Generic;
using Appalachia.CI.Integration.Assets;
using Appalachia.Simulation.Trees.Build.RequestManagers;
using Appalachia.Simulation.Trees.Build.Requests;
using Appalachia.Simulation.Trees.Core.Seeds;
using Appalachia.Simulation.Trees.Core.Settings;
using Appalachia.Simulation.Trees.Data;
using Appalachia.Simulation.Trees.Definition;
using Appalachia.Simulation.Trees.Extensions;
using Appalachia.Simulation.Trees.Generation.AmbientOcclusion;
using Appalachia.Simulation.Trees.Generation.Assets;
using Appalachia.Simulation.Trees.Generation.Texturing.Materials.Management;
using Appalachia.Simulation.Trees.Prefabs;
using Appalachia.Simulation.Trees.ResponsiveUI;
using Appalachia.Simulation.Trees.Seeds;
using Appalachia.Simulation.Trees.Settings;
using Appalachia.Simulation.Trees.Snapshot;
using Appalachia.Spatial.Octree;
using Appalachia.Utility.Logging;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Appalachia.Simulation.Trees
{
    [Serializable]
    public class BranchDataContainer : TSEDataContainer
    {
        #region Fields and Autoproperties

        [HideInInspector] public BranchAsset branchAsset;

        [HideInInspector] public BranchBuildRequests buildRequest;

        [HideInInspector] public BranchMaterialCollection materials;
        [HideInInspector] public BranchSettings settings;

        /*[HideInInspector] public BranchSnapshotParameters snapshotParameters;*/

        [HideInInspector] public List<BranchSnapshotParameters> snapshots;

        [HideInInspector] public TreeBranch branch;

        private BoundsOctree<AmbientOcclusionSamplePoint> _samplePoints;

        private PreviewRenderUtility _renderUtility;

        #endregion

        public override ResponsiveSettingsType settingsType => ResponsiveSettingsType.Branch;

        public BuildRequestLevel requestLevel => buildRequest.requestLevel;

        public PreviewRenderUtility renderUtility
        {
            get
            {
                if (_renderUtility == null)
                {
                    _renderUtility = new PreviewRenderUtility(true, true);
                }

                return _renderUtility;
            }
        }

        public BoundsOctree<AmbientOcclusionSamplePoint> samplePoints
        {
            get => _samplePoints;
            set => _samplePoints = value;
        }

        #region Event Functions

        protected void OnDisable()
        {
            if (_renderUtility != null)
            {
                _renderUtility.Cleanup();
                _renderUtility = null;
            }
        }

        #endregion

        public override void BuildDefault()
        {
            BranchBuildRequestManager.Default();
        }

        public override void BuildForceFull()
        {
            BranchBuildRequestManager.ForceFull();
        }

        public override void BuildFull()
        {
            BranchBuildRequestManager.Full();
        }

        public override void CopyHierarchiesFrom(TSEDataContainer tsd)
        {
            if (tsd is BranchDataContainer dc)
            {
                dc.branch.hierarchies.CopyHierarchiesTo(branch.hierarchies);
            }
        }

        public override void CopySettingsFrom(TSEDataContainer tsd)
        {
            if (tsd is BranchDataContainer dc)
            {
                dc.settings.CopySettingsTo(settings);
            }
        }

        public override NameBasis GetNameBasis()
        {
            if (branch == null)
            {
                return null;
            }

            return branch.nameBasis;
        }

        public override void RebuildStructures()
        {
            branch.hierarchies.Rebuild();
            branch.shapes.Rebuild();
        }

        public override void SetDirtyStates()
        {
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(subfolders);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(materials);

            foreach (var outputMaterial in materials.outputMaterialCache.tiledOutputMaterials)
            {
                foreach (var material in outputMaterial)
                {
                    if (material.asset != null)
                    {
                        EditorUtility.SetDirty(material.asset);
                    }
                }
            }

            foreach (var material in materials.outputMaterialCache?.atlasOutputMaterial)
            {
                if (material.asset != null)
                {
                    EditorUtility.SetDirty(material.asset);
                }
            }

            foreach (var snapshot in snapshots)
            {
                if (snapshot != null)
                {
                    EditorUtility.SetDirty(snapshot);
                }

                foreach (var material in snapshot.branchOutputMaterial)
                {
                    if (material.asset != null)
                    {
                        EditorUtility.SetDirty(material.asset);
                    }
                }
            }

            EditorUtility.SetDirty(hierarchyPrefabs);
            EditorUtility.SetDirty(branch);
            EditorUtility.SetDirty(branchAsset);
            if (branchAsset.mesh != null)
            {
                EditorUtility.SetDirty(branchAsset.mesh);
            }
        }

        public override void SettingsChanged(SettingsUpdateTarget target)
        {
            BranchBuildRequestManager.SettingsChanged(target);
        }

        [Button, HideIf(nameof(initialized))]
        public override void Initialize()
        {
            try
            {
                if (initialized)
                {
                    return;
                }
                
                base.Initialize();

                ResetInitialization();

                var basis = NameBasis.CreateNested<NameBasis>(this);

                var n = initializationSettings.name.ToSafe().TrimEnd(',', '.', '_', '-');

                if (string.IsNullOrWhiteSpace(n))
                {
                    n = $"branch_{DateTime.Now:yyyyMMdd-HHmmss}";
                }

                basis.nameBasis = n;

                UpdateNameAndMove(basis.nameBasis);

                subfolders = TreeAssetSubfolders.CreateNested<TreeAssetSubfolders>(this, false);

                subfolders.nameBasis = basis;

                subfolders.Initialize(this);
                subfolders.CreateFolders();

                branch = TreeBranch.Create(subfolders.data, basis);
                branch.seed.SetInternalSeed(Random.Range(1, BaseSeed.HIGH_ELEMENT));

                if (settings == null)
                {
                    settings = BranchSettings.Create(subfolders.data, basis);
                }

                subfolders.CreateSnapshotFolder();

                if (snapshots == null)
                {
                    snapshots = new List<BranchSnapshotParameters>();

                    var firstSnapshot = BranchSnapshotParameters.Create(subfolders.data, basis, 0);

                    snapshots.Add(firstSnapshot);
                }

                hierarchyPrefabs = TreePrefabCollection.Create(subfolders.data, basis);
                hierarchyPrefabs.ResetPrefabs();
                hierarchyPrefabs.UpdatePrefabs(branch);

                materials = BranchMaterialCollection.Create(subfolders.data, basis);
                materials.UpdateMaterials(branch, hierarchyPrefabs);
                materials.inputMaterialCache.SetDefaultMaterials(settings.material.defaultMaterials);
                materials.inputMaterialCache.UpdateDefaultMaterials();

                branchAsset = BranchAsset.Create(subfolders.data, basis);

                buildRequest = new BranchBuildRequests();

                if (!initializationSettings.convertTreeData)
                {
                    branch.hierarchies.CreateTrunkHierarchy(materials.inputMaterialCache);
                }

                SeedManager.UpdateSeeds(branch);

                initialized = true;
                basis.Lock();
                dataState = DataState.Dirty;

                SetDirtyStates();

                AssetDatabaseManager.SaveAssets();
            }
            catch (Exception ex)
            {
                AppaLog.Error(ex.Message, this);
                initialized = false;
                throw;
            }
        }

        public void PushBuildRequestLevel(
            bool distribution = false,
            bool materialProperties = false,
            bool materialGeneration = false,
            bool uv = false,
            bool highQualityGeometry = false,
            bool ambientOcclusion = false)
        {
            buildRequest.materialGeneration =
                materialGeneration ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.materialProperties =
                materialProperties ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.distribution = distribution ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.uv = uv ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.geometry =
                highQualityGeometry ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.ambientOcclusion =
                ambientOcclusion ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
        }

        public void PushBuildRequestLevelAll(bool active)
        {
            buildRequest.materialGeneration = active ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.materialProperties = active ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.distribution = active ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.uv = active ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.geometry = active ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
            buildRequest.ambientOcclusion = active ? BuildRequestLevel.FinalPass : BuildRequestLevel.None;
        }

        protected override void SaveAllAssets(bool saveImpostors)
        {
            AssetManager.SaveAllAssets(this);
        }

        private void ResetInitialization()
        {
            //initializationSettings = new InitializationSettings(ResponsiveSettingsType.Branch) {name = name};
            branch = null;
            dataState = DataState.Normal;
            initialized = false;
            primaryIDs.SetNextID(0);
            hierarchyPrefabs = null;
            subfolders = null;
            settings = null;
            materials = null;
            snapshots = new List<BranchSnapshotParameters>();
        }

        #region Menu Items

        [MenuItem(PKG.Menu.Assets.Base + nameof(BranchDataContainer), priority = PKG.Menu.Assets.Priority)]
        public static void CreateAsset()
        {
            CreateNew<BranchDataContainer>();
        }

        #endregion
    }
}
