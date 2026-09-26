// Earth Guardian, 2026-09-26. SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace MemoryOrb
{
    /// <summary>A real, textured 3D sphere. Idle rendering has no animation clock.</summary>
    public sealed class EarthView : Grid
    {
        private readonly AxisAngleRotation3D rotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), -100);
        private readonly ImageBrush surfaceBrush = new ImageBrush { ViewportUnits = BrushMappingMode.Absolute, TileMode = TileMode.None };
        private readonly BitmapSource[] textures = new BitmapSource[21];
        private readonly byte[] sourcePixels;
        private readonly int textureWidth, textureHeight, stride;
        public int PressureBand { get; private set; } = -1;
        public EarthView()
        {
            IsHitTestVisible = false;
            var viewport = new Viewport3D { ClipToBounds = false };
            viewport.Camera = new OrthographicCamera(new Point3D(0, 0, 3), new Vector3D(0, 0, -3), new Vector3D(0, 1, 0), 2.08);
            var scene = new Model3DGroup();
            scene.Children.Add(new AmbientLight(Color.FromRgb(85, 85, 85)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(230, 242, 255), new Vector3D(0.6, -0.6, -1)));
            var texture = new BitmapImage();
            texture.BeginInit();
            texture.UriSource = new Uri("pack://application:,,,/EarthGuardian;component/Assets/earth-map.png");
            texture.DecodePixelWidth = 512;
            texture.CacheOption = BitmapCacheOption.OnLoad;
            texture.EndInit();
            texture.Freeze();
            var pixels = new FormatConvertedBitmap(texture, PixelFormats.Bgra32, null, 0);
            textureWidth = pixels.PixelWidth; textureHeight = pixels.PixelHeight; stride = textureWidth * 4;
            sourcePixels = new byte[stride * textureHeight];
            pixels.CopyPixels(sourcePixels, stride, 0);
            SetMemoryLoad(0);
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(surfaceBrush));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(75, 175, 219, 255)), 28));
            var globe = new GeometryModel3D(CreateSphere(), material) { Transform = new RotateTransform3D(rotation) };
            scene.Children.Add(globe);
            viewport.Children.Add(new ModelVisual3D { Content = scene });
            Children.Add(viewport);
            Unloaded += (s, e) => SetOptimizing(false);
        }

        public void SetMemoryLoad(int load)
        {
            // Keep the last known color if the system reading is invalid.
            if (load < 0 || load > 100) return;
            int band = MemoryPressurePalette.Band(load);
            if (PressureBand == band) return;
            if (textures[band] == null)
            {
                var output = new byte[sourcePixels.Length];
                Color landColor = MemoryPressurePalette.Surface(true, band), oceanColor = MemoryPressurePalette.Surface(false, band);
                for (int i = 0; i < output.Length; i += 4)
                {
                    bool land = sourcePixels[i + 1] > sourcePixels[i] * .9;
                    Color tint = land ? landColor : oceanColor;
                    double shade = land ? .70 + .30 * sourcePixels[i + 1] / 191.0 : .93 + .07 * sourcePixels[i] / 255.0;
                    output[i] = (byte)Math.Min(255, tint.B * shade);
                    output[i + 1] = (byte)Math.Min(255, tint.G * shade);
                    output[i + 2] = (byte)Math.Min(255, tint.R * shade);
                    output[i + 3] = 255;
                }
                var result = BitmapSource.Create(textureWidth, textureHeight, 96, 96, PixelFormats.Bgra32, null, output, stride);
                result.Freeze(); textures[band] = result;
            }
            surfaceBrush.ImageSource = textures[band];
            PressureBand = band;
        }

        private static MeshGeometry3D CreateSphere()
        {
            const int longitudeSteps = 64, latitudeSteps = 32;
            var mesh = new MeshGeometry3D();
            for (int y = 0; y <= latitudeSteps; y++)
            {
                double latitude = Math.PI / 2 - Math.PI * y / latitudeSteps;
                for (int x = 0; x <= longitudeSteps; x++)
                {
                    double longitude = 2 * Math.PI * x / longitudeSteps - Math.PI;
                    double px = Math.Cos(latitude) * Math.Sin(longitude), py = Math.Sin(latitude), pz = Math.Cos(latitude) * Math.Cos(longitude);
                    mesh.Positions.Add(new Point3D(px, py, pz));
                    mesh.Normals.Add(new Vector3D(px, py, pz));
                    mesh.TextureCoordinates.Add(new Point((double)x / longitudeSteps, (double)y / latitudeSteps));
                }
            }
            for (int y = 0; y < latitudeSteps; y++)
                for (int x = 0; x < longitudeSteps; x++)
                {
                    int a = y * (longitudeSteps + 1) + x, b = a + longitudeSteps + 1;
                    mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(a + 1);
                    mesh.TriangleIndices.Add(a + 1); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 1);
                }
            mesh.Freeze();
            return mesh;
        }

        public void SetOptimizing(bool active)
        {
            if (!active) { rotation.BeginAnimation(AxisAngleRotation3D.AngleProperty, null); return; }
            var animation = new DoubleAnimation(-100, 260, TimeSpan.FromSeconds(6)) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(animation, 24);
            rotation.BeginAnimation(AxisAngleRotation3D.AngleProperty, animation);
        }
    }
}
