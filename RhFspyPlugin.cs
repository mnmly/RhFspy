using System;
using System.Drawing;
using System.Linq;
using Rhino;
using Rhino.FileIO;
using Rhino.PlugIns;
using Rhino.UI;

namespace MNML
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class RhFspyPlugin : FileImportPlugIn
    {
        public RhFspyPlugin()
        {
            Instance = this;
        }

        ///<summary>Gets the only instance of the RhFspyPlugin plug-in.</summary>
        public static RhFspyPlugin Instance { get; private set; }

        protected override FileTypeList AddFileTypes(FileReadOptions options)
        {
            FileTypeList result = new FileTypeList();
            result.AddFileType("FSpy project file (*.fspy)", "fspy");
            return result;
        }

        private Size ScaleDimensions(int originalWidth, int originalHeight, int maxSize)
        {
            double aspectRatio = (double)originalWidth / originalHeight;

            int newWidth, newHeight;

            if (originalWidth > originalHeight)
            {
                newWidth = maxSize;
                newHeight = (int)(newWidth / aspectRatio);
            }
            else
            {
                newHeight = maxSize;
                newWidth = (int)(newHeight * aspectRatio);
            }


            return new Size(newWidth, newHeight);
        }

        protected override bool ReadFile(string filename, int index, RhinoDoc doc, FileReadOptions options)
        {
            try
            {
                var dialog = new SaveFileDialog();
                dialog.Title = "Save wallpaper image file";
                dialog.FileName = filename.Split("/").Last().Replace(".fspy", ".png");
                if (!dialog.ShowSaveDialog()) {
                    throw new Exception("Please select a location for image.");
                }
                var project = new FspyProject(filename);
                var cameraInfo = project.GetCameraInfo();
                var imageSize = new Size(project.CameraParameters.ImageWidth, project.CameraParameters.ImageHeight);
                var scaledSize = Utils.ScaleDimensions(imageSize, 1280);
                var viewName = "fspy Views";
                var viewRect = new Rectangle(0, 0, scaledSize.Width, scaledSize.Height);
                var newView = doc.Views.Find(viewName, true);

                if (newView == null)
                {
                    newView = doc.Views.Add(viewName, Rhino.Display.DefinedViewportProjection.Perspective, viewRect, true);
                    if (newView == null)
                    {
                        throw new Exception("Failed to create new view");
                    }
                }

                //  Get the viewport of the new view
                var viewport = newView.ActiveViewport;

                var sensorWidth = 36.0;
                var sensorHeight = 24.0;
                var sensorAspectRatio = sensorHeight > 0 ? sensorWidth / sensorHeight : 1.0;
                var absoluteFocalLength = 0.0;
                var relativeFocalLength = project.CameraParameters.RelativeFocalLength;

                if (sensorAspectRatio > 1)
                {
                    // wide sensor
                    absoluteFocalLength = 0.5 * sensorWidth * relativeFocalLength;
                }
                else
                {
                    // tall sensor
                    absoluteFocalLength = 0.5 * sensorHeight * relativeFocalLength;
                }

                // Set up the viewport
                viewport.Camera35mmLensLength = absoluteFocalLength;

                // Set camera location and orientation
                viewport.SetCameraLocation(cameraInfo.Location, false);
                viewport.SetCameraDirection(cameraInfo.Forward, false);
                viewport.CameraUp = cameraInfo.Up;
                viewport.SetWallpaper(project.SaveImageData(dialog.FileName), false);

                // Set the viewport size
                viewport.Size = scaledSize;

                // Set the name for the NamedView
                var name = project.FileName.Split()[0];

                //  Add the NamedView
                int namedViewIndex = doc.NamedViews.FindByName(name);
                if (namedViewIndex > -1) { doc.NamedViews.Delete(namedViewIndex); }
                namedViewIndex = doc.NamedViews.Add(name, newView.ActiveViewportID);
                if (namedViewIndex < 0)
                {
                    throw new Exception("Failed to add NamedView");
                }

                newView.Redraw();

                doc.Views.ActiveView = newView;
                var fspyProjectList = doc.GetList<FspyProject>("fspy");
                var existingFspyProject = fspyProjectList.Find( e => e.FileName == project.FileName);
                if (existingFspyProject != null) {
                    fspyProjectList.Remove(existingFspyProject);
                }
                fspyProjectList.Add(project);
                doc.SetList<FspyProject>("fspy", fspyProjectList);

                // var sizeString = scaledSize.Width.ToString() + " " + scaledSize.Height.ToString();
                // var scriptString = "-NamedView Restore \"" + name + "\" _Enter ";
                // scriptString    += "-NewFloatingViewport _P \"CopyActive\" _Enter ";
                // scriptString    += "-ViewportProperties _S " + sizeString + " _Enter";
                // Rhino.RhinoApp.RunScript(scriptString, true);
                // Rhino.RhinoApp.WriteLine(scriptString);
                return true;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error importing fSpy project: {ex.Message}");
            }
            return false;
        }
    }
}