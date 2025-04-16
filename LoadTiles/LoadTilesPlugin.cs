using System;
using Rhino;

namespace LoadTiles
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class LoadTilesPlugin : Rhino.PlugIns.PlugIn
    {
        // Version number (used for writing plug-in data to the save file)
        private const int MAJOR = 1;
        private const int MINOR = 0;
        public LoadTilesPlugin()
        {
            Instance = this;
        }

        /// <summary>
        /// Called whenever Rhino is about to save a .3dm file. If you want to save
        /// plug-in document data when a model is saved in a version 5 .3dm file, then
        /// you must override this function to return true and you must override WriteDocument().
        /// </summary>
        protected override bool ShouldCallWriteDocument(Rhino.FileIO.FileWriteOptions options) {
            // TODO: do we always want to return true? I think you're supposed to use the information in `options` to determine whether you should write.
            // https://developer.rhino3d.com/api/rhinocommon/rhino.plugins.plugin/shouldcallwritedocument
            return true;
        }

        /// <summary>
        /// Called when Rhino is saving a .3dm file to allow the plug-in to save document user data.
        /// </summary>
        protected override void WriteDocument(RhinoDoc doc, Rhino.FileIO.BinaryArchiveWriter archive, Rhino.FileIO.FileWriteOptions options) {
            Console.WriteLine("WriteDocument invoked");
            Rhino.Commands.Command[] commands = GetCommands();
            LoadTilesCommand loadTilesCommand = null;
            foreach (Rhino.Commands.Command command in commands) {
                if (command.EnglishName == "Fetch") {
                    loadTilesCommand = (LoadTilesCommand) command;
                }
            }
            if (loadTilesCommand == null) {
                Console.WriteLine("Couldn't find the LoadTilesCommand object. Can't write anything to file.");
                return;
            }

            if (!loadTilesCommand.locationInputted) {
                // The user hasn't actually loaded any terrain data in yet, so there's nothing to save.
                return;
            }

            double[] locationValues = new double[] {loadTilesCommand.latitude, loadTilesCommand.longitude, loadTilesCommand.altitude};
            
            // Write the version of our document data
            archive.Write3dmChunkVersion(MAJOR, MINOR);
            // Write our actual user data
            archive.WriteDoubleArray(locationValues);
        }

        /// <summary>
        /// Called whenever a Rhino document is being loaded and plug-in user data was
        /// encountered, written by a plug-in with this plug-in's GUID.
        /// </summary>
        protected override void ReadDocument(RhinoDoc doc, Rhino.FileIO.BinaryArchiveReader archiveReader, Rhino.FileIO.FileReadOptions options) {
            archiveReader.Read3dmChunkVersion(out int major, out int minor);
            if (MAJOR == major && MINOR == minor) {
                double[] locationValues = archiveReader.ReadDoubleArray();
                double latitude = locationValues[0];
                double longitude = locationValues[1];
                double altitude = locationValues[2];

                Rhino.Commands.Command[] commands = GetCommands();
                LoadTilesCommand loadTilesCommand = null;
                foreach (Rhino.Commands.Command command in commands) {
                    if (command.EnglishName == "Fetch") {
                        loadTilesCommand = (LoadTilesCommand) command;
                    }
                }
                if (loadTilesCommand == null) {
                    Console.WriteLine("Couldn't find the LoadTilesCommand object. Can't read anything from file.");
                    return;
                }

                Console.WriteLine("Saved location data found and loaded.");
                loadTilesCommand.latitude = latitude;
                loadTilesCommand.longitude = longitude;
                loadTilesCommand.altitude = altitude;
                loadTilesCommand.locationInputted = true;
            }
        }
        
        ///<summary>Gets the only instance of the LoadTilesPlugin plug-in.</summary>
        public static LoadTilesPlugin Instance { get; private set; }

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and maintain plug-in wide options in a document.
    }
}