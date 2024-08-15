using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Numerics;

namespace MNML
{
    public class ParsingError : Exception
    {
        public ParsingError(string message) : base(message) { }
    }

    public class CameraInfo
    {
        public Rhino.Geometry.Vector3d Right { get; set; }
        public Rhino.Geometry.Vector3d Up { get; set; }
        public Rhino.Geometry.Vector3d Forward { get; set; }
        public Rhino.Geometry.Point3d Location { get; set; }
    }

    public class CameraParameters
    {
        public (float x, float y) PrincipalPoint { get; private set; }
        public float FovHoriz { get; private set; }
        public float[][] CameraTransform { get; private set; }
        public int ImageWidth { get; private set; }
        public int ImageHeight { get; private set; }
        public double RelativeFocalLength { get; private set; }

        public CameraParameters(JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Undefined)
            {
                throw new ParsingError("Trying to import an fSpy project with no camera parameters");
            }

            var principalPoint = jsonElement.GetProperty("principalPoint");
            PrincipalPoint = (principalPoint.GetProperty("x").GetSingle(), principalPoint.GetProperty("y").GetSingle());
            FovHoriz = jsonElement.GetProperty("horizontalFieldOfView").GetSingle();
            CameraTransform = jsonElement.GetProperty("cameraTransform").GetProperty("rows").Deserialize<float[][]>();
            ImageWidth = jsonElement.GetProperty("imageWidth").GetInt32();
            ImageHeight = jsonElement.GetProperty("imageHeight").GetInt32();
            RelativeFocalLength = jsonElement.GetProperty("relativeFocalLength").GetDouble();
        }
    }


    public class FspyProject
    {
        public int ProjectVersion { get; private set; }
        public CameraParameters CameraParameters { get; private set; }
        public string ReferenceDistanceUnit { get; private set; }
        public byte[] ImageData { get; private set; }
        public string FileName { get; private set; }

        public FspyProject(string projectPath)
        {
            using (var projectFile = new BinaryReader(File.Open(projectPath, FileMode.Open)))
            {
                uint fileId = projectFile.ReadUInt32();
                if (2037412710 != fileId)
                {
                    throw new ParsingError("Trying to import a file that is not an fSpy project");
                }

                ProjectVersion = projectFile.ReadInt32();
                if (ProjectVersion != 1)
                {
                    throw new ParsingError($"Unsupported fSpy project file version {ProjectVersion}");
                }

                int stateStringSize = projectFile.ReadInt32();
                int imageBufferSize = projectFile.ReadInt32();

                if (imageBufferSize == 0)
                {
                    throw new ParsingError("Trying to import an fSpy project with no image data");
                }

                projectFile.BaseStream.Seek(16, SeekOrigin.Begin);
                string stateJson = Encoding.UTF8.GetString(projectFile.ReadBytes(stateStringSize));
                using (JsonDocument doc = JsonDocument.Parse(stateJson))
                {
                    JsonElement root = doc.RootElement;
                    CameraParameters = new CameraParameters(root.GetProperty("cameraParameters"));
                    var calibrationSettings = root.GetProperty("calibrationSettingsBase");
                    ReferenceDistanceUnit = calibrationSettings.GetProperty("referenceDistanceUnit").GetString();
                }
                ImageData = projectFile.ReadBytes(imageBufferSize);
                FileName = Path.GetFileName(projectPath);
            }
        }

        public CameraInfo GetCameraInfo()
        {
            Matrix4x4 cameraTransform = ConvertToMatrix4x4(this.CameraParameters.CameraTransform);
            Matrix4x4 invCameraTransform;
            bool invertible = Matrix4x4.Invert(cameraTransform, out invCameraTransform);

            if (!invertible)
            {
                throw new InvalidOperationException("Camera transform matrix is not invertible");
            }

            var camLoc = new Rhino.Geometry.Point3d(cameraTransform.M41, cameraTransform.M42, cameraTransform.M43);
            var camX = new Rhino.Geometry.Vector3d(invCameraTransform.M11, invCameraTransform.M21, invCameraTransform.M31);
            var camY = new Rhino.Geometry.Vector3d(invCameraTransform.M12, invCameraTransform.M22, invCameraTransform.M32);
            var camZ = new Rhino.Geometry.Vector3d(invCameraTransform.M13, invCameraTransform.M23, invCameraTransform.M33);

            return new CameraInfo
            {
                Right = camX,
                Up = camY,
                Forward = -camZ,
                Location = camLoc
            };
        }

        public string SaveImageData(string file_path)
        {

            // Assuming fspyProject.ImageData is a byte array
            File.WriteAllBytes(file_path, this.ImageData);

            return file_path;
        }

        private static Matrix4x4 ConvertToMatrix4x4(float[][] array)
        {
            return new Matrix4x4(
                array[0][0], array[1][0], array[2][0], array[3][0],
                array[0][1], array[1][1], array[2][1], array[3][1],
                array[0][2], array[1][2], array[2][2], array[3][2],
                array[0][3], array[1][3], array[2][3], array[3][3]
            );
        }
    }
}