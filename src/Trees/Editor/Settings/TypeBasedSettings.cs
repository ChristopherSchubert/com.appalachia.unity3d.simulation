using Appalachia.Simulation.Trees.Core.Serialization;
using Appalachia.Simulation.Trees.Core.Settings;
using Appalachia.Simulation.Trees.Extensions;

namespace Appalachia.Simulation.Trees.Settings
{
    public abstract class TypeBasedSettings<T> : ResponsiveAppalachiaObject
        where T : TypeBasedSettings<T>
    {
        public virtual void CopySettingsTo(T t)
        {
        }

        public override void UpdateSettingsType(ResponsiveSettingsType t)
        {
            this.HandleResponsiveUpdate(t);
        }
    }
}
