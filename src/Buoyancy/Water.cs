#region

using System;
using Appalachia.Core.Attributes;
using Appalachia.Core.Behaviours;
using Appalachia.Core.Collections.Implementations.Lookups;
using Appalachia.Core.Editing.Attributes;
using Appalachia.Core.Extensions;
using Appalachia.Core.Math;
using Appalachia.Filtering;
using Appalachia.Simulation.Buoyancy.Jobs;
using Appalachia.Simulation.Core;
using Appalachia.Simulation.Physical.Relays;
using Appalachia.Simulation.Physical.Sampling;
using Appalachia.Voxels;
using Appalachia.Voxels.Gizmos;
using Appalachia.Voxels.VoxelTypes;
using Sirenix.OdinInspector;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

#endregion

namespace Appalachia.Simulation.Buoyancy
{
    [ExecuteAlways]
    [ExecutionOrder(-100)]
    public class Water : InternalMonoBehaviour
    {
        private const string _PRF_PFX = nameof(Water) + ".";

        public bool useNewForceApplication;

        [PropertySpace]
        [SmartLabel]
        public Vector3 current;

        private const float _voxelResolutionMin = .25f;
        private const float _voxelResolutionMax = 5.0f;

        [SmartLabel, PropertyRange(nameof(_voxelResolutionMin), nameof(_voxelResolutionMax))]
        public float voxelResolution;

        [SmartLabel]
        [ShowInInspector]
        public int tracking => _index.Count;

        [NonSerialized] private Collider[] _triggers;
        [NonSerialized] private Voxels<WaterVoxel> _voxels;
        [NonSerialized] private bool _hasCurrentChanged;

        [NonSerialized] private BuoyancyLookup _index;

        private static readonly ProfilerMarker _PRF_waterBounds = new ProfilerMarker(_PRF_PFX + nameof(waterBounds));

        public Bounds waterBounds
        {
            get
            {
                using (_PRF_waterBounds.Auto())
                {
                    CheckTriggersAndVoxels();

                    if (_voxels == null)
                    {
                        return default;
                    }

                    return _voxels.rawWorldBounds;
                }
            }
        }

        private static readonly ProfilerMarker _PRF_GetWorldHeightAt = new ProfilerMarker(_PRF_PFX + nameof(GetWorldHeightAt));

        // ReSharper disable once UnusedParameter.Global
        public float GetWorldHeightAt(Vector3 worldPoint)
        {
            using (_PRF_GetWorldHeightAt.Auto())
            {
                return waterBounds.max.y;
            }
        }

        private static readonly ProfilerMarker _PRF_SetRenderTexture = new ProfilerMarker(_PRF_PFX + nameof(SetRenderTexture));

        public void SetRenderTexture(RenderTexture texture)
        {
            using (_PRF_SetRenderTexture.Auto())
            {
                //_waterHeightMap = texture.CropTexture();
            }
        }

        private static readonly ProfilerMarker _PRF_Start = new ProfilerMarker(_PRF_PFX + nameof(Start));

        private void Start()
        {
            using (_PRF_Start.Auto())
            {
                Initialize();
            }
        }

        private static readonly ProfilerMarker _PRF_OnEnable = new ProfilerMarker(_PRF_PFX + nameof(OnEnable));

        private void OnEnable()
        {
            using (_PRF_OnEnable.Auto())
            {

                Initialize();
            }
        }

        private static readonly ProfilerMarker _PRF_OnDestroy = new ProfilerMarker(_PRF_PFX + nameof(OnDestroy));

        private void OnDestroy()
        {
            using (_PRF_OnDestroy.Auto())
            {
                CleanUp();
            }
        }

        private static readonly ProfilerMarker _PRF_Initialize = new ProfilerMarker(_PRF_PFX + nameof(Initialize));

        private void Initialize()
        {
            using (_PRF_Initialize.Auto())
            {
                if (_index == null)
                {
                    _index = new BuoyancyLookup();
                }

                var allColliders = _transform.FilterComponents<Collider>(true).NoTriggers().RunFilter();

                foreach (var c in allColliders)
                {
                    if (c.attachedRigidbody)
                    {
                        Debug.LogError("Water should REALLY not have a rigidbody!");

                        c.attachedRigidbody.detectCollisions = false;
                    }

                    Debug.LogError("Water should not have real colliders!");

                    c.enabled = false;
                }

                CheckTriggersAndVoxels();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    PhysicsSimulator.onSimulationUpdate -= FixedUpdate;
                    PhysicsSimulator.onSimulationUpdate += FixedUpdate;
                }
#endif
            }
        }

#if UNITY_EDITOR
        private static readonly ProfilerMarker _PRF_OnDisable = new ProfilerMarker(_PRF_PFX + nameof(OnDisable));
        private void OnDisable()
        {
            using (_PRF_OnDisable.Auto())
            {
                if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    CleanUp();
                }
            }
        }
#endif


        private static readonly ProfilerMarker _PRF_CleanUp = new ProfilerMarker(_PRF_PFX + nameof(CleanUp));

        private void CleanUp()
        {
            using (_PRF_CleanUp.Auto())
            {
                _voxels.Dispose();

                _triggers = default;
                _voxels = default;
                _hasCurrentChanged = default;
                _index = default;

                if (!Application.isPlaying)
                {
                    PhysicsSimulator.onSimulationUpdate -= FixedUpdate;
                }
            }
        }

        [Button]
        public void ExecuteReset()
        {
            CleanUp();
            OnEnable();
        }

        private static readonly ProfilerMarker _PRF_CheckTriggersAndVoxels = new ProfilerMarker(_PRF_PFX + nameof(CheckTriggersAndVoxels));

        private void CheckTriggersAndVoxels()
        {
            using (_PRF_CheckTriggersAndVoxels.Auto())
            {
                var anyTriggerNull = false;

                if (_triggers != null && _triggers.Length > 0)
                {
                    for (var i = 0; i < _triggers.Length; i++)
                    {
                        if (_triggers[i] == null)
                        {
                            anyTriggerNull = true;
                            break;
                        }
                    }
                }

                if (_triggers == null || _triggers.Length == 0 || anyTriggerNull)
                {
                    _triggers = _transform.FilterComponents<Collider>(true).OnlyTriggers().RunFilter();

                    for (var i = 0; i < _triggers.Length; i++)
                    {
                        var trigger = _triggers[i];

                        var relay = trigger.GetComponent<TriggerRelay_EnterExit>();

                        if (relay == null)
                        {
                            relay = trigger.gameObject.AddComponent<TriggerRelay_EnterExit>();
                        }

                        relay.OnRelayedTriggerEnter -= RelayOnOnRelayedTriggerEnter;
                        relay.OnRelayedTriggerEnter += RelayOnOnRelayedTriggerEnter;
                        relay.OnRelayedTriggerExit -= RelayOnOnRelayedTriggerExit;
                        relay.OnRelayedTriggerExit += RelayOnOnRelayedTriggerExit;
                    }
                }

                if (_triggers.Length == 0)
                {
                    throw new NotSupportedException("Need to add triggers here!");
                }

                if (_voxels == null)
                {
                    voxelResolution = math.clamp(voxelResolution, _voxelResolutionMin, _voxelResolutionMax);
                    _voxels = Voxels<WaterVoxel>.Voxelize(_transform, _triggers, voxelResolution);
                }
            }
        }

        private static readonly ProfilerMarker _PRF_FixedUpdate = new ProfilerMarker(_PRF_PFX + nameof(FixedUpdate));

        private void FixedUpdate()
        {
            using (_PRF_FixedUpdate.Auto())
            {
                FixedUpdate(Time.fixedDeltaTime);
            }
        }

        private static readonly ProfilerMarker _PRF_FixedUpdate_SynchronizeVoxelData =
            new ProfilerMarker(_PRF_PFX + nameof(FixedUpdate) + ".SynchronizeVoxelData");

        private static readonly ProfilerMarker _PRF_FixedUpdate_SyncTransforms =
            new ProfilerMarker(_PRF_PFX + nameof(FixedUpdate) + ".SyncTransforms");

        private static readonly ProfilerMarker _PRF_FixedUpdate_ScheduleBatchedJobs =
            new ProfilerMarker(_PRF_PFX + nameof(FixedUpdate) + ".ScheduleBatchedJobs");

        private static readonly ProfilerMarker _PRF_FixedUpdate_IterateIndices =
            new ProfilerMarker(_PRF_PFX + nameof(FixedUpdate) + ".IterateIndices");

        private static readonly ProfilerMarker _PRF_FixedUpdate_ScheduleBuoyancyJobs =
            new ProfilerMarker(_PRF_PFX + nameof(FixedUpdate) + ".ScheduleBuoyancyJobs");
        
        private static readonly ProfilerMarker _PRF_FixedUpdate_JobHandleComplete =
            new ProfilerMarker(_PRF_PFX + nameof(FixedUpdate) + ".JobHandleComplete");

        private static readonly ProfilerMarker _PRF_FixedUpdate_UpdateDrag = new ProfilerMarker(_PRF_PFX + nameof(FixedUpdate) + ".UpdateDrag");


        private void FixedUpdate(float deltaTime)
        {
            using (_PRF_FixedUpdate.Auto())
            {
                if (_voxels == null)
                {
                    CheckTriggersAndVoxels();
                }

                using (_PRF_FixedUpdate_SynchronizeVoxelData.Auto())
                {
                    _voxels.Synchronize();
                }

                Physics.autoSyncTransforms = false;

                using (_PRF_FixedUpdate_IterateIndices.Auto())
                {
                    for (var floatingIndex = _index.Count - 1; floatingIndex >= 0; floatingIndex--)
                    {
                        var buoyant = _index.at[floatingIndex];

                        if (buoyant == null)
                        {
                            _index.RemoveAt(floatingIndex);
                            continue;
                        }

                        if (!buoyant.enabled)
                        {
                            continue;
                        }

                        if (!buoyant.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        buoyant.Initialize();

                        if (buoyant.buoyancyData.buoyancyType == BuoyancyType.NonPhysical)
                        {
                            continue;
                        }

                        if (buoyant.voxels == null)
                        {
                            using (_PRF_FixedUpdate_ScheduleBuoyancyJobs.Auto())
                            {
                                buoyant.ScheduleBuoyancyJobs(this, deltaTime);
                            }

                            continue;
                        }

                        using (_PRF_FixedUpdate_JobHandleComplete.Auto())
                        {
                            buoyant.jobHandle.Complete();
                        }

                        using (_PRF_FixedUpdate_UpdateDrag.Auto())
                        {
                            buoyant.UpdateDrag(buoyant.voxels.objectData.submersionPercentage.Value);
                        }

                        if (useNewForceApplication)
                        {
                            ApplyBuoyancyForces_New(buoyant);
                        }
                        else
                        {
                            ApplyBuoyancyForces(buoyant);
                        }

                        using (_PRF_FixedUpdate_ScheduleBuoyancyJobs.Auto())
                        {
                            buoyant.ScheduleBuoyancyJobs(this, deltaTime);
                        }
                    }
                }

                using (_PRF_FixedUpdate_SyncTransforms.Auto())
                {
                    Physics.SyncTransforms();
                    Physics.autoSyncTransforms = true;
                }

                using (_PRF_FixedUpdate_ScheduleBatchedJobs.Auto())
                {

                    JobHandle.ScheduleBatchedJobs();
                }
            }
        }

        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_New = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces_New));

        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_New_AddForce =
            new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces_New) + ".AddForce");

        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_New_AddTorque =
            new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces_New) + ".AddTorque");

        private void ApplyBuoyancyForces_New(Buoyant buoyant)
        {
            using (_PRF_ApplyBuoyancyForces_New.Auto())
            {
                var cumulativeForce = buoyant.voxels.objectData.force.Value;
                var cumulativeTorque = buoyant.voxels.objectData.torque.Value;

                if (buoyant.averagedTorque == null)
                {
                    buoyant.averagedTorque = new double3Average() {windowSize = 4};
                }

                buoyant.averagedTorque.ComputeAverage(cumulativeTorque);

                var body = buoyant.body;

                using (_PRF_ApplyBuoyancyForces_New_AddForce.Auto())
                {
                    body.AddForce(cumulativeForce);
                }

                using (_PRF_ApplyBuoyancyForces_New_AddTorque.Auto())
                {
                    body.AddTorque(cumulativeTorque);
                }

                buoyant.cumulativeForce = cumulativeForce;
                buoyant.cumulativeTorque = cumulativeTorque;
            }
        }


        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces));
        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_IterateCalculations = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces) + ".IterateCalculations");
        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_IterateCalculations_Iteration = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces) + ".IterateCalculations.Iteration");
        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_GetCalculationData = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces) + ".GetCalculationData");
        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_GetVoxelWorldPosition = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces) + ".GetVoxelWorldPosition");
        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_GetForce = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces) + ".GetForce");
        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_AddCumulativeForce = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces) + ".AddCumulativeForce");
        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_GetVoxel = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces) + ".GetVoxel");
        private static readonly ProfilerMarker _PRF_ApplyBuoyancyForces_AddForceAtPosition = new ProfilerMarker(_PRF_PFX + nameof(ApplyBuoyancyForces) + ".AddForceAtPosition");

        
        private void ApplyBuoyancyForces(Buoyant buoyant)
        {
            using (_PRF_ApplyBuoyancyForces.Auto())
            {
                var cumulativeForce = Vector3.zero;
                
                var calculationData = buoyant.voxels.elementDatas;
                var voxels = buoyant.voxels.voxels;
                var calculationsLength = calculationData.Length;
                var body = buoyant.body;

                using (_PRF_ApplyBuoyancyForces_IterateCalculations.Auto())
                {
                    for (var forceIndex = 0; forceIndex < calculationsLength; forceIndex++)
                    {
                        using (_PRF_ApplyBuoyancyForces_IterateCalculations_Iteration.Auto())
                        {
                            Voxel voxel;
                            BuoyancyVoxel buoyancyCalculationData;
                            float3 force;
                            float3 forcePosition;

                            using (_PRF_ApplyBuoyancyForces_GetCalculationData.Auto())
                            {
                                buoyancyCalculationData = calculationData[forceIndex];
                            }

                            using (_PRF_ApplyBuoyancyForces_GetForce.Auto())
                            {
                                force = buoyancyCalculationData.force;
                            }

                            using (_PRF_ApplyBuoyancyForces_GetVoxel.Auto())
                            {
                                voxel = voxels[forceIndex];
                            }

                            using (_PRF_ApplyBuoyancyForces_GetVoxelWorldPosition.Auto())
                            {
                                forcePosition = voxel.worldPosition.value;
                            }

                            using (_PRF_ApplyBuoyancyForces_AddCumulativeForce.Auto())
                            {
                                cumulativeForce += (Vector3) force;
                            }

                            using (_PRF_ApplyBuoyancyForces_AddForceAtPosition.Auto())
                            {
                                body.AddForceAtPosition(force, forcePosition, ForceMode.Force);
                            }
                        }
                    }
                }

                buoyant.cumulativeForce = cumulativeForce;

            }
        }

        private void RelayOnOnRelayedTriggerEnter(TriggerRelay relay, Collider[] these, Collider other)
        {
            OnTriggerEnter(other);
        }
        
        private void RelayOnOnRelayedTriggerExit(TriggerRelay relay, Collider[] these, Collider other)
        {
            OnTriggerExit(other);
        }
        
        private static readonly ProfilerMarker _PRF_OnTriggerEnter = new ProfilerMarker(_PRF_PFX + nameof(OnTriggerEnter));
        private void OnTriggerEnter(Collider other)
        {
            using (_PRF_OnTriggerEnter.Auto())
            {
                var rb = other.attachedRigidbody;

                if ((rb == null) || !rb.detectCollisions || rb.isKinematic)
                {
                    return;
                }

                var buoyant = rb.GetComponent<Buoyant>();

                if (buoyant == null)
                {
                    buoyant = rb.gameObject.AddComponent<Buoyant>();
                }

                InitiateBuoyancy(buoyant);
            }
        }

        private static readonly ProfilerMarker _PRF_InitiateBuoyancy = new ProfilerMarker(_PRF_PFX + nameof(InitiateBuoyancy));
        public void InitiateBuoyancy(Buoyant buoyant)
        {
            using (_PRF_InitiateBuoyancy.Auto())
            {
                buoyant.EnterWater(this);

                var go = buoyant.gameObject;

                if (!_index.ContainsKey(go))
                {
                    _index.Add(go, buoyant);
                }
            }
        }

        private static readonly ProfilerMarker _PRF_OnTriggerExit = new ProfilerMarker(_PRF_PFX + nameof(OnTriggerExit));
        private void OnTriggerExit(Collider other)
        {
            using (_PRF_OnTriggerExit.Auto())
            {
                var rb = other.attachedRigidbody;

                if (rb == null)
                {
                    return;
                }

                var buoyant = rb.GetComponent<Buoyant>();

                if (buoyant == null)
                {
                    return;
                }

                buoyant.cumulativeForce = Vector3.zero;
                buoyant.cumulativeTorque = Vector3.zero;
                
                if (!buoyant.submerged)
                {
                    buoyant.enabled = false;                    
                }
            }
        }

        private static readonly ProfilerMarker _PRF_UpdateSamples = new ProfilerMarker(_PRF_PFX + nameof(UpdateSamples));
        public void UpdateSamples(ref TrilinearSamples<WaterVoxel> currentSamples)
        {
            using (_PRF_UpdateSamples.Auto())
            {
                CheckTriggersAndVoxels();
            
                if (_voxels == null) return;
            
                if (!_hasCurrentChanged && currentSamples.isCreated && currentSamples.isPopulated)
                {
                    return;
                }

                var matrix = currentSamples.localToWorld;
                
                for (var i = 0; i < currentSamples.points.Length; i++)
                {
                    var point = currentSamples.points[i];

                    var worldPoint = matrix.MultiplyPoint3x4(point);
                    var waterVoxelSpacePoint = _voxels.worldToLocal.MultiplyPoint3x4(worldPoint);
                    
                    if (_voxels.TryGetSamplePointIndices(waterVoxelSpacePoint, out var samplePointIndex))
                    {
                        var samplePoint = _voxels.samplePoints[samplePointIndex];
                        
                        if (!samplePoint.populated)
                        {
                            currentSamples.values[i] = default;
                            continue;
                        }
                        
                        currentSamples.values[i] = _voxels.elementDatas[samplePoint.index];
                    }
                }

                currentSamples.isPopulated = true;
                _hasCurrentChanged = false;
            }
        }
        
#if UNITY_EDITOR

        [FoldoutGroup("Gizmos"), NonSerialized, ShowInInspector, SmartLabel, InlineEditor()]
        private VoxelDataGizmoSettings _voxelGizmoSettings;

        public void OnDrawGizmosSelected()
        {
            if (!enabled) return;
            
            CheckTriggersAndVoxels();

            if (_voxels == null)
            {
                return;
            }
            
            if (_voxelGizmoSettings == null)
            {
                var lookup = VoxelDataGizmoSettingsLookup.instance;

                _voxelGizmoSettings = lookup.GetOrLoadOrCreateNew(VoxelDataGizmoStyle.Water, nameof(VoxelDataGizmoStyle.Water));
            }

            _voxels.DrawGizmos(_voxelGizmoSettings);
        }
#endif
    }
}
