using Autodesk.Max;
using Test_ExplodeScript;

namespace ExplodeScript
{
#if MAX_2012_DEBUG
    public class basePluginClass : MaxCustomControls.CuiActionCommandAdapter
    {
        public override string ActionText
        {
            get { return InternalActionText; }
        }

        public override string InternalActionText
        {
            get { return "ExplodeScript"; }
        }

        public override string Category
        {
            get { return InternalCategory; }
        }

        public override string InternalCategory
        {
            get { return "VinTech"; }
        }

        public override void Execute(object parameter)
        {
            var global = GlobalInterface.Instance;
            var explode = new ExplodeView(global);
        }
    }
#endif
#if (MAX_2013_DEBUG || MAX_2014_DEBUG ||  MAX_2015_DEBUG)
    public class basePluginClass : UiViewModels.Actions.CuiActionCommandAdapter
    {
        public override string ActionText
        {
            get { return InternalActionText; }
        }

        public override string InternalActionText
        {
            get { return "ExplodeScript"; }
        }

        public override string Category
        {
            get { return InternalCategory; }
        }

        public override string InternalCategory
        {
            get { return "VinTech"; }
        }

        public override void Execute(object parameter)
        {
            var global = GlobalInterface.Instance;
            var explode = new ExplodeView(global);
        }
    }
#endif

}
