// Project: WorldCreatorBridge
// Filename: UnityTerrainUtility.cs
// Copyright (c) 2023 BiteTheBytes GmbH. All rights reserved
// *********************************************************

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

#if UNITY_EDITOR

namespace BtB.WC.Bridge
{
    public static class UnityTerrainUtility
    {
        #region Methods (Static / Public)

        public static void CreateTerrainFromFile(ParamsBridge pB)
        {
            ParamsImport pi = new ParamsImport
            {
                directoryXml = Path.GetDirectoryName(pB.bridgeFilePath) + "/",
                directoryAssets = "Assets/" + pB.terrainsFolderName + "/" + pB.assetName + "/"
            };

            GameObject container;
            Terrain[,] parts;

            // Load sync file
            XmlDocument doc = new XmlDocument();
            doc.Load(pB.bridgeFilePath);

            if (pB.isImportLayers)
            {
                XmlNodeList textureElements = doc.GetElementsByTagName("Texturing");
                if (textureElements.Count > 0)
                    pi.xmlTexture = textureElements[0];
            }

            // Load surface
            XmlNodeList surfaceElements = doc.GetElementsByTagName("Surface");
            if (surfaceElements.Count > 0)
            {
                XmlNode surface = surfaceElements[0];

                int xmlWidth, xmlLength, xmlResX, xmlResY, maxTileRes, minTileRes;

                if (surface.Attributes["TilesX"] != null)
                    pi.wcVersion = 2;
                else
                {
                    float wcV = 3;
                    XmlNodeList wcElements = doc.GetElementsByTagName("WorldCreator");
                    if (wcElements.Count > 0)
                        wcV = float.Parse(wcElements[0].Attributes["Version"].Value, CultureInfo.InvariantCulture.NumberFormat);
                    pi.wcVersion = wcV <= 2 ? 0 : 1;
                }
                
                pi.ComputeHeight(surface);
                int.TryParse(surface.Attributes["ResolutionX"].Value, out xmlResX);
                int.TryParse(surface.Attributes["ResolutionY"].Value, out xmlResY);
                int.TryParse(surface.Attributes["Width"].Value, out xmlWidth);
                int.TryParse(surface.Attributes["Length"].Value, out xmlLength);

                if (pi.wcVersion == 2)
                {
                    int.TryParse(surface.Attributes["TilesX"].Value, out pi.maxTilesX);
                    int.TryParse(surface.Attributes["TilesY"].Value, out pi.maxTilesY);
                }

                if (surface.Attributes["TileResolution"] != null)
                    int.TryParse(surface.Attributes["TileResolution"].Value, out maxTileRes);
                else
                    maxTileRes = Math.Max(xmlResX, xmlResY);

                minTileRes = Math.Min(maxTileRes, Math.Min(xmlResX, xmlResY));
                pB.internalSplit = Mathf.NextPowerOfTwo(minTileRes - 1);
                pi.precision = xmlWidth / (float) xmlResX;

                int terrainX = xmlResX;
                parts = new Terrain[Mathf.CeilToInt((float) xmlResX / pB.InternalSplit), Mathf.CeilToInt((float) xmlResY / pB.InternalSplit)];
                pi.tileResX = Math.Min(maxTileRes, xmlResX);
                pi.tileResY = Math.Min(maxTileRes, xmlResY);
                int precisionX = Mathf.CeilToInt(pi.tileResX * pi.precision);
                int precisionY = Mathf.CeilToInt(pi.tileResY * pi.precision);
                int locX = 0;

                container = GameObject.Find(pB.assetName);
                if (container != null)
                    GameObject.DestroyImmediate(container);
                container = new GameObject(pB.assetName);

                Terrain[,] curParts;

                for (int x = 0; x < pi.maxTilesX; x++)
                {
                    int terrainY = xmlResY;
                    int locY = 0;
                    int stepsX = 0;
                    
                    pi.curPartY = 0;

                    for (int y = 0; y < pi.maxTilesY; y++)
                    {
                        pi.terrainLeftX = Math.Min(maxTileRes, terrainX);
                        pi.terrainLeftY = Math.Min(maxTileRes, terrainY);
                        if (pi.terrainLeftX > 0 && pi.terrainLeftY > 0)
                        {
                            pi.SetTile(x, y, locX, locY);
                            curParts = ConstructTerrainPart(pB, pi, container.transform);

                            stepsX = curParts.GetLength(0);
                            int stepsY = curParts.GetLength(1);
                            for (int xP = 0; xP < stepsX; xP++)
                            for (int yP = 0; yP < stepsY; yP++)
                                parts[pi.curPartX + xP, pi.curPartY + yP] = curParts[xP, yP];
                            pi.curPartY += stepsY;
                        }

                        terrainY -= maxTileRes;
                        locY += precisionY;
                    }
                    
                    pi.curPartX += stepsX;
                    terrainX -= maxTileRes;
                    locX += precisionX;

                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                for (int xP = 0; xP < parts.GetLength(0); xP++)
                for (int yP = 0; yP < parts.GetLength(1); yP++)
                {
                    Terrain left = xP > 0 ? parts[xP - 1, yP] : null;
                    Terrain right = xP < parts.GetLength(0) - 1 ? parts[xP + 1, yP] : null;
                    Terrain top = yP > 0 ? parts[xP, yP - 1] : null;
                    Terrain bottom = yP < parts.GetLength(1) - 1 ? parts[xP, yP + 1] : null;

                    parts[xP, yP].SetNeighbors(left, bottom, right, top);
                }
            }
            else return;
            
            // Load Details

            // Load Objects

            // Finish Terrain
            foreach (Terrain t in container.transform.GetComponentsInChildren<Terrain>())
                t.Flush();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Terrain[,] ConstructTerrainPart(ParamsBridge pb, ParamsImport pi, Transform container)
        {
            float terrainHeight = 100 * pi.height * pb.worldScale;
            float mult = pi.precision * pb.worldScale;
            float tileScaleX = pb.InternalSplit * mult;
            float tileScaleY = pb.InternalSplit * mult;

            float curLocX = pi.locationX;
            float stepWs = pb.InternalSplit * mult;

            int stepsX = Mathf.CeilToInt(pi.terrainLeftX / (float)pb.InternalSplit);
            int stepsY = Mathf.CeilToInt(pi.terrainLeftY / (float)pb.InternalSplit);
            Terrain[,] parts = new Terrain[stepsX, stepsY];

            int heightMapRes = pb.InternalSplit + 1;

            string heightMapPath = pi.directoryXml + "heightmap" + pi.nameEnding + ".raw";
            float[,] heightMap = Importer.RawUint16FromFile(heightMapPath, pi.tileResX, pi.tileResY, false, pi.tileResX * 2, 0, pi.wcVersion == 2);

            float[,] heightMapSplit = new float[heightMapRes, heightMapRes];

            int fullX = pi.terrainLeftX;
            float texOffsetX = 0;
            if(pb.isImportLayers)
                texOffsetX = pi.curTileX;
            float texSizeX = 0;
            float texSizeY = 0;

            for (int tileX = 0; tileX < stepsX; tileX++)
            {
                float curLocY = pi.locationY;
                int fullY = pi.terrainLeftY;
                float texOffsetY = 0;
                if(pb.isImportLayers)
                    texOffsetY = pi.curTileY;
                
                for(int tileY = 0; tileY < stepsY; tileY++)
                {
                    #region Terrain
                    
                    string tileName = pb.assetName + "_" + (pi.curPartX + tileX) + "_" + (pi.curPartY + tileY);
                    string assetPath = @"Assets/" + pb.terrainsFolderName + "/" + pb.assetName + "/" + tileName + ".asset";
                    TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);

                    if (terrainData == null)
                    {
                        terrainData = new TerrainData();
                        terrainData.SetDetailResolution(pb.InternalSplit, 16);
                        terrainData.name = tileName;
                        AssetDatabase.CreateAsset(terrainData, assetPath);
                    }

                    terrainData.heightmapResolution = heightMapRes;
                    terrainData.alphamapResolution = pb.InternalSplit;

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    for (int x = 0; x < heightMapRes; x++)
                    for (int y = 0; y < heightMapRes; y++)
                    {
                        int clampedX = Mathf.Clamp(tileY * pb.InternalSplit + x, 0, pi.tileResY - 1);
                        int clampedY = Mathf.Clamp(tileX * pb.InternalSplit + y, 0, pi.tileResX - 1);
                        heightMapSplit[x, y] = heightMap[clampedX, clampedY];
                    }

                    terrainData.SetHeights(0, 0, heightMapSplit);
                    terrainData.size = new Vector3(tileScaleX, terrainHeight, tileScaleY);

                    Transform tileTransform = container.Find(tileName);
                    GameObject tileGo = null;

                    if (tileTransform == null)
                    {
                        tileGo = Terrain.CreateTerrainGameObject(terrainData);
                        tileGo.name = tileName;
                        tileGo.transform.parent = container;
                    }
                    else
                    {
                        tileGo = tileTransform.gameObject;
                        tileGo.GetComponent<Terrain>().terrainData = terrainData;
                    }

                    Terrain tile = tileGo.GetComponent<Terrain>();
                    // set pixel error to 1 for crisp textures
                    tile.heightmapPixelError = 1f;
                    parts[tileX, tileY] = tile;
                    tileGo.transform.position = new Vector3(curLocX, 0, curLocY);
                    
                    #endregion

                    #region Material
                    
                    // Set/Create Material
                    Material createdMat = null;
                    if (pb.materialType != MaterialType.Custom)
                    {
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/WorldCreatorBridge/Content/Materials/WC_Default_Terrain_" + pb.materialType + ".mat");
                        Texture2D tex;
                        if (pb.isImportLayers)
                            tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/" + pb.terrainsFolderName + "/" + pb.assetName + "/colormap" + pi.nameEnding + ".png");
                        else
                            tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/WorldCreatorBridge/Content/Textures/albedo_default.png");
                        string matPath = "Assets/" + pb.terrainsFolderName + "/" + pb.assetName + "/" + tileName + ".mat";

                        Material matInstance = new Material(mat);
                        switch (pb.materialType)
                        {
                            case MaterialType.HDRP:
                                matInstance.EnableKeyword("HDRP_ENABLED");
                                break;
                            case MaterialType.URP:
                                matInstance.EnableKeyword("URP_ENABLED");
                                break;
                        }

                        Vector2 tileOffset = new Vector2(texOffsetX, texOffsetY);

                        if (pb.isImportLayers)
                        {
                            if(pi.curTileX > 0)
                                texSizeX = (float) Mathf.Min(4096, pb.InternalSplit) / (4096);
                            else
                                texSizeX = (float) pb.InternalSplit / (pi.tileResX);
                            
                            if(pi.curTileY > 0)
                                texSizeY = (float) Mathf.Min(4096, pb.InternalSplit) / (4096);
                            else
                                texSizeY = (float) pb.InternalSplit / (pi.tileResY);
                        }
                        else
                        {
                            if(pi.curTileX > 0)
                                texSizeX = (float) Mathf.Min(4096, pb.InternalSplit) / (4096);
                            else
                                texSizeX = (float) pb.InternalSplit / (Mathf.Max(pi.terrainLeftX, pb.InternalSplit));
                            
                            if(pi.curTileY > 0)
                                texSizeY = (float) Mathf.Min(4096, pb.InternalSplit) / (4096);
                            else
                                texSizeY = (float) pb.InternalSplit / (Mathf.Max(pi.terrainLeftY, pb.InternalSplit));
                        }

                        if (pi.wcVersion == 1)
                        {
                            tileOffset.y -= 1;
                        }
                        matInstance.SetTexture("_ColorMap", tex);
                        matInstance.SetVector("_OffsetSize", new Vector4(tileOffset.x, tileOffset.y, texSizeX, texSizeY));
                        AssetDatabase.CreateAsset(matInstance, matPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        createdMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        tile.materialTemplate = createdMat;
                    }
                    else
                        tile.materialTemplate = pb.customMaterial;
                    
                    #endregion

                    texOffsetY += texSizeY;
                    fullY -= pb.InternalSplit;
                    curLocY += stepWs;
                }

                texOffsetX += texSizeX;
                fullX -= pb.InternalSplit;
                curLocX += stepWs;
            }
            
            if(pb.isImportLayers)
                FillMaterial_Layers(pb, pi, ref parts);
            else
                FillMaterial_Single(pb, pi, ref parts);

            return parts;
        }

        private static void FillMaterial_Layers(ParamsBridge pb, ParamsImport pi, ref Terrain[,] parts)
        {
            // Load Splatmaps
            XmlNode texturing = pi.xmlTexture;
            int iter_total = 0;
            List<TerrainLayer> splatPrototypes = new List<TerrainLayer>();

            Vector2 uvMult = pi.precision * new Vector2(pi.tileResX, pi.tileResY);
            foreach (XmlElement splatmap in texturing)
            {
                foreach (XmlElement textureInfo in splatmap.ChildNodes)
                {
                    pi.SetXmlTexture(textureInfo);
                    Vector2 tileSize = Vector2FromString(XML.GetAttrib(textureInfo, "TileSize", "1,1"));
                    Vector2 tileOffset = Vector2FromString(XML.GetAttrib(textureInfo, "TileOffset", "0,0"));

                    string smoothnessString = XML.GetAttrib(textureInfo, "Smoothness", "0");
                    string metallicString = XML.GetAttrib(textureInfo, "Metallic", "0");
                    float smoothness = float.Parse(smoothnessString, NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat);
                    float metallic = float.Parse(metallicString, NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat);
                    int maxRes = Math.Max(pi.terrainLeftX, pi.terrainLeftY);

                    if (pi.wcVersion == 0)
                    {
                        tileSize.x *= (float) pb.InternalSplit / (pi.tileResX);
                        tileSize.y *= (float) pb.InternalSplit / (pi.tileResY);
                        tileOffset *= new Vector2((float)pb.InternalSplit / pi.tileResX, (float)pb.InternalSplit / pi.tileResY);
                    }
                    else if (pi.wcVersion == 1)
                    {
                        tileSize = Vector2.one * maxRes * pb.worldScale / tileSize * pi.precision;
                        float minTile = Mathf.Min(tileSize.x, tileSize.y);
                        tileSize = new Vector2(minTile * pb.InternalSplit / pi.tileResX, minTile * pb.InternalSplit / pi.tileResY);
                    }
                    else    // keep the tileSize/tileOffset from the xml for WC >2023 
                    {
                        Vector2 maxTiles = new Vector2(pi.terrainLeftX / (float)pb.InternalSplit, pi.terrainLeftY / (float)pb.InternalSplit);

                        if (pi.curTileX > 0)
                            tileSize.x /= Mathf.CeilToInt(pi.terrainLeftX / (float) pb.InternalSplit);
                        else if(pi.tileResX < pb.InternalSplit)
                            tileSize.x = Mathf.CeilToInt(tileSize.x * ((float) pb.InternalSplit / Mathf.Min(pi.tileResX, pb.InternalSplit)));
                        else
                            tileSize.x = Mathf.CeilToInt(tileSize.x * ((float) pb.InternalSplit / Mathf.Max(pi.tileResX, pb.InternalSplit)));
                        
                        if (pi.curTileY > 0)
                            tileSize.y /= Mathf.CeilToInt(pi.terrainLeftY / (float) pb.InternalSplit);
                        else if(pi.tileResY < pb.InternalSplit)
                            tileSize.y = Mathf.CeilToInt(tileSize.y * ((float) pb.InternalSplit / Mathf.Min(pi.tileResY, pb.InternalSplit)));
                        else
                            tileSize.y = Mathf.CeilToInt(tileSize.y * ((float) pb.InternalSplit / Mathf.Max(pi.tileResY, pb.InternalSplit)));
                        
                        tileOffset = (tileOffset * -1) / maxTiles;
                    }
                    
                    TerrainLayer layer = new TerrainLayer
                    {
                        metallic = metallic,
                        smoothness = smoothness,
                        specular = Color.white,
                        tileOffset = tileOffset,
                        tileSize = tileSize
                    };

                    string assetProjectPath = "Assets/" + pb.terrainsFolderName + "/" + pb.assetName + "/Assets/";

                    // Albedo - if built-in rp try to pack the roughness map into the diffuse maps alpha channel
                    if (pi.HasTexture("AlbedoFile"))
                    {
                        string albedoPath = pi.GetTexture("AlbedoFile");
                        Texture2D diffuseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetProjectPath + albedoPath);
                        layer.diffuseTexture = diffuseTex;
                    }
                    else
                        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/WorldCreatorBridge/Content/Textures/albedo_default.png");
                    
                    if (pb.materialType == MaterialType.URP || pb.materialType == MaterialType.HDRP)
                    {
                        string colorString = textureInfo.Attributes["Color"].Value;
                        Color color = Color.white;
                        if(pi.wcVersion == 0)
                            ColorUtility.TryParseHtmlString(colorString, out color);
                        else
                            color = ReadWCColor(colorString);
                        layer.diffuseRemapMax = color;
                    }
                    
                    // Normal 
                    if (pi.HasTexture("NormalFile"))
                    {
                        string normalPath = pi.GetTexture("NormalFile");
                        layer.normalMapTexture = ImportNormal(assetProjectPath + normalPath);
                    }
                    else
                        layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/WorldCreatorBridge/Content/Textures/normal_default.png");

                    // Mask map - only used in HDRP and URP terrain shaders, excluded for built-in rp
                    if (pb.materialType != MaterialType.Standard && (pi.HasTexture("AoFile") || pi.HasTexture("RoughnessFile")))
                    {
                        Texture2D maskMap = Texture2D.whiteTexture;
                        ComputeShader maskMapConversionCs = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/WorldCreatorBridge/Content/Shaders/Compute/MaskMap.compute");

                        string aoPath = "";
                        if (pi.HasTexture("AoFile"))
                            aoPath = pi.GetTexture("AoFile");
                        string roughnessPath = "";
                        if (pi.HasTexture("RoughnessFile"))
                            roughnessPath = pi.GetTexture("RoughnessFile");

                        if (!string.IsNullOrEmpty(aoPath) && !string.IsNullOrEmpty(roughnessPath))
                        {
                            Texture2D aoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetProjectPath + aoPath);
                            Texture2D roughnessTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetProjectPath + roughnessPath);

                            bool roughnessOnly = false;
                            int res = roughnessTex.width;

                            if (aoTex.width != roughnessTex.width)
                            {
                                roughnessOnly = true;
                                Debug.Log("AO map has not been loaded into mask map. To do so, please make sure that both maps are of the same resolution.");
                            }

                            int kernel = roughnessOnly ? 2 : 0;
                            maskMapConversionCs.SetInt("_Res", res);
                            if (!roughnessOnly) maskMapConversionCs.SetTexture(0, "_AOTex", aoTex);
                            maskMapConversionCs.SetTexture(kernel, "_RoughnessTex", roughnessTex);

                            ComputeBuffer maskBuffer = new ComputeBuffer(res * res, 16);
                            maskMapConversionCs.SetBuffer(kernel, "_OutBuffer", maskBuffer);
                            maskMapConversionCs.Dispatch(kernel, res / 8, res / 8, 1);

                            Color[] colors = new Color[res * res];
                            maskBuffer.GetData(colors);
                            maskMap = new Texture2D(res, res, TextureFormat.ARGB32, true);
                            maskMap.SetPixels(colors);
                            maskMap.Apply(true);
                        }
                        else if (!string.IsNullOrEmpty(aoPath))
                        {
                            Texture2D aoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetProjectPath + aoPath);

                            int res = aoTex.width;

                            maskMapConversionCs.SetInt("_Res", res);
                            maskMapConversionCs.SetTexture(1, "_AOTex", aoTex);

                            ComputeBuffer maskBuffer = new ComputeBuffer(res * res, 16);
                            maskMapConversionCs.SetBuffer(1, "_OutBuffer", maskBuffer);
                            maskMapConversionCs.Dispatch(1, res / 8, res / 8, 1);

                            Color[] colors = new Color[res * res];
                            maskBuffer.GetData(colors);
                            maskMap = new Texture2D(res, res, TextureFormat.ARGB32, true);
                            maskMap.SetPixels(colors);
                            maskMap.Apply(true);
                        }
                        else if (!string.IsNullOrEmpty(roughnessPath))
                        {
                            Texture2D roughnessTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetProjectPath + roughnessPath);

                            int res = roughnessTex.width;

                            maskMapConversionCs.SetInt("_Res", res);
                            maskMapConversionCs.SetTexture(2, "_AOTex", roughnessTex);

                            ComputeBuffer maskBuffer = new ComputeBuffer(res * res, 16);
                            maskMapConversionCs.SetBuffer(2, "_OutBuffer", maskBuffer);
                            maskMapConversionCs.Dispatch(2, res / 8, res / 8, 1);

                            Color[] colors = new Color[res * res];
                            maskBuffer.GetData(colors);
                            maskMap = new Texture2D(res, res, TextureFormat.ARGB32, true);
                            maskMap.SetPixels(colors);
                            maskMap.Apply(true);
                            layer.maskMapTexture = maskMap;
                        }

                        layer.maskMapTexture = maskMap;
                    }
                    else
                    {
                        Texture2D baseMask = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/WorldCreatorBridge/Content/Textures/mask_default.png");
                        layer.maskMapTexture = baseMask;
                    }

                    
                    string splatName = textureInfo.Attributes["Name"].Value;
                    splatName.Replace(" ", "_");
                    
                    // Check if Terrainlayer already exists, if load the exisiting one
                    if (File.Exists(pi.directoryAssets + splatName + "_wc_" + iter_total + ".terrainlayer"))
                        layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(pi.directoryAssets + splatName + "_wc_" + iter_total + ".terrainlayer");
                    else
                        AssetDatabase.CreateAsset(layer, pi.directoryAssets + splatName + "_wc_" + iter_total + ".terrainlayer");
                    
                    splatPrototypes.Add(layer);
                    iter_total++;
                }
            }

            pb.layerWarning = false;

            if ((pb.materialType == MaterialType.URP || pb.materialType == MaterialType.Standard) && iter_total > 4)
                pb.layerWarning = true;
            else if (pb.materialType == MaterialType.HDRP && iter_total > 8)
                pb.layerWarning = true;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            TerrainLayer[] splatProtoArray = splatPrototypes.ToArray();

            float[,,] alphaMaps = new float[pi.tileResX, pi.tileResY, iter_total];

            int textureIndex = 0;
            int iter_splat = 0;
            foreach (XmlElement splatmap in texturing)
            {
                string fileName = "splatmap_" + iter_splat + pi.nameEnding + ".tga";
                if (pi.wcVersion == 0)
                    fileName = "splatmap_0" + iter_splat + ".tga";
                else if (pi.wcVersion == 1)
                    fileName = "Splat Map_" + iter_splat + pi.nameEnding + ".tga";


                Vector4[] pixels = ReadRGBA(pi.directoryXml + fileName, out int textureWidth, out int textureHeight);

                for (int channelIndex = 0; channelIndex < splatmap.ChildNodes.Count; channelIndex++)
                {
                    for (int y = 0; y < pi.tileResY; y++)
                    for (int x = 0; x < pi.tileResX; x++)
                    {
                            
                        int realX = Mathf.Clamp(x - 1, 0, textureWidth - 1);
                        if (pi.wcVersion == 0) realX = pi.tileResX - 1 - realX;
                        int realY = Mathf.Clamp(y, 0, textureHeight - 1);

                        int pixelIndex = realX + realY * textureWidth;
                        switch (channelIndex)
                        {
                            case 0:
                                alphaMaps[x, y, textureIndex] = pixels[pixelIndex].z;
                                break;
                            case 1:
                                alphaMaps[x, y, textureIndex] = pixels[pixelIndex].y;
                                break;
                            case 2:
                                alphaMaps[x, y, textureIndex] = pixels[pixelIndex].x;
                                break;
                            case 3:
                                alphaMaps[x, y, textureIndex] = pixels[pixelIndex].w;
                                break;
                        }
                    }

                    textureIndex++;
                }

                iter_splat++;
            }

            int alphaMapRes = Mathf.Min(4096, pb.InternalSplit);
            float[,,] splitAlphaMaps = new float[alphaMapRes, alphaMapRes, iter_total];
            for (int xP = 0; xP < parts.GetLength(0); xP++)
            for (int yP = 0; yP < parts.GetLength(1); yP++)
            {
                TerrainData terrainData = parts[xP, yP].terrainData;
                terrainData.terrainLayers = splatProtoArray;

                int xOff = xP * pb.InternalSplit;
                int yOff = yP * pb.InternalSplit;

                float scale = pb.InternalSplit <= 4096 ? 1 : (float) pb.InternalSplit / alphaMapRes;

                for (int x = 0; x < alphaMapRes; x++)
                for (int y = 0; y < alphaMapRes; y++)
                {
                    int realX = xOff + Mathf.CeilToInt(y * ((float) (pb.InternalSplit + 1) / pb.InternalSplit) * scale);
                    int realY = yOff + Mathf.CeilToInt(x * ((float) (pb.InternalSplit + 1) / pb.InternalSplit) * scale);

                    if (realX >= pi.tileResX) realX = pi.tileResX - 1;
                    if (realY >= pi.tileResY) realY = pi.tileResY - 1;

                    for (int i = 0; i < iter_total; i++)
                        splitAlphaMaps[x, y, i] = alphaMaps[realX, realY, i];
                }

                terrainData.SetAlphamaps(0, 0, splitAlphaMaps);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        
        private static void FillMaterial_Single(ParamsBridge pb, ParamsImport pi, ref Terrain[,] parts)
        {
            Vector2 maxRes = new Vector2(pi.terrainLeftX, pi.terrainLeftY);
            Vector2 maxTiles = new Vector2(Mathf.CeilToInt(maxRes.x/ (float)pb.InternalSplit), Mathf.CeilToInt(maxRes.y / (float)pb.InternalSplit));
            Vector2 tileSize = new Vector2(Math.Min(pb.InternalSplit, pi.tileResX), Math.Min(pb.InternalSplit, pi.tileResY));

            if (pi.wcVersion == 1 || pi.wcVersion == 2)
                tileSize *= pi.precision;
                
            TerrainLayer baseLayer = new TerrainLayer
            {
                metallic = 0,
                smoothness = 0,
                specular = Color.white,
                tileSize = tileSize
            };
            
            Texture2D baseDiffuse;
            string assetpath = "Assets/" + pb.terrainsFolderName + "/" + pb.assetName + "/";
            if (File.Exists(assetpath + "texturemap" + pi.nameEnding + ".png"))
                baseDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(assetpath + "texturemap" + pi.nameEnding + ".png");
            else
                baseDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(assetpath + "colormap" + pi.nameEnding + ".png");
            Texture2D baseNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/WorldCreatorBridge/Content/Textures/normal_default.png");
            Texture2D baseMask = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/WorldCreatorBridge/Content/Textures/mask_default.png");
            
            baseLayer.diffuseTexture = baseDiffuse;
            baseLayer.normalMapTexture = baseNormal;
            baseLayer.maskMapTexture = baseMask;
            
            // Check if Terrainlayer already exists, if load the exisiting one
            if (File.Exists(pi.directoryAssets + "baselayer_wc_" + pi.nameEnding + ".terrainlayer"))
                baseLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(pi.directoryAssets + "baselayer_wc_" + pi.nameEnding + ".terrainlayer");
            else
                AssetDatabase.CreateAsset(baseLayer, pi.directoryAssets + "baselayer_wc" + pi.nameEnding + ".terrainlayer");

            for (int xP = 0; xP < parts.GetLength(0); xP++)
            for (int yP = 0; yP < parts.GetLength(1); yP++)
            {
                TerrainData terrainData = parts[xP, yP].terrainData;
                terrainData.terrainLayers = new[] {baseLayer};
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        private static Texture2D ImportNormal(string path)
        {
            TextureImporter normalImporter = TextureImporter.GetAtPath(path) as TextureImporter;
            if (normalImporter != null)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        

        /// <summary>
        ///   Creates an instance of Vector3 from the specified string.
        /// </summary>
        /// <param name="value">A string that encodes a Vector3.</param>
        /// <returns>A new instance of Vector3.</returns>
        public static Vector2 Vector2FromString(string value)
        {
            var parts = value.Replace("(", "").Replace(")", "").Split(',');
            var v = new Vector2();
            try
            {
                v.x = float.Parse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture);
                v.y = float.Parse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture);
            }
            catch
            {
                Debug.Log("Vector parse failed");
            }

            return v;
        }

        /// <summary>
        /// Loads a tga from the given filepath
        /// </summary>
        /// <param name="path"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Vector4[] ReadRGBA(string path, out int width, out int height)
        {
            using (FileStream fileStream = File.OpenRead(path))
            {
                using (BinaryReader reader = new BinaryReader(fileStream))
                {
                    reader.BaseStream.Seek(12, SeekOrigin.Begin);
                    width = reader.ReadInt16();
                    height = reader.ReadInt16();
                    int bitDepth = reader.ReadByte();
                    reader.BaseStream.Seek(1, SeekOrigin.Current);

                    int size = width * height;
                    Vector4[] textureData = new Vector4[size];
                    const float invByte = 1.0f / 255.0f;
                    if (bitDepth == 32)
                    {
                        for (int i = 0; i < size; i++)
                        {
                            textureData[i] = new Vector4(
                                reader.ReadByte() * invByte,
                                reader.ReadByte() * invByte,
                                reader.ReadByte() * invByte,
                                reader.ReadByte() * invByte);
                        }
                    }
                    else if (bitDepth == 24)
                    {
                        for (int i = 0; i < size; i++)
                        {
                            textureData[i] = new Vector4(
                                reader.ReadByte() * invByte,
                                reader.ReadByte() * invByte,
                                reader.ReadByte() * invByte, 1);
                        }
                    }
                    else if (bitDepth == 8)
                    {
                        for (int i = 0; i < size; i++)
                        {
                            float v = reader.ReadByte() * invByte;
                            textureData[i] = new Vector4(
                                v, 
                                v, 
                                v, 
                                1);
                        }
                    }
                    else
                        return null;

                    return textureData;
                }
            }
        }

        /// <summary>
        /// Converts a hex color from WC Sync file to a color.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        private static Color ReadWCColor(string hex)
        {
            hex = hex.Replace("#", "");
            if (hex.Length != 8) throw new Exception("Wrong hex format.");
            float a = Int32.Parse(hex.Substring(0,2), NumberStyles.HexNumber) / 255f;
            float r = Int32.Parse(hex.Substring(2,2), NumberStyles.HexNumber) / 255f;
            float g = Int32.Parse(hex.Substring(4,2), NumberStyles.HexNumber) / 255f;
            float b = Int32.Parse(hex.Substring(6,2), NumberStyles.HexNumber) / 255f;
            return new Color(r,g,b,a);
        }
        
        #endregion Methods (Static / Public)
    }
}

#endif