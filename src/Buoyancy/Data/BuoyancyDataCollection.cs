using System;
using Appalachia.Core.Collections.Implementations.Lists;
using Appalachia.Core.Scriptables;
using Appalachia.Simulation.Buoyancy.Collections;

namespace Appalachia.Simulation.Buoyancy.Data
{
    [Serializable]
    public class BuoyancyDataCollection : ScriptableObjectLookupCollection<BuoyancyDataCollection, BuoyancyDataLookup, string, BuoyancyData,
        AppaList_string, AppaList_BuoyancyData>
    {
        protected override string GetUniqueKeyFromValue(BuoyancyData value)
        {
            return value.meshGUID;
        }
    }
}