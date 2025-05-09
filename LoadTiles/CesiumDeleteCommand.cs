using Rhino;
using Rhino.Commands;
namespace LoadTiles
{
    public class CesiumDeleteCommand : Command
    {
        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "SealionDelete";

        /// <summary>
        /// Handles the user running the command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            TemporaryGeometryConduit displayConduit = TemporaryGeometryConduit.Instance;
            displayConduit.Reset();

            AttributionConduit.Instance.removeImage();
            AttributionConduit.Instance.setClickURL("");
            AttributionConduit.Instance.setAttributionText("");

            return Result.Success;
        }
    }
}
