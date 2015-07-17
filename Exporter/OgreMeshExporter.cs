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

        public async void ParseAndConvertFileToMesh(string path)
        {
            //Do a pre conversion pass to count all of the objects
            int numFaces = 0;

            StreamReader reader = new StreamReader(path);

            string objectName = Path.GetFileNameWithoutExtension(path);

            string folder = Path.GetDirectoryName(path);

            //Loop through the entire file and count the num of verts, normals, and text coords
            string temp = "";


            //allocate our large chunks of verts and normals
            List<Point3D> pVerts = new List<Point3D>();
            List<Point3D> pNormals = new List<Point3D>();
            List<Point> pTexCoords = new List<Point>();

            //Write our shared geometry to the output file
            List<SubMeshData> subMeshData = new List<SubMeshData>();

            int numFaceVertexDefs = 0;

            string materialPrefix = Path.GetFileNameWithoutExtension(path);

            string currentMaterial = "";
            SubMeshData pCurrentSubMesh = null;

            //loop through again, this time, filling out data
            while ((temp = reader.ReadLine()) != null)
            {
                if (temp.Trim().StartsWith("vn"))
                {
                    string[] split = temp.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                    pNormals.Add(new Point3D(Convert.ToDouble(split[1].Trim()), Convert.ToDouble(split[2].Trim()), Convert.ToDouble(split[3].Trim())));
                }
                else if (temp.Trim().StartsWith("vt"))
                {
                    string[] split = temp.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                    pTexCoords.Add(new Point(Convert.ToDouble(split[1].Trim()), 1.0 - Convert.ToDouble(split[2].Trim())));
                    //MOST IMPORTANT!
                    //pTexCoords[foundTexCoords].x = tempVec2.x;
                    //pTexCoords[foundTexCoords].y = 1.0f - tempVec2.y;
                }
                else if (temp.Trim().StartsWith("v"))
                {
                    string[] split = temp.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                    pVerts.Add(new Point3D(Convert.ToDouble(split[1].Trim()), Convert.ToDouble(split[2].Trim()), Convert.ToDouble(split[3].Trim())));
                }
                else if (temp.Trim().StartsWith("f"))
                {
                    string line = temp.Substring(temp.IndexOf("f") + 1).Trim();
                    string[] entries = line.Split(' ');



                    int faceEntryCount = entries.Count();

                    List<VertIndexData> lineFaceData = new List<VertIndexData>(faceEntryCount);

                    string[] vertComponents;
                    for (int i = 0; i < faceEntryCount; ++i)
                    {
                        VertIndexData vert = new VertIndexData();
                        vert.normal = 0;
                        vert.texCoord = 0;
                        vertComponents = entries[i].Split('/');
                        if (vertComponents.Count() == 3)
                        {
                            vert.vert = Convert.ToInt32(vertComponents[0]);
                            vert.texCoord = vertComponents[1].Trim().Equals("") ? 0 : Convert.ToInt32(vertComponents[1]);
                            vert.normal = vertComponents[2].Trim().Equals("") ? 0 : Convert.ToInt32(vertComponents[2]);
                        }
                        else if (vertComponents.Count() == 2)
                        {
                            vert.vert = Convert.ToInt32(vertComponents[0]);
                            vert.texCoord = vertComponents[1].Trim().Equals("") ? 0 : Convert.ToInt32(vertComponents[1]);
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

                        subMesh.name = "default" + subMeshData.Count();
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
                        ++numFaces;
                        numFaceVertexDefs += 3;

                        pCurrentSubMesh.faceData.Add(lineFaceData[0]);
                        pCurrentSubMesh.faceData.Add(lineFaceData[1]);
                        pCurrentSubMesh.faceData.Add(lineFaceData[2]);
                    }
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
                            subMesh.name = "default" + subMeshData.Count();
                            subMesh.material = currentMaterial;
                            subMeshData.Add(subMesh);
                            pCurrentSubMesh = subMesh;
                        }
                    }
                }
                else if (temp.Trim().StartsWith("mtllib"))
                {
                    //ignore material files for now
                    /*const char* end = strchr( pData + 6, (char) 0x0A );
                    if ( end == null )
                        break;

                    String line( pData+6, end-(pData+6) );
                    StringUtil::Trim( line );
                    StringVector files = StringUtil::Split( line, " " );

                    if ( files.size() > 1 )
                    {
                        Console.WriteLine( "Warning: Automated mesh conversion will use only the first file in the mtllib defintion line. This may result in missing materials."  );
                    }

                    if (files.empty() )
                    {
                        Console.WriteLine( "Error: No files found in mtllib definition line."  );
                    }
                    else
                    {	
                        String fullMaterialPath = folder + "/" + files[0];
			
                        //we have a file, so convert it and set our materialPrefix name to be the 
                        //name of the material file without the extension.
                        String newMaterial = VaneOgre::VaneOgreRenderer::GetSingleton().GetMeshConverter().ConvertMtlToOgreMaterial( fullMaterialPath );
                        materialPrefix = FileSystem::StripFileExtension( newMaterial );
                    }*/
                }
            }

            List<VertIndexData> combinedFaceVector = new List<VertIndexData>();

            int numUsedVerts = 0;
            for (int i = 0; i < subMeshData.Count(); ++i)
            {
                numUsedVerts += subMeshData[i].faceData.Count();
            }

            // A list of which faces use this vertex
            List<List<int>> vertexFaceList = new List<List<int>>();

            //Make an Ogre Mesh directly.
            //Ogre::MeshPtr newMesh = Ogre::MeshManager::getSingleton().createManual( objectName, "General" );

            int faceIndex = 0;

            //Update index buffer
            for (int i = 0; i < subMeshData.Count(); ++i)
            {
                SubMeshData subMesh = subMeshData[i];
                if (subMesh.faceData.Count() == 0)
                    continue;

                int subMeshFaceCount = subMesh.faceData.Count();

                //Ogre::SubMesh* pSubMesh = newMesh->createSubMesh( subMesh.name );



                combinedFaceVector.Clear();

                for (int j = 0; j < subMeshFaceCount; ++j)
                {
                    int index = 0;
                    if (combinedFaceVector.Count() > 0 && subMesh.faceData[j].Equals(combinedFaceVector.Last()))
                    {
                        index = combinedFaceVector.Count();
                        combinedFaceVector.Add(subMesh.faceData[j]);
                        subMesh.combinedIndexFaceData.Add(index);
                    }
                    else
                    {
                        subMesh.combinedIndexFaceData.Add(combinedFaceVector.IndexOf(subMesh.faceData[j]));
                    }
                }

                //List<FaceData> faceArray = new List<FaceData>();
                FaceData[] faceArray = new FaceData[numFaces];

                int count = combinedFaceVector.Count;
                vertexFaceList.Clear();


                List<Point3D> pVertBuffer = new List<Point3D>(count);
                List<Point3D> pNormalBuffer = new List<Point3D>(count);
                List<Point> pTexCoordBuffer = new List<Point>(count);

                for (int j = 0; j < count; ++j)
                {
                    pVertBuffer[j] = pVerts[combinedFaceVector[j].vert - 1];
                }
                if (pNormals != null)
                {
                    for (int j = 0; j < count; ++j)
                    {
                        pNormalBuffer[j] = pNormals[combinedFaceVector[j].normal - 1];
                    }
                }

                if (pTexCoords != null)
                {
                    for (int j = 0; j < count; ++j)
                    {
                        pTexCoordBuffer[j] = pTexCoords[combinedFaceVector[j].texCoord - 1];
                    }
                }

                //pSubMesh->vertexData = new Ogre::VertexData();
                //pSubMesh->vertexData->vertexCount = count;

                //Ogre::VertexBufferBinding* bind = pSubMesh->vertexData->vertexBufferBinding; 
                /// Create declaration (memory format) of vertex data
                //Ogre::VertexDeclaration* decl = pSubMesh->vertexData->vertexDeclaration;
                //decl->addElement(POSITION_BINDING, 0, Ogre::VET_FLOAT3, Ogre::VES_POSITION);
                //decl->addElement(NORMAL_BINDING, 0, Ogre::VET_FLOAT3, Ogre::VES_NORMAL);

                //if ( pTexCoords )
                //{
                //    decl->addElement(TEXCOORD_BINDING, 0, Ogre::VET_FLOAT2, Ogre::VES_TEXTURE_COORDINATES, 0);
                //}

                // Ogre::HardwareVertexBufferSharedPtr vbuf = Ogre::HardwareBufferManager::getSingleton().createVertexBuffer(decl->getVertexSize(POSITION_BINDING), pSubMesh->vertexData->vertexCount, Ogre::HardwareBuffer::HBU_STATIC_WRITE_ONLY, false);
                // Upload the vertex data to the card
                //vbuf->writeData(0, vbuf->getSizeInBytes(), pVertBuffer, true);
                //bind->setBinding(0, vbuf);

                //Ogre::HardwareVertexBufferSharedPtr nbuf = Ogre::HardwareBufferManager::getSingleton().createVertexBuffer(decl->getVertexSize(NORMAL_BINDING), pSubMesh->vertexData->vertexCount, Ogre::HardwareBuffer::HBU_STATIC_WRITE_ONLY, false);

                //if ( pNormals )
                //{
                //    nbuf->writeData(0, nbuf->getSizeInBytes(), pNormalBuffer, true);
                //    bind->setBinding(NORMAL_BINDING, nbuf);
                //}

                //if ( pTexCoords )
                //{
                //    Ogre::HardwareVertexBufferSharedPtr tbuf = Ogre::HardwareBufferManager::getSingleton().createVertexBuffer(
                //        decl->getVertexSize(TEXCOORD_BINDING), pSubMesh->vertexData->vertexCount, Ogre::HardwareBuffer::HBU_STATIC_WRITE_ONLY );
                //    tbuf->writeData(0, tbuf->getSizeInBytes(), pTexCoordBuffer, true);
                //    bind->setBinding(TEXCOORD_BINDING, tbuf);
                //}

                faceIndex = 0;


                //////////////////////////////////////////////////////////////////////////
                // Do Index buffer. This is done individually, even if we are using shared buffers.

                //int[] pIndexBuffer = new int[subMeshFaceCount];

                //for (int j = 0; j < subMesh.faceData.Count(); j += 3)
                //{
                //pIndexBuffer[j] = subMesh.combinedIndexFaceData[j];
                //pIndexBuffer[j + 1] = subMesh.combinedIndexFaceData[j + 1];
                //pIndexBuffer[j + 2] = subMesh.combinedIndexFaceData[j + 2];

                //faceArray[faceIndex].v1 = subMesh.combinedIndexFaceData[j];
                //faceArray[faceIndex].v2 = subMesh.combinedIndexFaceData[j + 1];
                //faceArray[faceIndex].v3 = subMesh.combinedIndexFaceData[j + 2];

                //vertexFaceList[faceArray[faceIndex].v1].Add(faceIndex);
                //vertexFaceList[faceArray[faceIndex].v2].Add(faceIndex);
                //vertexFaceList[faceArray[faceIndex].v3].Add(faceIndex);

                // ++faceIndex;
                //}

                //Ogre::HardwareIndexBufferSharedPtr ibuf = Ogre::HardwareBufferManager::getSingleton().
                //    createIndexBuffer(
                //    Ogre::HardwareIndexBuffer::IT_32BIT, 
                //    subMeshFaceCount, 
                //    Ogre::HardwareBuffer::HBU_STATIC_WRITE_ONLY);

                //VANEAssert( subMesh.faceData.size() % 3 == 0);
                //ibuf->writeData(0, subMeshFaceCount * sizeof(int), pIndexBuffer, false);			

                //delete[] pIndexBuffer;



                //////////////////////////////////////////////////////////////////////////
                // Manually calculate normals if we do not have them defined

                //if ( !pNormals )
                //{
                //    //Calculate face normals
                //    const int faceCount = faceArray.size();
                //    vector<Ogre::Vector3> faceNormals( faceCount );

                //    for ( int j = 0; j < faceCount; ++j)
                //    {
                //        //TODO: do we need to subtract 1 here?
                //        const FaceData& face = faceArray[j];
                //        Ogre::Vector3 ab = pVertBuffer[face.v2] - pVertBuffer[face.v1];
                //        Ogre::Vector3 ac = pVertBuffer[face.v3] - pVertBuffer[face.v1];

                //        faceNormals[j] = ab.crossProduct(ac);		
                //    }

                //    //compute vert normals
                //    const int vertFaceListCount = vertexFaceList.size();

                //    for ( int j = 0; j < vertFaceListCount; ++j)
                //    {
                //        Ogre::Vector3 normal(0,0,0);
                //        const std::vector<int>& faceList = vertexFaceList[j];
                //        const int listSize = faceList.size();

                //        for ( int k = 0; k < listSize; ++k)
                //        {
                //            normal += faceNormals[ faceList[k] ];
                //        }

                //        normal.normalise();
                //        pNormalBuffer[j] = normal;
                //    }

                //    nbuf->writeData(0, nbuf->getSizeInBytes(), pNormalBuffer, true);
                //    bind->setBinding(NORMAL_BINDING, nbuf);
                //}

                //pSubMesh->useSharedVertices = false;


                //pSubMesh->indexData->indexBuffer = ibuf;
                //pSubMesh->indexData->indexCount = subMeshFaceCount;
                //pSubMesh->indexData->indexStart = 0;
                //pSubMesh->setMaterialName( subMesh.material );

                // Now use Ogre's ability to reorganise the vertex buffers the best way
                //Ogre::VertexDeclaration* newDecl = pSubMesh->vertexData->vertexDeclaration->getAutoOrganisedDeclaration( false, false );
                //Ogre::BufferUsageList bufferUsages;
                //for (size_t u = 0; u <= newDecl->getMaxSource(); ++u )
                //{
                //    bufferUsages.Add(Ogre::HardwareBuffer::HBU_STATIC_WRITE_ONLY);
                //}
                //pSubMesh->vertexData->reorganiseBuffers(newDecl, bufferUsages);


            }

            await GenerateXml(path, pNormals, pVerts, pTexCoords, subMeshData);


            // Set bounds
            //const Ogre::AxisAlignedBox& currBox = newMesh->getBounds();
            //Ogre::Real currRadius = newMesh->getBoundingSphereRadius();
            //if (currBox.isnull())
            //{
            //    newMesh->_setBounds(Ogre::AxisAlignedBox(min, max), false);
            //    newMesh->_setBoundingSphereRadius( Ogre::Math::Sqrt(maxSquaredRadius));
            //}
            //else
            //{
            //    Ogre::AxisAlignedBox newBox(min, max);
            //    newBox.merge(currBox);
            //    newMesh->_setBounds(newBox, false);
            //    newMesh->_setBoundingSphereRadius(std::max( Ogre::Math::Sqrt(maxSquaredRadius), currRadius));
            //}

            //newMesh->load();

            //Ogre::MeshSerializer serializer;
            //serializer.exportMesh( newMesh.get(), outputFileName );
            Console.WriteLine("OBJ converted successfully to ");

            //Ogre::MeshManager::getSingleton().remove( newMesh->getHandle() );


        }

        public async void ParseObjFile(string path)
        {
            using (StreamReader reader = new StreamReader(path))
            {

                List<Point3D> pNormals = new List<Point3D>();
                List<Point3D> pVerts = new List<Point3D>();
                List<Point> pTexCoords = new List<Point>();

                SubMeshData pCurrentSubMesh = null;
                string currentMaterial = "";

                List<SubMeshData> subMeshData = new List<SubMeshData>();

                string materialPrefix = Path.GetFileNameWithoutExtension(path);

                string temp = "";

                while ((temp = reader.ReadLine()) != null)
                {
                    if (temp.Trim().StartsWith("vn"))
                    {
                        string[] split = temp.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                        pNormals.Add(new Point3D(Convert.ToDouble(split[1].Trim()), Convert.ToDouble(split[2].Trim()), Convert.ToDouble(split[3].Trim())));
                    }
                    else if (temp.Trim().StartsWith("vt"))
                    {
                        string[] split = temp.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                        pTexCoords.Add(new Point(Convert.ToDouble(split[1].Trim()), 1.0 - Convert.ToDouble(split[2].Trim())));
                        //MOST IMPORTANT!
                        //pTexCoords[foundTexCoords].x = tempVec2.x;
                        //pTexCoords[foundTexCoords].y = 1.0f - tempVec2.y;
                    }
                    else if (temp.Trim().StartsWith("v"))
                    {
                        string[] split = temp.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);
                        pVerts.Add(new Point3D(Convert.ToDouble(split[1].Trim()), Convert.ToDouble(split[2].Trim()), Convert.ToDouble(split[3].Trim())));

                    }
                    else if (temp.Trim().StartsWith("f"))
                    {
                        string line = temp.Substring(temp.IndexOf("f") + 1).Trim();
                        string[] entries = line.Split(' ');

                        List<VertIndexData> lineFaceData = new List<VertIndexData>();

                        string[] vertComponents;
                        for (int i = 0; i < entries.Count(); ++i)
                        {
                            VertIndexData vert = new VertIndexData();
                            vert.normal = 0;
                            vert.texCoord = 0;
                            vertComponents = entries[i].Split('/');
                            if (vertComponents.Count() == 3)
                            {
                                vert.vert = Convert.ToInt32(vertComponents[0]);
                                vert.texCoord = vertComponents[1].Trim().Equals("") ? 0 : Convert.ToInt32(vertComponents[1]);
                                vert.normal = vertComponents[2].Trim().Equals("") ? 0 : Convert.ToInt32(vertComponents[2]);
                            }
                            else if (vertComponents.Count() == 2)
                            {
                                vert.vert = Convert.ToInt32(vertComponents[0]);
                                vert.texCoord = vertComponents[1].Trim().Equals("") ? 0 : Convert.ToInt32(vertComponents[1]);
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

                            subMesh.name = "default" + subMeshData.Count();
                            subMesh.material = currentMaterial;
                            subMeshData.Add(subMesh);
                            pCurrentSubMesh = subMesh;
                        }

                        if (entries.Count() == 4)
                        {
                            //split this into groups
                            //numFaces += 2;
                            //numFaceVertexDefs += 6;

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
                        else if (entries.Count() == 3)
                        {
                            //we are using tris.
                            //++numFaces;
                            //numFaceVertexDefs += 3;

                            pCurrentSubMesh.faceData.Add(lineFaceData[0]);
                            pCurrentSubMesh.faceData.Add(lineFaceData[1]);
                            pCurrentSubMesh.faceData.Add(lineFaceData[2]);
                        }
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
                                subMesh.name = "default" + subMeshData.Count();
                                subMesh.material = currentMaterial;
                                subMeshData.Add(subMesh);
                                pCurrentSubMesh = subMesh;
                            }
                        }
                    }
                    else if (temp.Trim().StartsWith("mtllib"))
                    {
                        //ignore material files for now
                        /*const char* end = strchr( pData + 6, (char) 0x0A );
                        if ( end == null )
                            break;

                        String line( pData+6, end-(pData+6) );
                        StringUtil::Trim( line );
                        StringVector files = StringUtil::Split( line, " " );

                        if ( files.size() > 1 )
                        {
                            Console.WriteLine( "Warning: Automated mesh conversion will use only the first file in the mtllib defintion line. This may result in missing materials."  );
                        }

                        if (files.empty() )
                        {
                            Console.WriteLine( "Error: No files found in mtllib definition line."  );
                        }
                        else
                        {	
                            String fullMaterialPath = folder + "/" + files[0];
			
                            //we have a file, so convert it and set our materialPrefix name to be the 
                            //name of the material file without the extension.
                            String newMaterial = VaneOgre::VaneOgreRenderer::GetSingleton().GetMeshConverter().ConvertMtlToOgreMaterial( fullMaterialPath );
                            materialPrefix = FileSystem::StripFileExtension( newMaterial );
                        }*/
                    }
                }

                await GenerateXml(path, pNormals, pVerts, pTexCoords, subMeshData);
            }
        }

        public async Task GenerateXml(string path, List<Point3D> pNormals, List<Point3D> pVerts, List<Point> pTexCoords, List<SubMeshData> subMeshData)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Async = true;

            int indexOffset = 0;

            using (XmlWriter writer = XmlWriter.Create(Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".mesh.xml"), settings))
            {
                await writer.WriteStartDocumentAsync();

                //start mesh
                await writer.WriteStartElementAsync(null, "mesh", null);

                //start submeshes
                await writer.WriteStartElementAsync(null, "submeshes", null);

                foreach (SubMeshData smd in subMeshData)
                {
                    //start submesh
                    await writer.WriteStartElementAsync(null, "submesh", null);

                    //write attributes
                    await writer.WriteAttributeStringAsync(null, "operationtype", null, "triangle_list");
                    await writer.WriteAttributeStringAsync(null, "use32bitindexes", null, "true");
                    await writer.WriteAttributeStringAsync(null, "usesharedvertices", null, "false");
                    await writer.WriteAttributeStringAsync(null, "material", null, smd.material);

                    //write faces
                    await writer.WriteStartElementAsync(null, "faces", null);

                    //write face attribute
                    await writer.WriteAttributeStringAsync(null, "count", null, (smd.faceData.Count() / 3).ToString());

                    //write each face element
                    for (int i = 0; i < smd.faceData.Count(); i += 3)
                    {
                        await writer.WriteStartElementAsync(null, "face", null);
                        await writer.WriteAttributeStringAsync(null, "v1", null, (smd.faceData[i].vert - 1 - indexOffset).ToString());
                        await writer.WriteAttributeStringAsync(null, "v2", null, (smd.faceData[i + 1].vert - 1 - indexOffset).ToString());
                        await writer.WriteAttributeStringAsync(null, "v3", null, (smd.faceData[i + 2].vert - 1 - indexOffset).ToString());
                        await writer.WriteEndElementAsync();
                    }

                    //end faces
                    await writer.WriteEndElementAsync();

                    List<VertIndexData> distinctVertices = smd.faceData.Distinct().ToList();

                    indexOffset += distinctVertices.Count();

                    //write geometry
                    await writer.WriteStartElementAsync(null, "geometry", null);
                    await writer.WriteAttributeStringAsync(null, "vertexcount", null, distinctVertices.Count().ToString());

                    //write vertexbuffer
                    await writer.WriteStartElementAsync(null, "vertexbuffer", null);
                    await writer.WriteAttributeStringAsync(null, "positions", null, "true");
                    await writer.WriteAttributeStringAsync(null, "normals", null, "true");
                    await writer.WriteAttributeStringAsync(null, "texture_coord_dimensions_0", null, "2");
                    await writer.WriteAttributeStringAsync(null, "texture_coords", null, "1");



                    foreach (VertIndexData vid in distinctVertices)
                    {
                        //write vertex
                        await writer.WriteStartElementAsync(null, "vertex", null);

                        //write position
                        await writer.WriteStartElementAsync(null, "position", null);
                        await writer.WriteAttributeStringAsync(null, "x", null, pVerts[vid.vert - 1].X.ToString());
                        await writer.WriteAttributeStringAsync(null, "y", null, pVerts[vid.vert - 1].Y.ToString());
                        await writer.WriteAttributeStringAsync(null, "z", null, pVerts[vid.vert - 1].Z.ToString());
                        await writer.WriteEndElementAsync();

                        //write normal
                        await writer.WriteStartElementAsync(null, "normal", null);
                        await writer.WriteAttributeStringAsync(null, "x", null, pNormals[vid.normal - 1].X.ToString());
                        await writer.WriteAttributeStringAsync(null, "y", null, pNormals[vid.normal - 1].Y.ToString());
                        await writer.WriteAttributeStringAsync(null, "z", null, pNormals[vid.normal - 1].Z.ToString());
                        await writer.WriteEndElementAsync();

                        //writer texcoord
                        await writer.WriteStartElementAsync(null, "texcoord", null);
                        await writer.WriteAttributeStringAsync(null, "u", null, pTexCoords[vid.texCoord - 1].X.ToString());
                        await writer.WriteAttributeStringAsync(null, "v", null, pTexCoords[vid.texCoord - 1].Y.ToString());
                        await writer.WriteEndElementAsync();

                        //end vertex
                        await writer.WriteEndElementAsync();
                    }

                    //end vertexbuffer
                    await writer.WriteEndElementAsync();

                    //end geometry
                    await writer.WriteEndElementAsync();

                    //end submesh
                    await writer.WriteEndElementAsync();
                }

                //end submeshes
                await writer.WriteEndElementAsync();

                //start submeshnames
                await writer.WriteStartElementAsync(null, "submeshnames", null);

                //write submeshname elements
                for (int i = 0; i < subMeshData.Count(); i++)
                {
                    //start submeshname
                    await writer.WriteStartElementAsync(null, "submeshname", null);
                    await writer.WriteAttributeStringAsync(null, "name", null, subMeshData[i].name);
                    await writer.WriteAttributeStringAsync(null, "index", null, i.ToString());
                    await writer.WriteEndElementAsync();
                }

                //end submeshnames
                await writer.WriteEndElementAsync();

                //end mesh
                await writer.WriteEndElementAsync();

                //flush buffer
                await writer.FlushAsync();
            }

        }

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
                        //Assume mtl handling done outside of this scope for now
                        /*const char* end = strchr( pData + 6, (char) 0x0A );
                        if ( end == null )
                            break;

                        String line( pData+6, end-(pData+6) );
                        StringUtil::Trim( line );
                        StringVector files = StringUtil::Split( line, " " );

                        if ( files.size() > 1 )
                        {
                            LogMessage( "Warning: Automated mesh conversion will use only the first file in the mtllib defintion line. This may result in missing materials.", kLogMsgWarning  );
                        }

                        if (files.empty() )
                        {
                            LogMessage( "Error: No files found in mtllib definition line.", kLogMsgError  );
                        }
                        else
                        {	//we have a file, so convert it and set our materialPrefix name to be the 
                            //name of the material file without the extension.
                            String newMaterial = VaneOgre::VaneOgreRenderer::GetSingleton().GetMeshConverter().ConvertMtlToOgreMaterial( files[0] );
                            materialPrefix = FileSystem::StripFileExtension( newMaterial.c_str() );
                        }*/
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
