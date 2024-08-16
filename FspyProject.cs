using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public class Point2d
    {
        [JsonInclude, JsonPropertyName("x")]
        public float X { get; private set; }

        [JsonInclude, JsonPropertyName("y")]
        public float Y { get; private set; }

        [JsonConstructor]
        public Point2d(float x, float y)
        {
            (X, Y) = (x, y);
        }

    }


    public class CameraParameters
    {
        [JsonInclude, JsonPropertyName("principalPoint")]
        public Point2d PrincipalPoint { get; private set; }

        [JsonInclude, JsonPropertyName("horizontalFieldOfView")]
        public float FovHoriz { get; private set; }

        [JsonInclude, JsonPropertyName("cameraTransform")]
        [JsonConverter(typeof(CameraTransformConverter))]
        public float[][] CameraTransform { get; private set; }

        [JsonInclude, JsonPropertyName("imageWidth")]
        public int ImageWidth { get; private set; }

        [JsonInclude, JsonPropertyName("imageHeight")]
        public int ImageHeight { get; private set; }

        [JsonInclude, JsonPropertyName("relativeFocalLength")]
        public double RelativeFocalLength { get; private set; }

        public CameraParameters(JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Undefined)
            {
                throw new ParsingError("Trying to import an fSpy project with no camera parameters");
            }

            var principalPoint = jsonElement.GetProperty("principalPoint");
            PrincipalPoint = new Point2d(principalPoint.GetProperty("x").GetSingle(), principalPoint.GetProperty("y").GetSingle());
            FovHoriz = jsonElement.GetProperty("horizontalFieldOfView").GetSingle();
            CameraTransform = jsonElement.GetProperty("cameraTransform").GetProperty("rows").Deserialize<float[][]>();
            ImageWidth = jsonElement.GetProperty("imageWidth").GetInt32();
            ImageHeight = jsonElement.GetProperty("imageHeight").GetInt32();
            RelativeFocalLength = jsonElement.GetProperty("relativeFocalLength").GetDouble();
        }

        public class CameraTransformConverter : JsonConverter<float[][]>
        {
            public override float[][] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
                {
                    JsonElement root = doc.RootElement;
                    JsonElement rowsElement = root.GetProperty("rows");

                    float[][] result = new float[rowsElement.GetArrayLength()][];

                    for (int i = 0; i < result.Length; i++)
                    {
                        JsonElement row = rowsElement[i];
                        result[i] = new float[row.GetArrayLength()];
                        for (int j = 0; j < result[i].Length; j++)
                        {
                            result[i][j] = row[j].GetSingle();
                        }
                    }

                    return result;
                }
            }

            public override void Write(Utf8JsonWriter writer, float[][] value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WriteStartArray("rows");
                foreach (var row in value)
                {
                    writer.WriteStartArray();
                    foreach (var item in row)
                    {
                        writer.WriteNumberValue(item);
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
        }
    }



    public class CameraParametersConverter : JsonConverter<CameraParameters>
    {
        public override CameraParameters Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                JsonElement root = doc.RootElement;
                // JsonElement rowsElement = root.GetProperty("cameraParameters");
                return new CameraParameters(root);
            }
        }

        public override void Write(Utf8JsonWriter writer, CameraParameters value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(JsonSerializer.Serialize(value));
        }
    }

    public class FspyProject
    {
        [JsonInclude, JsonPropertyName("projectVersion")]
        public int ProjectVersion { get; private set; }

        [JsonInclude, JsonPropertyName("cameraParameters")]
        [JsonConverter(typeof(CameraParametersConverter))]
        public CameraParameters CameraParameters { get; private set; }

        [JsonInclude, JsonPropertyName("referenceDistanceUnit")]
        public string ReferenceDistanceUnit { get; private set; }

        [JsonIgnore]
        public byte[] ImageData { get; private set; }

        [JsonInclude, JsonPropertyName("fileName")]
        public string FileName { get; private set; }
        // Parameterless constructor for JSON deserialization

        [JsonConstructor]
        public FspyProject(int projectVersion, CameraParameters cameraParameters, string referenceDistanceUnit, string fileName) {
            (ProjectVersion, CameraParameters, ReferenceDistanceUnit, FileName) = (projectVersion, cameraParameters, referenceDistanceUnit, fileName);
        }
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