using System;
using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System.IO;

namespace DayNightSimulation
{
    // Klasa materiału
    public class Material
    {
        public Vector3 Ambient;
        public Vector3 Diffuse;
        public Vector3 Specular;
        public float Shininess;

        public Material(Vector3 ambient, Vector3 diffuse, Vector3 specular, float shininess)
        {
            Ambient = ambient;
            Diffuse = diffuse;
            Specular = specular;
            Shininess = shininess;
        }
    }

    // Klasa chmury
    public class Cloud
    {
        public Vector3 Position;
        public Vector3 Scale;
        public float Speed;
        public float Density;

        public Cloud(Vector3 position, Vector3 scale, float speed, float density)
        {
            Position = position;
            Scale = scale;
            Speed = speed;
            Density = density;
        }
    }

    // Klasa shadera
    public class Shader
    {
        public int ProgramID;
        private Dictionary<string, int> uniformLocations;

        public Shader(string vertexPath, string fragmentPath)
        {
            uniformLocations = new Dictionary<string, int>();
            ProgramID = CreateShaderProgram(vertexPath, fragmentPath);
        }

        private int CreateShaderProgram(string vertexPath, string fragmentPath)
        {
            string vertexSource = File.ReadAllText(vertexPath);
            string fragmentSource = File.ReadAllText(fragmentPath);

            int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            int program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);
            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                throw new Exception($"Shader program linking failed: {infoLog}");
            }

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            return program;
        }

        private int CompileShader(ShaderType type, string source)
        {
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"Shader compilation failed: {infoLog}");
            }

            return shader;
        }

        public void Use()
        {
            GL.UseProgram(ProgramID);
        }

        public void SetMatrix4(string name, Matrix4 matrix)
        {
            int location = GetUniformLocation(name);
            GL.UniformMatrix4(location, false, ref matrix);
        }

        public void SetVector3(string name, Vector3 vector)
        {
            int location = GetUniformLocation(name);
            GL.Uniform3(location, vector);
        }

        public void SetFloat(string name, float value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform1(location, value);
        }

        public void SetInt(string name, int value)
        {
            int location = GetUniformLocation(name);
            GL.Uniform1(location, value);
        }

        private int GetUniformLocation(string name)
        {
            if (uniformLocations.ContainsKey(name))
                return uniformLocations[name];

            int location = GL.GetUniformLocation(ProgramID, name);
            uniformLocations[name] = location;
            return location;
        }
    }

    // Klasa siatki 3D
    public class Mesh
    {
        private int VAO, VBO, EBO;
        private float[] vertices;
        private uint[] indices;

        public Mesh(float[] vertices, uint[] indices)
        {
            this.vertices = vertices;
            this.indices = indices;
            SetupMesh();
        }

        private void SetupMesh()
        {
            GL.GenVertexArrays(1, out VAO);
            GL.GenBuffers(1, out VBO);
            GL.GenBuffers(1, out EBO);

            GL.BindVertexArray(VAO);

            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            // Pozycje wierzchołków
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            // Normalne
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // Współrzędne tekstury
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);
        }

        public void Draw()
        {
            GL.BindVertexArray(VAO);
            GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
            GL.BindVertexArray(0);
        }
    }

    class Game : GameWindow
    {
        private float timeOfDay = 12.0f;
        private Vector3 cameraPos = new Vector3(0, 3, 8);
        private float yaw = 0f;
        private float pitch = -10f;
        private float speed = 4f;

        // Granice kamery
        private const float TERRAIN_SIZE = 40f;
        private const float MIN_CAMERA_HEIGHT = 1.0f;
        private const float MAX_CAMERA_HEIGHT = 20.0f;
        private const float CAMERA_BORDER = 2.0f; // margines od krawędzi

        private int grassTexture;
        private Shader mainShader;
        private Mesh cubeMesh;
        private Mesh terrainMesh;

        // Materiały
        private Material grassMaterial;
        private Material trunkMaterial;
        private Material leafMaterial;
        private Material stoneMaterial;
        private Material houseMaterial;
        private Material roofMaterial;
        private Material starMaterial;
        private Material cloudMaterial;

        // Gwiazdy i chmury
        private List<Vector3> stars;
        private List<Cloud> clouds;
        private Random random;

        public Game(int width, int height, string title)
            : base(width, height, GraphicsMode.Default, title)
        {
            VSync = VSyncMode.On;
            random = new Random();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Inicjalizacja shaderów
            CreateShaders();

            // Inicjalizacja meshy
            CreateMeshes();

            // Inicjalizacja materiałów
            CreateMaterials();

            // Inicjalizacja tekstur
            grassTexture = LoadTexture("Assets/grass.jpg");

            // Generuj gwiazdy i chmury
            GenerateStars();
            GenerateClouds();

            Console.Clear();
            Console.WriteLine("==== DAY & NIGHT SIMULATION ====");
            Console.WriteLine("W/S/A/D - move, Q/E - up/down, arrows - rotate");
            Console.WriteLine("P - advance hour, O - reverse hour, ESC - exit");
            Console.WriteLine($"Camera bounds: X,Z: ±{TERRAIN_SIZE - CAMERA_BORDER:F1}, Y: {MIN_CAMERA_HEIGHT:F1}-{MAX_CAMERA_HEIGHT:F1}");
            Console.WriteLine($"Stars: {stars.Count}, Clouds: {clouds.Count}");
        }

        private void GenerateStars()
        {
            stars = new List<Vector3>();
            int starCount = 300;

            for (int i = 0; i < starCount; i++)
            {
                float x = (float)(random.NextDouble() * 2 - 1) * 80f;
                float y = 25f + (float)random.NextDouble() * 20f;
                float z = (float)(random.NextDouble() * 2 - 1) * 80f;

                stars.Add(new Vector3(x, y, z));
            }
        }

        private void GenerateClouds()
        {
            clouds = new List<Cloud>();
            int cloudCount = 25;

            for (int i = 0; i < cloudCount; i++)
            {
                float x = (float)(random.NextDouble() * 2 - 1) * 70f;
                float y = 12f + (float)random.NextDouble() * 6f;
                float z = (float)(random.NextDouble() * 2 - 1) * 70f;

                float scaleX = 4f + (float)random.NextDouble() * 6f;
                float scaleY = 1f + (float)random.NextDouble() * 2f;
                float scaleZ = 3f + (float)random.NextDouble() * 4f;

                float speed = 0.2f + (float)random.NextDouble() * 0.8f;
                float density = 0.5f + (float)random.NextDouble() * 0.5f;

                clouds.Add(new Cloud(
                    new Vector3(x, y, z),
                    new Vector3(scaleX, scaleY, scaleZ),
                    speed,
                    density
                ));
            }
        }

        private void CreateShaders()
        {
            SaveShaderFiles();
            mainShader = new Shader("Shaders/shader.vert", "Shaders/shader.frag");
        }

        private void SaveShaderFiles()
        {
            Directory.CreateDirectory("Shaders");

            string vertexShader = @"#version 330 core
layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoords;

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoords;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    FragPos = vec3(model * vec4(aPos, 1.0));
    Normal = mat3(transpose(inverse(model))) * aNormal;
    TexCoords = aTexCoords;
    gl_Position = projection * view * vec4(FragPos, 1.0);
}";
            File.WriteAllText("Shaders/shader.vert", vertexShader);

            string fragmentShader = @"#version 330 core
out vec4 FragColor;

in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoords;

struct Material {
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
    float shininess;
};

struct Light {
    vec3 position;
    vec3 ambient;
    vec3 diffuse;
    vec3 specular;
};

uniform Material material;
uniform Light light;
uniform vec3 viewPos;
uniform sampler2D texture_diffuse1;
uniform bool useTexture;
uniform float alphaMultiplier = 1.0;

void main()
{
    // Ambient
    vec3 ambient = light.ambient * material.ambient;
    
    // Diffuse
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(light.position - FragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = light.diffuse * (diff * material.diffuse);
    
    // Specular
    vec3 viewDir = normalize(viewPos - FragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), material.shininess);
    vec3 specular = light.specular * (spec * material.specular);
    
    vec4 texColor = useTexture ? texture(texture_diffuse1, TexCoords) : vec4(1.0);
    vec3 result = (ambient + diffuse + specular) * texColor.rgb;
    FragColor = vec4(result, texColor.a * alphaMultiplier);
}";
            File.WriteAllText("Shaders/shader.frag", fragmentShader);
        }

        private void CreateMeshes()
        {
            // Sześcian
            float[] cubeVertices = {
                // positions          // normals           // texture coords
                -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,
                 0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
                 0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
                -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 1.0f,
                -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,

                -0.5f, -0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   0.0f, 0.0f,
                 0.5f, -0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   1.0f, 1.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   1.0f, 1.0f,
                -0.5f,  0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   0.0f, 1.0f,
                -0.5f, -0.5f,  0.5f,  0.0f,  0.0f, 1.0f,   0.0f, 0.0f,

                -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
                -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
                -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,

                 0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                 0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
                 0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,

                -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
                 0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
                 0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
                -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,

                -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f,
                 0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 1.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
                -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
                -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f
            };

            uint[] cubeIndices = {
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
                18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35
            };

            cubeMesh = new Mesh(cubeVertices, cubeIndices);

            // Teren
            float size = TERRAIN_SIZE;
            float[] terrainVertices = {
                -size, 0, -size,  0, 1, 0,  0.0f, 0.0f,
                -size, 0,  size,  0, 1, 0,  0.0f, 8.0f,
                 size, 0,  size,  0, 1, 0,  8.0f, 8.0f,
                 size, 0, -size,  0, 1, 0,  8.0f, 0.0f,
            };

            uint[] terrainIndices = { 0, 1, 2, 2, 3, 0 };
            terrainMesh = new Mesh(terrainVertices, terrainIndices);
        }

        private void CreateMaterials()
        {
            grassMaterial = new Material(
                new Vector3(0.2f, 0.5f, 0.2f),
                new Vector3(0.6f, 0.8f, 0.6f),
                new Vector3(0.2f, 0.2f, 0.2f),
                8.0f
            );

            trunkMaterial = new Material(
                new Vector3(0.2f, 0.1f, 0.05f),
                new Vector3(0.55f, 0.35f, 0.2f),
                new Vector3(0.3f, 0.2f, 0.1f),
                32.0f
            );

            leafMaterial = new Material(
                new Vector3(0.05f, 0.2f, 0.05f),
                new Vector3(0.1f, 0.6f, 0.2f),
                new Vector3(0.1f, 0.2f, 0.1f),
                16.0f
            );

            stoneMaterial = new Material(
                new Vector3(0.3f, 0.3f, 0.3f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.2f, 0.2f, 0.2f),
                64.0f
            );

            houseMaterial = new Material(
                new Vector3(0.8f, 0.7f, 0.6f),
                new Vector3(0.9f, 0.8f, 0.7f),
                new Vector3(0.3f, 0.3f, 0.3f),
                32.0f
            );

            roofMaterial = new Material(
                new Vector3(0.4f, 0.2f, 0.1f),
                new Vector3(0.6f, 0.3f, 0.2f),
                new Vector3(0.2f, 0.1f, 0.05f),
                16.0f
            );

            starMaterial = new Material(
                new Vector3(1.2f, 1.1f, 0.8f),
                new Vector3(1.4f, 1.3f, 1.0f),
                new Vector3(1.2f, 1.2f, 1.0f),
                2.0f
            );

            cloudMaterial = new Material(
                new Vector3(1.0f, 1.0f, 1.0f),
                new Vector3(1.0f, 1.0f, 1.0f),
                new Vector3(0.8f, 0.8f, 0.8f),
                4.0f
            );
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            GL.Viewport(0, 0, Width, Height);
        }

        private int LoadTexture(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                Console.WriteLine($"Warning: Texture file not found: {path}");
                return CreatePlaceholderTexture();
            }

            try
            {
                System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(path);
                System.Drawing.Imaging.BitmapData data = bmp.LockBits(
                    new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                );

                int tex;
                GL.GenTextures(1, out tex);
                GL.BindTexture(TextureTarget.Texture2D, tex);

                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                    data.Width, data.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

                bmp.UnlockBits(data);
                bmp.Dispose();

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

                GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

                return tex;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading texture: {ex.Message}");
                return CreatePlaceholderTexture();
            }
        }

        private int CreatePlaceholderTexture()
        {
            int tex;
            GL.GenTextures(1, out tex);
            GL.BindTexture(TextureTarget.Texture2D, tex);

            byte[] pixels = new byte[64 * 64 * 4];
            for (int i = 0; i < 64 * 64; i++)
            {
                pixels[i * 4] = 100;
                pixels[i * 4 + 1] = 200;
                pixels[i * 4 + 2] = 100;
                pixels[i * 4 + 3] = 255;
            }

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 64, 64, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            return tex;
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
            KeyboardState k = Keyboard.GetState();
            if (k.IsKeyDown(Key.Escape)) Exit();

            // Camera rotation
            if (k.IsKeyDown(Key.Left)) yaw -= 80f * (float)e.Time;
            if (k.IsKeyDown(Key.Right)) yaw += 80f * (float)e.Time;
            if (k.IsKeyDown(Key.Up)) pitch += 50f * (float)e.Time;
            if (k.IsKeyDown(Key.Down)) pitch -= 50f * (float)e.Time;
            pitch = MathHelper.Clamp(pitch, -89f, 89f);

            Vector3 forward = new Vector3(
                (float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(yaw)),
                (float)Math.Sin(MathHelper.DegreesToRadians(pitch)),
                -(float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(yaw))
            ).Normalized();
            Vector3 right = Vector3.Cross(forward, Vector3.UnitY).Normalized();

            // Camera movement
            Vector3 newCameraPos = cameraPos;

            if (k.IsKeyDown(Key.W)) newCameraPos += forward * speed * (float)e.Time;
            if (k.IsKeyDown(Key.S)) newCameraPos -= forward * speed * (float)e.Time;
            if (k.IsKeyDown(Key.A)) newCameraPos -= right * speed * (float)e.Time;
            if (k.IsKeyDown(Key.D)) newCameraPos += right * speed * (float)e.Time;
            if (k.IsKeyDown(Key.Q)) newCameraPos += Vector3.UnitY * speed * (float)e.Time;
            if (k.IsKeyDown(Key.E)) newCameraPos -= Vector3.UnitY * speed * (float)e.Time;

            // Apply camera constraints
            newCameraPos = ApplyCameraConstraints(newCameraPos);
            cameraPos = newCameraPos;

            // Update clouds
            UpdateClouds((float)e.Time);

            // Time control
            if (k.IsKeyDown(Key.P)) timeOfDay += 0.5f * (float)e.Time * 20f;
            if (k.IsKeyDown(Key.O)) timeOfDay -= 0.5f * (float)e.Time * 20f;
            if (timeOfDay > 24f) timeOfDay = 0f;
            if (timeOfDay < 0f) timeOfDay = 24f;

            int hour = (int)timeOfDay;
            int minute = (int)((timeOfDay - hour) * 60);
            Console.SetCursorPosition(0, 13);
            Console.Write($"Time: {hour:00}:{minute:00}   ");
            Console.SetCursorPosition(0, 14);
            Console.Write($"Camera: X:{cameraPos.X:00.0} Y:{cameraPos.Y:00.0} Z:{cameraPos.Z:00.0}   ");
        }

        private void UpdateClouds(float deltaTime)
        {
            for (int i = 0; i < clouds.Count; i++)
            {
                Cloud cloud = clouds[i];
                cloud.Position.X += cloud.Speed * deltaTime;

                if (cloud.Position.X > 70f)
                {
                    cloud.Position.X = -70f;
                    cloud.Position.Z = (float)(random.NextDouble() * 2 - 1) * 70f;
                }

                clouds[i] = cloud;
            }
        }

        private Vector3 ApplyCameraConstraints(Vector3 position)
        {
            float maxXZ = TERRAIN_SIZE - CAMERA_BORDER;
            position.X = MathHelper.Clamp(position.X, -maxXZ, maxXZ);
            position.Z = MathHelper.Clamp(position.Z, -maxXZ, maxXZ);
            position.Y = MathHelper.Clamp(position.Y, MIN_CAMERA_HEIGHT, MAX_CAMERA_HEIGHT);
            return position;
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            Vector3 sunPos = GetSunPosition();
            Vector3 moonPos = GetMoonPosition();
            float dayFactor = MathHelper.Clamp(sunPos.Y / 40f, 0f, 1f);

            Vector3 skyColor = GetSkyColor(timeOfDay);
            GL.ClearColor(skyColor.X, skyColor.Y, skyColor.Z, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(60f), Width / (float)Height, 0.1f, 200f);
            Vector3 dir = new Vector3(
                (float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(yaw)),
                (float)Math.Sin(MathHelper.DegreesToRadians(pitch)),
                -(float)Math.Cos(MathHelper.DegreesToRadians(pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(yaw))
            );
            Matrix4 view = Matrix4.LookAt(cameraPos, cameraPos + dir, Vector3.UnitY);

            mainShader.Use();
            mainShader.SetMatrix4("projection", projection);
            mainShader.SetMatrix4("view", view);
            mainShader.SetVector3("viewPos", cameraPos);

            Vector3 lightColor = new Vector3(1.0f, 0.9f, 0.8f);
            Vector3 moonLightColor = new Vector3(0.3f, 0.3f, 0.5f);

            if (dayFactor > 0.1f)
            {
                mainShader.SetVector3("light.position", sunPos);
                mainShader.SetVector3("light.ambient", lightColor * 0.2f);
                mainShader.SetVector3("light.diffuse", lightColor * 0.8f);
                mainShader.SetVector3("light.specular", lightColor * 1.0f);
            }
            else
            {
                mainShader.SetVector3("light.position", moonPos);
                mainShader.SetVector3("light.ambient", moonLightColor * 0.15f);
                mainShader.SetVector3("light.diffuse", moonLightColor * 0.4f);
                mainShader.SetVector3("light.specular", moonLightColor * 0.3f);
            }

            DrawTerrain();
            DrawSceneObjects();
            DrawClouds();

            if (dayFactor < 0.1f)
            {
                DrawStars();
            }

            if (sunPos.Y > 0)
            {
                DrawSun(sunPos);
            }
            if (dayFactor < 0.1f && moonPos.Y > 0)
            {
                DrawMoon(moonPos);
            }

            SwapBuffers();
        }

        private void DrawClouds()
        {
            GL.Enable(EnableCap.Blend);
            mainShader.Use();
            mainShader.SetInt("useTexture", 0);

            float cloudAlpha = MathHelper.Clamp(1.0f - Math.Abs(timeOfDay - 12f) / 6f, 0.3f, 0.8f);

            foreach (var cloud in clouds)
            {
                Matrix4 model = Matrix4.CreateScale(cloud.Scale.X, cloud.Scale.Y, cloud.Scale.Z) *
                              Matrix4.CreateTranslation(cloud.Position);
                mainShader.SetMatrix4("model", model);

                mainShader.SetVector3("material.ambient", cloudMaterial.Ambient * cloud.Density);
                mainShader.SetVector3("material.diffuse", cloudMaterial.Diffuse * cloud.Density);
                mainShader.SetVector3("material.specular", cloudMaterial.Specular * cloud.Density);
                mainShader.SetFloat("material.shininess", cloudMaterial.Shininess);
                mainShader.SetFloat("alphaMultiplier", cloudAlpha * cloud.Density);

                cubeMesh.Draw();
            }
            GL.Disable(EnableCap.Blend);
        }

        private void DrawStars()
        {
            GL.Disable(EnableCap.DepthTest);
            mainShader.Use();
            mainShader.SetInt("useTexture", 0);

            float starIntensity = 1.0f - MathHelper.Clamp((timeOfDay - 20f) / 4f, 0f, 1f);
            starIntensity = MathHelper.Clamp(starIntensity, 0f, 1f);
            starIntensity *= 2.5f;
            starIntensity = MathHelper.Clamp(starIntensity, 0f, 1.5f);

            mainShader.SetVector3("material.ambient", starMaterial.Ambient * starIntensity);
            mainShader.SetVector3("material.diffuse", starMaterial.Diffuse * starIntensity);
            mainShader.SetVector3("material.specular", starMaterial.Specular * starIntensity);
            mainShader.SetFloat("material.shininess", starMaterial.Shininess);
            mainShader.SetFloat("alphaMultiplier", starIntensity);

            foreach (var starPos in stars)
            {
                Matrix4 model = Matrix4.CreateScale(0.18f) * Matrix4.CreateTranslation(starPos);
                mainShader.SetMatrix4("model", model);
                cubeMesh.Draw();
            }

            GL.Enable(EnableCap.DepthTest);
        }

        private Vector3 GetSunPosition()
        {
            float angle = (timeOfDay / 24f) * 360f - 90f;
            float radius = 40f;
            float x = radius * (float)Math.Cos(MathHelper.DegreesToRadians(angle));
            float y = radius * (float)Math.Sin(MathHelper.DegreesToRadians(angle));
            float z = -10f;
            return new Vector3(x, y, z);
        }

        private Vector3 GetMoonPosition()
        {
            Vector3 sunPos = GetSunPosition();
            return new Vector3(-sunPos.X, -sunPos.Y, sunPos.Z);
        }

        private Vector3 GetSkyColor(float hour)
        {
            if (hour < 0) hour += 24f;
            if (hour >= 24f) hour -= 24f;

            if (hour >= 20f || hour <= 4f)
            {
                if (hour >= 20f && hour <= 22f)
                {
                    float t = (hour - 20f) / 2f;
                    return SmoothLerp(new Vector3(0.8f, 0.6f, 0.9f), new Vector3(0.05f, 0.05f, 0.2f), t);
                }
                else
                {
                    return new Vector3(0.05f, 0.05f, 0.2f);
                }
            }
            else if (hour > 4f && hour <= 7f)
            {
                float t = (hour - 4f) / 3f;
                return SmoothLerp(new Vector3(0.05f, 0.05f, 0.2f), new Vector3(0.9f, 0.7f, 0.4f), t);
            }
            else if (hour > 7f && hour <= 9f)
            {
                float t = (hour - 7f) / 2f;
                return SmoothLerp(new Vector3(0.9f, 0.7f, 0.4f), new Vector3(0.8f, 0.9f, 1.0f), t);
            }
            else if (hour > 9f && hour <= 17f)
            {
                return new Vector3(0.8f, 0.9f, 1.0f);
            }
            else
            {
                float t = (hour - 17f) / 3f;
                return SmoothLerp(new Vector3(0.8f, 0.9f, 1.0f), new Vector3(0.8f, 0.6f, 0.9f), t);
            }
        }

        private Vector3 SmoothLerp(Vector3 a, Vector3 b, float t)
        {
            t = t * t * (3.0f - 2.0f * t);
            return a + (b - a) * t;
        }

        private void DrawTerrain()
        {
            mainShader.Use();
            mainShader.SetInt("useTexture", 1);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, grassTexture);
            mainShader.SetFloat("alphaMultiplier", 1.0f);

            mainShader.SetVector3("material.ambient", grassMaterial.Ambient);
            mainShader.SetVector3("material.diffuse", grassMaterial.Diffuse);
            mainShader.SetVector3("material.specular", grassMaterial.Specular);
            mainShader.SetFloat("material.shininess", grassMaterial.Shininess);

            Matrix4 model = Matrix4.Identity;
            mainShader.SetMatrix4("model", model);
            terrainMesh.Draw();
        }

        private void DrawSceneObjects()
        {
            mainShader.SetFloat("alphaMultiplier", 1.0f);

            DrawTree(new Vector3(-5f, 0f, -5f), 1.2f);
            DrawTree(new Vector3(8f, 0f, -3f), 1.0f);
            DrawTree(new Vector3(-8f, 0f, 6f), 1.5f);
            DrawTree(new Vector3(12f, 0f, 8f), 1.3f);
            DrawTree(new Vector3(-12f, 0f, -8f), 1.1f);

            DrawStone(new Vector3(3f, 0f, 4f), 0.5f);
            DrawStone(new Vector3(-2f, 0f, -7f), 0.7f);
            DrawStone(new Vector3(6f, 0f, -10f), 0.4f);
            DrawStone(new Vector3(-10f, 0f, 2f), 0.6f);

            DrawHouse(new Vector3(0f, 0f, 0f), 1.0f);
            DrawHouse(new Vector3(15f, 0f, -5f), 0.8f);
            DrawHouse(new Vector3(-15f, 0f, 5f), 1.2f);
        }

        private void DrawTree(Vector3 position, float scale = 1.0f)
        {
            mainShader.Use();
            mainShader.SetInt("useTexture", 0);

            float trunkHeight = 1.5f * scale;
            float trunkWidth = 0.3f * scale;

            Matrix4 trunkModel = Matrix4.CreateScale(trunkWidth, trunkHeight, trunkWidth) *
                                Matrix4.CreateTranslation(position + new Vector3(0, trunkHeight / 2, 0));
            mainShader.SetMatrix4("model", trunkModel);
            mainShader.SetVector3("material.ambient", trunkMaterial.Ambient);
            mainShader.SetVector3("material.diffuse", trunkMaterial.Diffuse);
            mainShader.SetVector3("material.specular", trunkMaterial.Specular);
            mainShader.SetFloat("material.shininess", trunkMaterial.Shininess);
            cubeMesh.Draw();

            float leavesSize = 1.2f * scale;
            float leavesYOffset = trunkHeight + leavesSize / 2 - 0.1f;

            Matrix4 leavesModel = Matrix4.CreateScale(leavesSize, leavesSize, leavesSize) *
                                 Matrix4.CreateTranslation(position + new Vector3(0, leavesYOffset, 0));
            mainShader.SetMatrix4("model", leavesModel);
            mainShader.SetVector3("material.ambient", leafMaterial.Ambient);
            mainShader.SetVector3("material.diffuse", leafMaterial.Diffuse);
            mainShader.SetVector3("material.specular", leafMaterial.Specular);
            mainShader.SetFloat("material.shininess", leafMaterial.Shininess);
            cubeMesh.Draw();
        }

        private void DrawStone(Vector3 position, float scale = 1.0f)
        {
            mainShader.Use();
            mainShader.SetInt("useTexture", 0);

            float stoneHeight = 0.4f * scale;
            float stoneWidth = 0.8f * scale;

            Matrix4 model = Matrix4.CreateScale(stoneWidth, stoneHeight, stoneWidth) *
                           Matrix4.CreateTranslation(position + new Vector3(0, stoneHeight / 2, 0));
            mainShader.SetMatrix4("model", model);
            mainShader.SetVector3("material.ambient", stoneMaterial.Ambient);
            mainShader.SetVector3("material.diffuse", stoneMaterial.Diffuse);
            mainShader.SetVector3("material.specular", stoneMaterial.Specular);
            mainShader.SetFloat("material.shininess", stoneMaterial.Shininess);
            cubeMesh.Draw();
        }

        private void DrawHouse(Vector3 position, float scale = 1.0f)
        {
            mainShader.Use();
            mainShader.SetInt("useTexture", 0);

            float houseHeight = 1.0f * scale;
            float houseWidth = 1.5f * scale;

            Matrix4 houseModel = Matrix4.CreateScale(houseWidth, houseHeight, houseWidth) *
                               Matrix4.CreateTranslation(position + new Vector3(0, houseHeight / 2, 0));
            mainShader.SetMatrix4("model", houseModel);
            mainShader.SetVector3("material.ambient", houseMaterial.Ambient);
            mainShader.SetVector3("material.diffuse", houseMaterial.Diffuse);
            mainShader.SetVector3("material.specular", houseMaterial.Specular);
            mainShader.SetFloat("material.shininess", houseMaterial.Shininess);
            cubeMesh.Draw();

            float roofHeight = 0.8f * scale;
            float roofWidth = 1.6f * scale;
            float roofYOffset = houseHeight + roofHeight / 2 - 0.1f;

            Matrix4 roofModel = Matrix4.CreateScale(roofWidth, roofHeight, roofWidth) *
                              Matrix4.CreateTranslation(position + new Vector3(0, roofYOffset, 0));
            mainShader.SetMatrix4("model", roofModel);
            mainShader.SetVector3("material.ambient", roofMaterial.Ambient);
            mainShader.SetVector3("material.diffuse", roofMaterial.Diffuse);
            mainShader.SetVector3("material.specular", roofMaterial.Specular);
            mainShader.SetFloat("material.shininess", roofMaterial.Shininess);
            cubeMesh.Draw();
        }

        private void DrawSun(Vector3 pos)
        {
            GL.Disable(EnableCap.DepthTest);
            mainShader.Use();
            mainShader.SetInt("useTexture", 0);
            mainShader.SetFloat("alphaMultiplier", 1.0f);

            Matrix4 model = Matrix4.CreateTranslation(pos);
            mainShader.SetMatrix4("model", model);
            mainShader.SetVector3("material.ambient", new Vector3(1f, 0.9f, 0.3f));
            mainShader.SetVector3("material.diffuse", new Vector3(1f, 0.9f, 0.3f));
            mainShader.SetVector3("material.specular", new Vector3(1f, 0.9f, 0.3f));
            mainShader.SetFloat("material.shininess", 1f);
            cubeMesh.Draw();
            GL.Enable(EnableCap.DepthTest);
        }

        private void DrawMoon(Vector3 pos)
        {
            GL.Disable(EnableCap.DepthTest);
            mainShader.Use();
            mainShader.SetInt("useTexture", 0);
            mainShader.SetFloat("alphaMultiplier", 1.0f);

            Matrix4 model = Matrix4.CreateScale(1.2f) * Matrix4.CreateTranslation(pos);
            mainShader.SetMatrix4("model", model);
            mainShader.SetVector3("material.ambient", new Vector3(1.0f, 1.0f, 1.2f));
            mainShader.SetVector3("material.diffuse", new Vector3(1.2f, 1.2f, 1.4f));
            mainShader.SetVector3("material.specular", new Vector3(1.0f, 1.0f, 1.2f));
            mainShader.SetFloat("material.shininess", 2f);
            cubeMesh.Draw();
            GL.Enable(EnableCap.DepthTest);
        }
    }
}