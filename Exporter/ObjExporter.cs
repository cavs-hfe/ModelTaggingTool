namespace ModelViewer.Exporter
{
    using HelixToolkit.Wpf;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Media.Media3D;
    using System.Xml;

    /// <summary>
    /// Export the 3D visual tree to a Wavefront OBJ file
    /// </summary>
    /// <remarks>
    /// http://en.wikipedia.org/wiki/Obj
    /// http://www.martinreddy.net/gfx/3d/OBJ.spec
    /// http://www.eg-models.de/formats/Format_Obj.html
    /// </remarks>
    public class ObjExporter
    {
        /// <summary>
        /// Gets or sets a value indicating whether to export normals.
        /// </summary>
        public bool ExportNormals { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to use "d" for transparency (default is "Tr").
        /// </summary>
        public bool UseDissolveForTransparency { get; set; }
        /// <summary>
        /// The exported materials.
        /// </summary>
        private readonly Dictionary<Material, string> exportedMaterials = new Dictionary<Material, string>();
        /// <summary>
        /// The group no.
        /// </summary>
        private int groupNo = 1;
        /// <summary>
        /// The mat no.
        /// </summary>
        private int matNo = 1;
        /// <summary>
        /// Normal index counter.
        /// </summary>
        private int normalIndex = 1;
        /// <summary>
        /// The object no.
        /// </summary>
        private int objectNo = 1;
        /// <summary>
        /// Texture index counter.
        /// </summary>
        private int textureIndex = 1;
        /// <summary>
        /// Vertex index counter.
        /// </summary>
        private int vertexIndex = 1;
        /// <summary>
        /// Initializes a new instance of the <see cref="ObjExporter" /> class.
        /// </summary>
        public ObjExporter()
        {
            this.TextureExtension = ".png";
            this.TextureSize = 1024;
            this.TextureQualityLevel = 90;
            this.SwitchYZ = true;
            this.ExportNormals = false;
            this.FileCreator = File.Create;
        }
        /// <summary>
        /// Gets or sets the comment.
        /// </summary>
        public string Comment { get; set; }
        /// <summary>
        /// Gets or sets the materials file.
        /// </summary>
        /// <value>
        /// The materials file.
        /// </value>
        public string MaterialsFile { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to switch Y and Z coordinates.
        /// </summary>
        public bool SwitchYZ { get; set; }
        /// <summary>
        /// Gets or sets the texture folder.
        /// </summary>
        public string TextureFolder { get; set; }
        /// <summary>
        /// Gets or sets the texture extension (.png or .jpg).
        /// </summary>
        /// <value>
        /// The default value is ".png".
        /// </value>
        public string TextureExtension { get; set; }
        /// <summary>
        /// Gets or sets the texture size.
        /// </summary>
        /// <value>
        /// The default value is 1024.
        /// </value>
        public int TextureSize { get; set; }
        /// <summary>
        /// Gets or sets the texture quality level (for JPEG encoding).
        /// </summary>
        /// <value>
        /// The quality level of the JPEG image. The value range is 1 (lowest quality) to 100 (highest quality) inclusive.
        /// The default value is 90.
        /// </value>
        public int TextureQualityLevel { get; set; }
        /// <summary>
        /// Gets or sets the file creator.
        /// </summary>
        /// <value>The file creator.</value>
        public Func<string, Stream> FileCreator { get; set; }
        /// <summary>
        /// Exports the specified model.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="stream">The output stream.</param>
        public virtual void Export(Model3D model, Stream stream)
        {
            var writer = this.Create(stream);
            //this.ExportHeader(writer);
            Model3DHelper.Traverse<GeometryModel3D>(model, (m, t) => this.ExportModel(writer, m, t));
            this.Close(writer);
        }
        /// <summary>
        /// Exports the mesh.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="m">The mesh geometry.</param>
        /// <param name="t">The transform.</param>
        public void ExportMesh(StreamWriter writer, MeshGeometry3D m, Transform3D t)
        {
            if (m == null)
            {
                throw new ArgumentNullException("m");
            }
            if (t == null)
            {
                throw new ArgumentNullException("t");
            }
            // mapping from local indices (0-based) to the obj file indices (1-based)
            var vertexIndexMap = new Dictionary<int, int>();
            var textureIndexMap = new Dictionary<int, int>();
            var normalIndexMap = new Dictionary<int, int>();
            int index = 0;
            if (m.Positions != null)
            {
                foreach (var v in m.Positions)
                {
                    vertexIndexMap.Add(index++, this.vertexIndex++);
                    var p = t.Transform(v);
                    writer.WriteLine(
                    string.Format(
                    CultureInfo.InvariantCulture,
                    "v {0} {1} {2}",
                    p.X,
                    this.SwitchYZ ? p.Z : p.Y,
                    this.SwitchYZ ? -p.Y : p.Z));
                }
                writer.WriteLine(string.Format("# {0} vertices", index));
            }
            if (m.TextureCoordinates != null)
            {
                index = 0;
                foreach (var vt in m.TextureCoordinates)
                {
                    textureIndexMap.Add(index++, this.textureIndex++);
                    writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "vt {0} {1}", vt.X, 1 - vt.Y));
                }
                writer.WriteLine(string.Format("# {0} texture coordinates", index));
            }
            if (m.Normals != null && ExportNormals)
            {
                index = 0;
                foreach (var vn in m.Normals)
                {
                    normalIndexMap.Add(index++, this.normalIndex++);
                    writer.WriteLine(
                    string.Format(CultureInfo.InvariantCulture, "vn {0} {1} {2}", vn.X, vn.Y, vn.Z));
                }
                writer.WriteLine(string.Format("# {0} normals", index));
            }
            Func<int, string> formatIndices = i0 =>
            {
                bool hasTextureIndex = textureIndexMap.ContainsKey(i0);
                bool hasNormalIndex = normalIndexMap.ContainsKey(i0);
                if (hasTextureIndex && hasNormalIndex)
                {
                    return string.Format("{0}/{1}/{2}", vertexIndexMap[i0], textureIndexMap[i0], normalIndexMap[i0]);
                }
                if (hasTextureIndex)
                {
                    return string.Format("{0}/{1}", vertexIndexMap[i0], textureIndexMap[i0]);
                }
                if (hasNormalIndex)
                {
                    return string.Format("{0}//{1}", vertexIndexMap[i0], normalIndexMap[i0]);
                }
                return vertexIndexMap[i0].ToString();
            };
            if (m.TriangleIndices != null)
            {
                for (int i = 0; i < m.TriangleIndices.Count; i += 3)
                {
                    int i0 = m.TriangleIndices[i];
                    int i1 = m.TriangleIndices[i + 1];
                    int i2 = m.TriangleIndices[i + 2];
                    writer.WriteLine("f {0} {1} {2}", formatIndices(i0), formatIndices(i1), formatIndices(i2));
                }
                writer.WriteLine(string.Format("# {0} faces", m.TriangleIndices.Count / 3));
            }
            writer.WriteLine();
        }
        /// <summary>
        /// Creates the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>StreamWriter.</returns>
        protected ObjWriters Create(Stream stream)
        {
            var writer = new StreamWriter(stream);
            if (!string.IsNullOrEmpty(this.Comment))
            {
                writer.WriteLine("# {0}", this.Comment);
            }
            writer.WriteLine("mtllib ./" + this.MaterialsFile);
            var materialStream = this.FileCreator(this.MaterialsFile);
            var materialWriter = new StreamWriter(materialStream);
            return new ObjWriters { ObjWriter = writer, MaterialsWriter = materialWriter };
        }
        /// <summary>
        /// Closes the specified writer.
        /// </summary>
        /// <param name="writer">The writer.</param>
        protected void Close(ObjWriters writer)
        {
            writer.ObjWriter.Close();
            writer.MaterialsWriter.Close();
        }
        /// <summary>
        /// Renders the brush to an image.
        /// </summary>
        /// <param name="path">
        /// The output path. If the path extension is .png, a PNG image is generated, otherwise a JPEG image.
        /// </param>
        /// <param name="brush">
        /// The brush to render.
        /// </param>
        /// <param name="w">
        /// The width of the output image.
        /// </param>
        /// <param name="h">
        /// The height of the output image.
        /// </param>
        /// <param name="qualityLevel">
        /// The quality level of the image (only used if an JPEG image is exported).
        /// The value range is 1 (lowest quality) to 100 (highest quality).
        /// </param>
        public void RenderBrush(string path, Brush brush, int w, int h, int qualityLevel = 90)
        {
            var ib = brush as ImageBrush;
            if (ib != null)
            {
                var bi = ib.ImageSource as BitmapImage;
                if (bi != null)
                {
                    w = bi.PixelWidth;
                    h = bi.PixelHeight;
                }
            }
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var rect = new Grid
            {
                Background = brush,
                Width = 1,
                Height = 1,
                LayoutTransform = new ScaleTransform(w, h)
            };
            rect.Arrange(new Rect(0, 0, w, h));
            bmp.Render(rect);
            var ext = (Path.GetExtension(path) ?? string.Empty).ToLower();
            BitmapEncoder encoder;
            if (ext == ".png")
            {
                encoder = new PngBitmapEncoder();
            }
            else
            {
                encoder = new JpegBitmapEncoder { QualityLevel = qualityLevel };
            }
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var stm = this.FileCreator(path))
            {
                encoder.Save(stm);
            }
        }
        /// <summary>
        /// Combines two transforms.
        /// </summary>
        /// <param name="t1">
        /// The first transform.
        /// </param>
        /// <param name="t2">
        /// The second transform.
        /// </param>
        /// <returns>
        /// The combined transform group.
        /// </returns>
        public Transform3D CombineTransform(Transform3D t1, Transform3D t2)
        {
            var g = new Transform3DGroup();
            g.Children.Add(t1);
            g.Children.Add(t2);
            return g;
        }
        /// <summary>
        /// The export model.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="model">The model.</param>
        /// <param name="transform">The transform.</param>
        public void ExportModel(ObjWriters writer, GeometryModel3D model, Transform3D transform)
        {
            /*writer.ObjWriter.WriteLine("o object{0}", this.objectNo++);
            writer.ObjWriter.WriteLine("g group{0}", this.groupNo++);
            if (this.exportedMaterials.ContainsKey(model.Material))
            {
                string matName = this.exportedMaterials[model.Material];
                writer.ObjWriter.WriteLine("usemtl {0}", matName);
            }
            else
            {
                string matName = string.Format("mat{0}", this.matNo++);
                writer.ObjWriter.WriteLine("usemtl {0}", matName);
                this.ExportMaterial(writer.MaterialsWriter, matName, model.Material, model.BackMaterial);
                this.exportedMaterials.Add(model.Material, matName);
            }
            var mesh = model.Geometry as MeshGeometry3D;
            this.ExportMesh(writer.ObjWriter, mesh, CombineTransform(transform, model.Transform));*/
        }
        /// <summary>
        /// Changes the intensity.
        /// </summary>
        /// <param name="c">
        /// The c.
        /// </param>
        /// <param name="factor">
        /// The factor.
        /// </param>
        /// <returns>
        /// </returns>
        /*public Color ChangeIntensity(this Color c, double factor)
        {
            var hsv = ColorToHsv(c);
            hsv[2] *= factor;
            if (hsv[2] > 1.0)
            {
                hsv[2] = 1.0;
            }
            return HsvToColor(hsv);
        }*/
        /// <summary>
        /// Converts from a <see cref="Color"/> to HSV values (double)
        /// </summary>
        /// <param name="color">
        /// </param>
        /// <returns>
        /// Array of [Hue,Saturation,Value] in the range [0,1]
        /// </returns>
        public double[] ColorToHsv(Color color)
        {
            byte r = color.R;
            byte g = color.G;
            byte b = color.B;
            double h = 0, s, v;
            double min = Math.Min(Math.Min(r, g), b);
            v = Math.Max(Math.Max(r, g), b);
            double delta = v - min;
            if (v == 0.0)
            {
                s = 0;
            }
            else
            {
                s = delta / v;
            }
            if (s == 0)
            {
                h = 0.0;
            }
            else
            {
                if (r == v)
                {
                    h = (g - b) / delta;
                }
                else if (g == v)
                {
                    h = 2 + (b - r) / delta;
                }
                else if (b == v)
                {
                    h = 4 + (r - g) / delta;
                }
                h *= 60;
                if (h < 0.0)
                {
                    h = h + 360;
                }
            }
            var hsv = new double[3];
            hsv[0] = h / 360.0;
            hsv[1] = s;
            hsv[2] = v / 255.0;
            return hsv;
        }

        /// <summary>
        /// Create a color from the specified HSV.
        /// </summary>
        /// <param name="hsv">
        /// The HSV.
        /// </param>
        /// <returns>
        /// A color.
        /// </returns>
        public Color HsvToColor(double[] hsv)
        {
            if (hsv.Length != 3)
            {
                throw new InvalidOperationException("Wrong length of hsv array.");
            }
            return HsvToColor(hsv[0], hsv[1], hsv[2]);
        }
        /// <summary>
        /// Convert from HSV to <see cref="Color"/>
        /// http://en.wikipedia.org/wiki/HSL_color_space
        /// </summary>
        /// <param name="hue">
        /// Hue [0,1]
        /// </param>
        /// <param name="sat">
        /// Saturation [0,1]
        /// </param>
        /// <param name="val">
        /// Value [0,1]
        /// </param>
        /// <returns>
        /// </returns>
        public Color HsvToColor(double hue, double sat, double val)
        {
            int i;
            double aa, bb, cc, f;
            double r, g, b;
            r = g = b = 0;
            if (sat == 0)
            {
                // Gray scale
                r = g = b = val;
            }
            else
            {
                if (hue == 1.0)
                {
                    hue = 0;
                }
                hue *= 6.0;
                i = (int)Math.Floor(hue);
                f = hue - i;
                aa = val * (1 - sat);
                bb = val * (1 - (sat * f));
                cc = val * (1 - (sat * (1 - f)));
                switch (i)
                {
                    case 0:
                        r = val;
                        g = cc;
                        b = aa;
                        break;
                    case 1:
                        r = bb;
                        g = val;
                        b = aa;
                        break;
                    case 2:
                        r = aa;
                        g = val;
                        b = cc;
                        break;
                    case 3:
                        r = aa;
                        g = bb;
                        b = val;
                        break;
                    case 4:
                        r = cc;
                        g = aa;
                        b = val;
                        break;
                    case 5:
                        r = val;
                        g = aa;
                        b = bb;
                        break;
                }
            }
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
        /// <summary>
        /// The export material.
        /// </summary>
        /// <param name="materialWriter">The material writer.</param>
        /// <param name="matName">The mat name.</param>
        /// <param name="material">The material.</param>
        /// <param name="backMaterial">The back material.</param>
        private void ExportMaterial(StreamWriter materialWriter, string matName, Material material, Material backMaterial)
        {
            /*materialWriter.WriteLine("newmtl {0}", matName);
            var dm = material as DiffuseMaterial;
            var sm = material as SpecularMaterial;
            var mg = material as MaterialGroup;
            if (mg != null)
            {
                foreach (var m in mg.Children)
                {
                    if (m is DiffuseMaterial)
                    {
                        dm = m as DiffuseMaterial;
                    }
                    if (m is SpecularMaterial)
                    {
                        sm = m as SpecularMaterial;
                    }
                }
            }
            if (dm != null)
            {
                var hsv = ColorToHsv(dm.AmbientColor);
                hsv[2] *= 0.2;
                if (hsv[2] > 1.0)
                {
                    hsv[2] = 1.0;
                }
                var adjustedAmbientColor = HsvToColor(hsv);


                //var adjustedAmbientColor = this.ChangeIntensity(dm.AmbientColor, 0.2);
                // materialWriter.WriteLine(string.Format("Ka {0}", this.ToColorString(adjustedAmbientColor)));
                var scb = dm.Brush as SolidColorBrush;
                if (scb != null)
                {
                    materialWriter.WriteLine(string.Format("Kd {0}", this.ToColorString(scb.Color)));
                    if (this.UseDissolveForTransparency)
                    {
                        // Dissolve factor
                        materialWriter.WriteLine(
                        string.Format(CultureInfo.InvariantCulture, "d {0:F4}", scb.Color.A / 255.0));
                    }
                    else
                    {
                        // Transparency
                        materialWriter.WriteLine(
                        string.Format(CultureInfo.InvariantCulture, "Tr {0:F4}", scb.Color.A / 255.0));
                    }
                }
                else
                {
                    var textureFilename = matName + this.TextureExtension;
                    var texturePath = Path.Combine(this.TextureFolder, textureFilename);
                    // create .png bitmap file for the brush
                    this.RenderBrush(texturePath, dm.Brush, this.TextureSize, this.TextureSize, this.TextureQualityLevel);
                    materialWriter.WriteLine(string.Format("map_Kd {0}", textureFilename));
                }
            }
            // Illumination model 1
            // This is a diffuse illumination model using Lambertian shading. The
            // color includes an ambient constant term and a diffuse shading term for
            // each light source. The formula is
            // color = KaIa + Kd { SUM j=1..ls, (N * Lj)Ij }
            int illum = 1; // Lambertian
            if (sm != null)
            {
                var scb = sm.Brush as SolidColorBrush;
                materialWriter.WriteLine(
                string.Format(
                "Ks {0}", this.ToColorString(scb != null ? scb.Color : Color.FromScRgb(1.0f, 0.2f, 0.2f, 0.2f))));
                // Illumination model 2
                // This is a diffuse and specular illumination model using Lambertian
                // shading and Blinn's interpretation of Phong's specular illumination
                // model (BLIN77). The color includes an ambient constant term, and a
                // diffuse and specular shading term for each light source. The formula
                // is: color = KaIa + Kd { SUM j=1..ls, (N*Lj)Ij } + Ks { SUM j=1..ls, ((H*Hj)^Ns)Ij }
                illum = 2;
                // Specifies the specular exponent for the current material. This defines the focus of the specular highlight.
                // "exponent" is the value for the specular exponent. A high exponent results in a tight, concentrated highlight. Ns values normally range from 0 to 1000.
                materialWriter.WriteLine(string.Format(CultureInfo.InvariantCulture, "Ns {0:F4}", sm.SpecularPower));
            }
            // roughness
            materialWriter.WriteLine(string.Format("Ns {0}", 2));
            // Optical density (index of refraction)
            materialWriter.WriteLine(string.Format("Ni {0}", 1));
            // Transmission filter
            materialWriter.WriteLine(string.Format("Tf {0} {1} {2}", 1, 1, 1));
            // Illumination model
            // Illumination Properties that are turned on in the
            // model Property Editor
            // 0 Color on and Ambient off
            // 1 Color on and Ambient on
            // 2 Highlight on
            // 3 Reflection on and Ray trace on
            // 4 Transparency: Glass on
            // Reflection: Ray trace on
            // 5 Reflection: Fresnel on and Ray trace on
            // 6 Transparency: Refraction on
            // Reflection: Fresnel off and Ray trace on
            // 7 Transparency: Refraction on
            // Reflection: Fresnel on and Ray trace on
            // 8 Reflection on and Ray trace off
            // 9 Transparency: Glass on
            // Reflection: Ray trace off
            // 10 Casts shadows onto invisible surfaces
            materialWriter.WriteLine("illum {0}", illum);*/
        }
        /// <summary>
        /// Converts a color to a string.
        /// </summary>
        /// <param name="color">
        /// The color.
        /// </param>
        /// <returns>
        /// The string.
        /// </returns>
        private string ToColorString(Color color)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F4} {1:F4} {2:F4}", color.R / 255.0, color.G / 255.0, color.B / 255.0);
        }
        /// <summary>
        /// Represents the stream writers for the <see cref="ObjExporter"/>.
        /// </summary>
        public class ObjWriters
        {
            /// <summary>
            /// Gets or sets the object file writer.
            /// </summary>
            public StreamWriter ObjWriter { get; set; }
            /// <summary>
            /// Gets or sets the material file writer.
            /// </summary>
            public StreamWriter MaterialsWriter { get; set; }
        }
    }
}