using Appalachia.Core.Attributes;
using Appalachia.Simulation.Trees.Build.RequestManagers;
using Appalachia.Simulation.Trees.Core;
using Appalachia.Simulation.Trees.Definition.Interfaces;
using Appalachia.Simulation.Trees.Hierarchy;
using Appalachia.Simulation.Trees.ResponsiveUI;
using Appalachia.Simulation.Trees.UI.Base;
using Appalachia.Simulation.Trees.UI.Selections.State;

namespace Appalachia.Simulation.Trees.UI.Log
{
    [CallStaticConstructorInEditor]
    public class LogHierarchyEditor : HierarchyBaseEditor<LogDataContainer>
    {
        static LogHierarchyEditor()
        {
            TreeSpeciesEditorSelection.InstanceAvailable += i => _treeSpeciesEditorSelection = i;
        }

        #region Static Fields and Autoproperties

        private static TreeSpeciesEditorSelection _treeSpeciesEditorSelection;

        #endregion

        protected override bool AreHierarchyButtonsEnabled()
        {
            var s = _treeSpeciesEditorSelection.log;

            if ((s.selection == null) || (s.selection.selected == null))
            {
                return false;
            }

            if (s.selection.selected.initialized)
            {
                return true;
            }

            return false;
        }

        protected override HierarchyData CreateHierarchy(
            IHierarchyWrite hierarchies,
            TreeComponentType type,
            HierarchyData parent)
        {
            var h = hierarchies.CreateHierarchy(type, parent, null);

            return h;
        }

        protected override HierarchyData CreateTrunkHierarchy(IHierarchyWrite hierarchies)
        {
            var h = hierarchies.CreateTrunkHierarchy(null);

            return h;
        }

        protected override IBasicSelection GetSelection()
        {
            return _treeSpeciesEditorSelection.log;
        }

        protected override void SettingsChanged()
        {
            LogBuildRequestManager.SettingsChanged(SettingsUpdateTarget.Distribution);
        }
    }
}
