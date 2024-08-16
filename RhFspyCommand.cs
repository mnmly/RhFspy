using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System.Linq;
using Rhino.Display;

namespace MNML
{
    [Rhino.Commands.CommandStyle(Rhino.Commands.Style.ScriptRunner)]
    public class RhFspyCommand : Command
    {
        public RhFspyCommand()
        {
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static RhFspyCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "FspyViewport";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {

            var fspyProjectList = doc.GetList<FspyProject>("fspy");
            if (fspyProjectList == null || fspyProjectList.Count == 0)
            {
                RhinoApp.WriteLine("No fSpy projects available.");
                return Result.Failure;
            }

            string[] projectNames = fspyProjectList.Select(p => p.FileName).ToArray();

            const string projectKey = "FspyProject";
            var projectIndex = 0; // Default to first project

            var getOption = new GetOption();
            getOption.AcceptNothing(true);
            getOption.SetCommandPrompt("Select fSpy project");

            while (true)
            {
                getOption.ClearCommandOptions();
                var projectOption = getOption.AddOptionList(projectKey, projectNames, projectIndex);
                var res = getOption.Get();

                if (res == GetResult.Option)
                {
                    RhinoApp.WriteLine("Option selected. Continuing.");
                    var option = getOption.Option();
                    if (option.Index == projectOption)
                    {
                        projectIndex = option.CurrentListOptionIndex;
                    }
                    continue;
                }
                else if (res == GetResult.Nothing)
                {
                    RhinoApp.WriteLine("Selection completed.");
                    break;
                }
                else if (res == GetResult.Cancel)
                {
                    RhinoApp.WriteLine("Selection cancelled.");
                    return Result.Cancel;
                }
                else
                {
                    RhinoApp.WriteLine($"Unexpected result: {res}");
                    return Result.Failure;
                }
            }

            FspyProject selectedProject = fspyProjectList[projectIndex];
            var imageSize = new Size(selectedProject.CameraParameters.ImageWidth, selectedProject.CameraParameters.ImageHeight);
            var scaledSize = Utils.ScaleDimensions(imageSize, 1280);
            string viewName = selectedProject.FileName;

            // Get the fspy view
            foreach(var v in doc.NamedViews) {
                if (v.Name == viewName) {
                    doc.Views.ActiveView.ActiveViewport.PushViewInfo(v, false);
                }
            }

            // Get the viewport that has the wallpaper
            foreach(var v in doc.Views) {
                if (v.ActiveViewport.WallpaperFilename == null) continue;
                var index = v.ActiveViewport.WallpaperFilename.IndexOf(viewName.Replace(".fspy", ""));
                if (index > -1) {
                    doc.Views.ActiveView.ActiveViewport.SetWallpaper(v.ActiveViewport.WallpaperFilename, false);
                    doc.Views.ActiveView.ActiveViewport.LockedProjection = true;
                    doc.Views.ActiveView.Redraw();
                    break;
                }
            }

            var sizeString = scaledSize.Width.ToString() + " " + scaledSize.Height.ToString();
            var scriptString = "_NewFloatingViewport _P \"CopyActive\" _Enter ";
            scriptString    += "-ViewportProperties _S " + sizeString + " _Enter";
            var result = RhinoApp.RunScript(scriptString, false);

            return Result.Success;
        }
    }
}
