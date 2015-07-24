using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Xml;

namespace ModelViewer.Exporter
{
    public class OgreMeshExporter
    {

        public OgreMeshExporter() { }

        public bool ParseAndConvertFileToXml(string path, string outputFileName)
        {

            //Do a pre conversion pass to count all of the objects
            int numFaces = 0;

            //remove const here on purpose, for optimization of strtod
            //char* pData = ( char*)  pRawData;
            //const char* pData = ( const char*)  pRawData;
            //const char* pEnd = pData + inputSize;

            string objectName = Path.GetFileNameWithoutExtension(path);

            string folder = Path.GetDirectoryName(path);
            string outputFolder = Path.GetDirectoryName(outputFileName);

            //Loop through the entire file and count the num of verts, normals, and text coords
            /*while (pData < pEnd)	
            {
                if ( memcmp(pData, "vn", 2) == 0) ++numNormals;      
                else if ( memcmp(pData, "vt", 2) == 0 ) ++numTexCoords;      
                else if ( memcmp(pData, "v",  1) == 0 ) ++numVerts; 
		
                //Skip the rest of the line until we get a new line
                while ( *pData++ != (Ogre::uint8) 0x0A && pData < pEnd );   
            }*/

            //allocate our large chunks of verts and normals
            List<Vector3D> pVerts = new List<Vector3D>();
            List<Vector3D> pNormals = new List<Vector3D>();
            List<Vector> pTexCoords = new List<Vector>();

            //Write our shared geometry to the output file

            List<SubMeshData> subMeshData = new List<SubMeshData>();

            int foundNormals = 0;
            int foundVerts = 0;
            int foundTexCoords = 0;
            int numFaceVertexDefs = 0;

            string materialPrefix = Path.GetFileNameWithoutExtension(path);

            string currentMaterial = "";
            SubMeshData pCurrentSubMesh = null;

            //loop through again, this time, filling out data
            using (StreamReader reader = new StreamReader(path))
            {
                string temp = "";

                while ((temp = reader.ReadLine()) != null)
                {
                    if (temp.Trim().StartsWith("vn"))
                    {
                        string[] split = temp.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                        //sscanf(pData, "vn %f %f %f", &pNormals[foundNormals].x,  &pNormals[foundNormals].y, &pNormals[foundNormals].z );
                        pNormals.Add(new Vector3D(Convert.ToDouble(split[1]), Convert.ToDouble(split[2]), Convert.ToDouble(split[3])));
                        ++foundNormals;


                    }
                    else if (temp.Trim().StartsWith("vt"))
                    {
                        string[] split = temp.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                        //sscanf(pData, "vt %f %f", &pTexCoords[foundTexCoords].x,   &pTexCoords[foundTexCoords].y);
                        pTexCoords.Add(new Vector(Convert.ToDouble(split[1]), 1.0 - Convert.ToDouble(split[2])));
                        ++foundTexCoords;

                    }
                    else if (temp.Trim().StartsWith("v"))
                    {
                        string[] split = temp.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                        //sscanf(pData, "v %f %f %f", &vert.x,  &vert.y, &vert.z );
                        pVerts.Add(new Vector3D(Convert.ToDouble(split[1]), Convert.ToDouble(split[2]), Convert.ToDouble(split[3])));

                        ++foundVerts;

                    }
                    else if (temp.Trim().StartsWith("g"))
                    {
                        SubMeshData subMesh = new SubMeshData();

                        subMesh.name = temp.Substring(temp.IndexOf("g") + 1).Trim();
                        subMesh.material = currentMaterial;
                        subMeshData.Add(subMesh);

                        pCurrentSubMesh = subMesh;
                    }
                    else if (temp.Trim().StartsWith("usemtl"))
                    {

                        String newCurrentMaterial = materialPrefix + "/" + temp.Substring(temp.IndexOf("usemtl") + 6).Trim();
                        //newCurrentMaterial = VaneOgre::RemoveInvalidMaterialCharacters( newCurrentMaterial );

                        if (newCurrentMaterial != currentMaterial)
                        {

                            currentMaterial = newCurrentMaterial;

                            //if the current submesh has no faces defined, we can set the current
                            //material on that and be done. Otherwise, we need a new submesh, since
                            //Ogre submeshes can only have one material.
                            if (pCurrentSubMesh != null && pCurrentSubMesh.faceData.Count() == 0)
                            {
                                pCurrentSubMesh.material = currentMaterial;
                            }
                            else
                            {
                                //we need to start a new submesh
                                SubMeshData subMesh = new SubMeshData();
                                subMesh.name = "default";
                                subMesh.material = currentMaterial;
                                subMeshData.Add(subMesh);
                                pCurrentSubMesh = subMesh;
                            }
                        }
                    }
                    else if (temp.Trim().StartsWith("f"))
                    {

                        String line = temp.Substring(temp.IndexOf("f") + 1).Trim();

                        string[] entries = line.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

                        int faceEntryCount = entries.Count();

                        List<VertIndexData> lineFaceData = new List<VertIndexData>(faceEntryCount);

                        string[] vertComponents;
                        for (int i = 0; i < faceEntryCount; ++i)
                        {
                            VertIndexData vert = new VertIndexData();
                            vertComponents = entries[i].Split('/');
                            if (vertComponents.Count() == 3)
                            {
                                vert.vert = Convert.ToInt32(vertComponents[0]);
                                vert.texCoord = vertComponents[1].Count() == 0 ? 0 : Convert.ToInt32(vertComponents[1]);
                                vert.normal = vertComponents[2].Count() == 0 ? 0 : Convert.ToInt32(vertComponents[2]);
                            }
                            else if (vertComponents.Count() == 2)
                            {
                                vert.vert = Convert.ToInt32(vertComponents[0]);
                                vert.texCoord = vertComponents[1].Count() == 0 ? 0 : Convert.ToInt32(vertComponents[1]);
                            }
                            else if (vertComponents.Count() == 1)
                            {
                                vert.vert = Convert.ToInt32(vertComponents[0]);
                            }
                            lineFaceData.Add(vert);
                        }

                        if (pCurrentSubMesh == null)
                        {
                            SubMeshData subMesh = new SubMeshData();

                            subMesh.name = "default";
                            subMesh.material = currentMaterial;
                            subMeshData.Add(subMesh);
                            pCurrentSubMesh = subMesh;
                        }

                        if (faceEntryCount == 4)
                        {
                            //split this into groups
                            numFaces += 2;
                            numFaceVertexDefs += 6;

                            //split face into 2 tris

                            //1st tri
                            pCurrentSubMesh.faceData.Add(lineFaceData[0]);
                            pCurrentSubMesh.faceData.Add(lineFaceData[1]);
                            pCurrentSubMesh.faceData.Add(lineFaceData[2]);
                            //2nd tri
                            pCurrentSubMesh.faceData.Add(lineFaceData[0]);
                            pCurrentSubMesh.faceData.Add(lineFaceData[2]);
                            pCurrentSubMesh.faceData.Add(lineFaceData[3]);
                        }
                        else if (faceEntryCount == 3)
                        {
                            //we are using tris.
                            numFaces++;
                            numFaceVertexDefs += 3;

                            pCurrentSubMesh.faceData.Add(lineFaceData[0]);
                            pCurrentSubMesh.faceData.Add(lineFaceData[1]);
                            pCurrentSubMesh.faceData.Add(lineFaceData[2]);
                        }
                    }
                    else if (temp.Trim().StartsWith("mtllib"))
                    {
                        string mtlFile = Path.Combine(folder, temp.Substring(temp.IndexOf("mtllib") + 6).Trim());
                        OgreMaterialExporter ome = new OgreMaterialExporter();
                        ome.Export(mtlFile);
                    }
                }
            }


            int numUsedVerts = 0;
            for (int i = 0; i < subMeshData.Count(); ++i)
            {
                numUsedVerts += subMeshData[i].faceData.Count();
            }

            int numAddedFaceVerts = 0;
            for (int i = 0; i < subMeshData.Count(); ++i)
            {
                SubMeshData subMesh = subMeshData[i];
                if (subMesh.faceData.Count() == 0)
                    continue;

                int initialVertexNum = numAddedFaceVerts;
                int subMeshFaceCount = subMesh.faceData.Count();

                if (pNormals != null)
                {
                    //Do positions and normals
                    for (int j = 0; j < subMeshFaceCount; ++j)
                    {
                        VertIndexData vertIndexData = subMeshData[i].faceData[j];

                        ++numAddedFaceVerts;
                    }
                }
                else
                {
                    for (int j = 0; j < subMeshFaceCount; ++j)
                    {
                        VertIndexData vertIndexData = subMeshData[i].faceData[j];

                        ++numAddedFaceVerts;
                    }
                }
            }

            if (pTexCoords != null)
            {

                numAddedFaceVerts = 0;
                for (int i = 0; i < subMeshData.Count(); ++i)
                {
                    SubMeshData subMesh = subMeshData[i];
                    if (subMesh.faceData.Count() == 0)
                        continue;

                    int initialVertexNum = numAddedFaceVerts;
                    int subMeshFaceCount = subMesh.faceData.Count();

                    //Do positions and normals
                    for (int j = 0; j < subMeshFaceCount; ++j)
                    {
                        VertIndexData vertIndexData = subMeshData[i].faceData[j];
                        //VANEAssert( vertIndexData.texCoord - 1 < numTexCoords && vertIndexData.texCoord > 0 );

                        ++numAddedFaceVerts;
                    }
                }
            }

            int startingIndex = 0;

            //Update index buffer
            for (int i = 0; i < subMeshData.Count(); ++i)
            {
                SubMeshData subMesh = subMeshData[i];
                if (subMesh.faceData.Count() == 0)
                    continue;


                int subMeshFaceCount = subMesh.faceData.Count();

                int[] pIndexBuffer = new int[subMeshFaceCount];

                for (int j = 0; j < subMesh.faceData.Count(); j += 3)
                {
                    //Since we have reordered the verts and normals to match the index
                    //buffer, the index buffer is now simply an incrementing count.
                    //Ogre vertex indices are 0 based, not 1 based.
                    pIndexBuffer[j] = startingIndex + j;
                    pIndexBuffer[j + 1] = startingIndex + j + 1;
                    pIndexBuffer[j + 2] = startingIndex + j + 2;
                }

                startingIndex += subMeshFaceCount;
            }

            using (StreamWriter writer = new StreamWriter(new FileStream(outputFileName, FileMode.Create)))
            {

                string kXmlVersionString = "<?xml version=\"1.0\" ?>\n";

                //FileSystem::WriteString( hOutput, kXmlVersionString );
                writer.WriteLine(kXmlVersionString);
                //FileSystem::WriteString( hOutput, "<mesh>");
                writer.WriteLine("<mesh>");

                numUsedVerts = 0;
                for (int i = 0; i < subMeshData.Count(); ++i)
                {
                    numUsedVerts += subMeshData[i].faceData.Count();
                }

                String shrGeom = "\n<sharedgeometry vertexcount=\"" + numUsedVerts + "\">";

                //FileSystem::WriteString( hOutput, shrGeom );
                writer.WriteLine(shrGeom);

                String vertBuf = "\n<vertexbuffer positions=\"true\"";
                if (pNormals != null)
                {
                    vertBuf += "normals=\"true\"";
                }
                else
                {
                    vertBuf += "normals=\"false\"";
                }
                vertBuf += ">";

                //FileSystem::WriteString( hOutput, vertBuf );
                writer.WriteLine(vertBuf);

                numAddedFaceVerts = 0;
                //char vertDef = new char[256] { 0 };
                string vertDef = "";

                List<int> initialCounts = new List<int>();

                for (int i = 0; i < subMeshData.Count(); ++i)
                {
                    SubMeshData subMesh = subMeshData[i];

                    initialCounts.Add(numAddedFaceVerts);

                    if (subMesh.faceData.Count() == 0)
                        continue;

                    int subMeshFaceCount = subMesh.faceData.Count();

                    //Do positions and normals
                    for (int j = 0; j < subMeshFaceCount; ++j)
                    {
                        VertIndexData vertIndexData = subMeshData[i].faceData[j];
                        //VANEAssert( vertIndexData.vert - 1 < numVerts && vertIndexData.vert > 0 );
                        Vector3D vec = pVerts[vertIndexData.vert - 1];
                        //sprintf( vertDef, "x=\"%lf\" y=\"%lf\" z=\"%lf\"/>", vec.x, vec.y, vec.z );
                        vertDef = String.Format("x=\"{0}\" y=\"{1}\" z=\"{2}\"/>", vec.X, vec.Y, vec.Z);

                        String vert = "\n<vertex>\n<position ";
                        vert += vertDef;

                        if (pNormals != null)
                        {
                            Vector3D norm = pNormals[vertIndexData.normal - 1];
                            //sprintf( vertDef, "x=\"%lf\" y=\"%lf\" z=\"%lf\"/>", norm.x, norm.y, norm.z );
                            vertDef = String.Format("x=\"{0}\" y=\"{1}\" z=\"{2}\"/>", norm.X, norm.Y, norm.Z);

                            vert += "\n<normal ";
                            vert += vertDef;
                        }

                        vert += "\n</vertex>";

                        //FileSystem::WriteString( hOutput, vert );
                        writer.WriteLine(vert);

                        ++numAddedFaceVerts;
                    }
                }

                //FileSystem::WriteString(hOutput, "\n</vertexbuffer>");
                writer.WriteLine("\n</vertexbuffer>");

                //Do tex Coords ( if we have them )

                if (pTexCoords != null)
                {
                    //FileSystem::WriteString(hOutput, "\n<vertexbuffer texture_coord_dimensions_0=\"2\" texture_coords=\"1\">");
                    writer.WriteLine("\n<vertexbuffer texture_coord_dimensions_0=\"2\" texture_coords=\"1\">");

                    for (int i = 0; i < subMeshData.Count(); ++i)
                    {
                        SubMeshData subMesh = subMeshData[i];

                        if (subMesh.faceData.Count() == 0)
                            continue;

                        int subMeshFaceCount = subMesh.faceData.Count();

                        //Do positions and normals
                        for (int j = 0; j < subMeshFaceCount; ++j)
                        {
                            VertIndexData vertIndexData = subMeshData[i].faceData[j];

                            //need to remap these, ARGH!
                            //VANEAssert( vertIndexData.texCoord - 1 < numTexCoords && vertIndexData.texCoord > 0 );
                            Vector vec = pTexCoords[vertIndexData.texCoord - 1];
                            //sprintf( vertDef, "u=\"%lf\" v=\"%lf\" />", vec.x, vec.y );
                            vertDef = String.Format("u=\"{0}\" v=\"{1}\" />", vec.X, vec.Y);

                            String vert = "\n<vertex>\n<texcoord ";
                            vert += vertDef;
                            vert += "\n</vertex>";

                            //FileSystem::WriteString( hOutput, vert );
                            writer.WriteLine(vert);
                        }
                    }

                    //FileSystem::WriteString(hOutput, "\n</vertexbuffer>");
                    writer.WriteLine("\n</vertexbuffer>");
                }

                //FileSystem::WriteString(hOutput, "\n</sharedgeometry>\n<submeshes>");
                writer.WriteLine("\n</sharedgeometry>\n<submeshes>");

                //start face data

                for (int i = 0; i < subMeshData.Count(); ++i)
                {
                    SubMeshData subMesh = subMeshData[i];

                    if (subMesh.faceData.Count() == 0)
                        continue;

                    int subMeshFaceCount = subMesh.faceData.Count();

                    int initialVertexNum = initialCounts[i];

                    //FileSystem::WriteString(hOutput, "\n<submesh material=\"" + subMesh.material + "\" usedsharedvertices=\"true\" use32bitindexes=\"true\" operationtype=\"triangle_list\">" );
                    writer.WriteLine("\n<submesh material=\"" + subMesh.material + "\" usedsharedvertices=\"true\" use32bitindexes=\"true\" operationtype=\"triangle_list\">");
                    //FileSystem::WriteString(hOutput, "\n<faces count=\"" + StringConverter::ToString( subMesh.faceData.size() / 3) + "\">" );
                    writer.WriteLine("\n<faces count=\"" + (subMesh.faceData.Count() / 3) + "\">");
                    //Do positions and normals
                    for (int j = 0; j < subMeshFaceCount; j += 3)
                    {
                        VertIndexData vertIndexData = subMeshData[i].faceData[j];

                        //sprintf( vertDef, "v1=\"%d\" v2=\"%d\" v3=\"%d\" />", initialVertexNum + j, initialVertexNum + j + 1, initialVertexNum + j + 2 );
                        vertDef = String.Format("v1=\"{0}\" v2=\"{1}\" v3=\"{2}\" />", initialVertexNum + j, initialVertexNum + j + 1, initialVertexNum + j + 2);

                        String vert = "\n<face ";
                        vert += vertDef;

                        //FileSystem::WriteString(hOutput, vert );
                        writer.WriteLine(vert);
                    }
                    //FileSystem::WriteString(hOutput, "\n</faces>" );
                    writer.WriteLine("\n</faces>");
                    //FileSystem::WriteString(hOutput, "\n</submesh>" );
                    writer.WriteLine("\n</submesh>");

                }

                //FileSystem::WriteString(hOutput, "\n</submeshes>" );
                writer.WriteLine("\n</submeshes>");
                //FileSystem::WriteString(hOutput, "\n</mesh>" );
                writer.WriteLine("\n</mesh>");

                //FileSystem::Close( hOutput );
            }

            Console.WriteLine("OBJ converted successfully to " + outputFileName);

            ConvertXmlToMesh(outputFileName);

            return true;
        }

        private bool ConvertXmlToMesh(string xmlFile)
        {
            Process proc = new Process();
            proc.StartInfo.FileName = "OgreXMLConverter.exe";
            proc.StartInfo.Arguments = xmlFile;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;

            try
            {
                proc.Start();

                proc.WaitForExit();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error converting xml to mesh: \n" + e.StackTrace);
            }

            if (proc.ExitCode == 0)
            {
                return true;
            }
            else
            {
                Console.WriteLine("Error converting file from XML to mesh.");
                return false;
            }

        }
    }

    public class VertIndexData
    {
        public int vert;
        public int normal;
        public int texCoord;

        public VertIndexData()
            : this(0, 0, 0)
        { }

        public VertIndexData(int vert, int normal, int texCoord)
        {
            this.vert = vert;
            this.normal = normal;
            this.texCoord = texCoord;
        }

        public override bool Equals(object obj)
        {
            var rhs = obj as VertIndexData;

            if (rhs != null)
            {
                return (this.vert == rhs.vert && this.normal == rhs.normal && this.texCoord == rhs.texCoord);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.vert.GetHashCode() + this.normal.GetHashCode() + this.texCoord.GetHashCode();
        }

    }

    public class SubMeshData
    {
        public SubMeshData()
        {
            faceData = new List<VertIndexData>();
            combinedIndexFaceData = new List<int>();
        }

        public String name { get; set; }
        public String material { get; set; }
        public List<VertIndexData> faceData { get; set; }
        public List<int> combinedIndexFaceData { get; set; }
    }

    public class FaceData
    {
        public FaceData() { }

        public int v1 { get; set; }
        public int v2 { get; set; }
        public int v3 { get; set; }
    }
}
