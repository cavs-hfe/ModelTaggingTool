using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelViewer.Exporter
{
    public class VaneExporter
    {
        public static List<string> GetVANEObjects(string vaneFilePath)
        {
            //read in all lines of VANE file and get obj names
            string[] lines = File.ReadAllLines(vaneFilePath);

            List<string> files = new List<string>();

            for (int i = 3; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split(' ');
                if (!files.Contains(parts[0]) && parts.Length > 1)
                {
                    files.Add(parts[0]);
                }
            }

            return files;
        }


        public static void CopyVANEObjects(string outputDirectory, string modelDirectory, List<string> files)
        {
            foreach (string file in files)
            {
                if (Directory.Exists(Path.Combine(modelDirectory, Path.GetFileNameWithoutExtension(file))))
                {
                    File.Copy(Path.Combine(modelDirectory, Path.GetFileNameWithoutExtension(file), file), Path.Combine(outputDirectory, file));
                    CopyMtlFiles(Path.Combine(modelDirectory, Path.GetFileNameWithoutExtension(file), file), outputDirectory);
                }
            }
        }

        private static void CopyMtlFiles(string objFile, string outputDirectory)
        {
            string[] lines = File.ReadAllLines(objFile);
            foreach (string line in lines)
            {
                if (line.Trim().StartsWith("mtllib"))
                {
                    string mtl = line.Substring(line.IndexOf(' ')).Trim();
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(objFile), mtl)))
                    {
                        File.Copy(Path.Combine(Path.GetDirectoryName(objFile), mtl), Path.Combine(outputDirectory, mtl));
                        CopyAssets(Path.Combine(Path.GetDirectoryName(objFile), mtl), outputDirectory);
                    }
                }
            }
        }

        private static void CopyAssets(string mtlFile, string outputDirectory)
        {
            string[] lines = File.ReadAllLines(mtlFile);
            foreach (string line in lines)
            {
                if (line.Trim().StartsWith("map"))
                {
                    string[] parts = line.Split(' ');
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(mtlFile), parts[parts.Length - 1])) && !File.Exists(Path.Combine(outputDirectory, parts[parts.Length - 1])))
                    {
                        File.Copy(Path.Combine(Path.GetDirectoryName(mtlFile), parts[parts.Length - 1]), Path.Combine(outputDirectory, parts[parts.Length - 1]));
                    }
                }
            }
        }
    }
}
