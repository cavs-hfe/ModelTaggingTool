using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelViewer.Exporter
{
    public class OgreMaterialExporter
    {
        private List<Material> materials = new List<Material>();

        public OgreMaterialExporter()
        {
            materials = new List<Material>();
        }

        public void Export(string path)
        {
            StreamReader reader = new StreamReader(path);
            StreamWriter writer = new StreamWriter(new FileStream(Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".material"), FileMode.Create));

            Material currentMat = new Material("");

            string temp = "";
            while ((temp = reader.ReadLine()) != null)
            {
                if (temp.Trim().StartsWith("newmtl"))
                {
                    if (!currentMat.Name.Equals(""))
                    {
                        materials.Add(currentMat);
                    }
                    currentMat = new Material(temp.Substring(temp.IndexOf("newmtl") + 7));
                }
                else if (temp.Trim().StartsWith("Ka"))
                {
                    currentMat.Ambient = temp.Substring(temp.IndexOf("Ka") + 3);
                }
                else if (temp.Trim().StartsWith("Kd"))
                {
                    currentMat.Diffuse = temp.Substring(temp.IndexOf("Kd") + 3);
                }
                else if (temp.Trim().StartsWith("Ks"))
                {
                    currentMat.Specular = temp.Substring(temp.IndexOf("Ks") + 3);
                }
                else if (temp.Trim().StartsWith("d"))
                {
                    currentMat.Dissolve = Convert.ToDouble(temp.Substring(temp.IndexOf("Kd") + 3));
                }
                else if (temp.Trim().StartsWith("map_Ka"))
                {
                    currentMat.Texture = temp.Substring(temp.IndexOf("map_Ka") + 7);
                }
            }

            if (!currentMat.Name.Equals(""))
            {
                materials.Add(currentMat);
            }

            foreach (Material m in materials)
            {
                writer.WriteLine("material " + Path.GetFileNameWithoutExtension(path) + "/" + m.Name);
                writer.WriteLine("{");
                writer.WriteLine("\ttechnique");
                writer.WriteLine("\t{");
                writer.WriteLine("\t\tpass");
                writer.WriteLine("\t\t{");
                if (m.Ambient != null)
                {
                    writer.WriteLine("\t\t\tambient " + m.Ambient + " 1");
                }
                if (m.Diffuse != null)
                {
                    writer.WriteLine("\t\t\tdiffuse " + m.Diffuse + " 1");
                }
                if (m.Specular != null)
                {
                    writer.WriteLine("\t\t\tspecular " + m.Specular + " 2");
                }
                if (m.Texture != null)
                {
                    writer.WriteLine("\t\t\ttexture_unit");
                    writer.WriteLine("\t\t\t{");
                    writer.WriteLine("\t\t\t\ttexture \"" + m.Texture + "\"");
                    writer.WriteLine("\t\t\t}");
                }
                if (m.Dissolve != 1.0)
                {
                    writer.WriteLine("\t\t\tscene_blend alpha_blend");
                    writer.WriteLine("\t\t\tdepth_write off");
                    writer.WriteLine("\t\t\ttexture_unit");
                    writer.WriteLine("\t\t\t{");
                    writer.WriteLine("\t\t\t\talpha_op_ex source1 src_manual src_current 0.0");
                    writer.WriteLine("\t\t\t}");
                }
                writer.WriteLine("\t\t}");
                writer.WriteLine("\t}");
                writer.WriteLine("}");
                writer.WriteLine("");
            }

            writer.Flush();

            reader.Close();
            writer.Close();
        }
    }

    class Material
    {
        public string Name { get; set; }
        public string Ambient { get; set; }
        public string Diffuse { get; set; }
        public string Specular { get; set; }
        public string Texture { get; set; }
        public double Dissolve { get; set; }


        public Material(string name)
        {
            this.Name = name;
            Ambient = null;
            Diffuse = null;
            Specular = null;
            Texture = null;
            Dissolve = 1.0;
        }


    }
}
