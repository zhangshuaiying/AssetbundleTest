﻿using System;
using System.Collections.Generic;
using System.IO;
using AssetBundleBrowser.AssetBundleDataSource;
using AssetBundles;
using UnityEditor;
using UnityEngine;

namespace AssetBundleBrowser
{
    public class AssetNode
    {
        public readonly List<AssetNode> Parents = new List<AssetNode>();
        public int Depth;
        public string Path;
    }

    public class AssetBundleBuilder
    {
        private static readonly string ASSSETS_STRING = "Assets";
        private static readonly int CURRENT_VERSION_MAJOR = 1;
        private readonly Dictionary<string, AssetNode> mAllAssetNodes = new Dictionary<string, AssetNode>();
        private readonly List<string> mBuildMap = new List<string>();

        private readonly List<AssetNode> mLeafNodes = new List<AssetNode>();
        private ABBuildInfo mAbBuildInfo;

        private List<BuildFolder> mDependciesFolder;
        private List<BuildFolder> mSingleFolder;

        public void BuildAssetBundle(ABBuildInfo buildInfo)
        {
            mAbBuildInfo = buildInfo;

            mDependciesFolder = mAbBuildInfo.buildFolderList.FindAll(bf => !bf.SingleAssetBundle);
            mSingleFolder = mAbBuildInfo.buildFolderList.FindAll(bf => bf.SingleAssetBundle);

            mBuildMap.Clear();
            mLeafNodes.Clear();
            mAllAssetNodes.Clear();

            CollectSingle();
            CollectDependcy();
            BuildResourceBuildMap();
            BuildAssetBundleWithBuildMap();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public void SetAssetBundleNames(List<BuildFolder> buildFolderList)
        {
            mDependciesFolder = buildFolderList.FindAll(bf => !bf.SingleAssetBundle);
            mSingleFolder = buildFolderList.FindAll(bf => bf.SingleAssetBundle);

            mBuildMap.Clear();
            mLeafNodes.Clear();
            mAllAssetNodes.Clear();

            CollectSingle();
            CollectDependcy();
            BuildResourceBuildMap();
            SetAssetBundleNames();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void SetAssetBundleNames()
        {
            if (mBuildMap.Count == 0)
            {
                return;
            }

            foreach (string path in mBuildMap)
            {
                var assetImporter = AssetImporter.GetAtPath(path);
                var singleBuildFolder = GetSingleBuildFolder(path);
                if (singleBuildFolder != null)
                {
                    assetImporter.SetAssetBundleNameAndVariant(
                        string.IsNullOrEmpty(singleBuildFolder.AssetBundleName)
                            ? singleBuildFolder.Path.Substring(ASSSETS_STRING.Length + 1)
                            : singleBuildFolder.AssetBundleName, string.Empty);
                }
                else
                {
                    assetImporter.SetAssetBundleNameAndVariant(path.Substring(ASSSETS_STRING.Length + 1), string.Empty);
                }
            }
        }

        private void BuildAssetBundleWithBuildMap()
        {
            ClearAssetBundleNames();

            if (mBuildMap.Count == 0)
            {
                return;
            }

            SetAssetBundleNames();

            if (!Directory.Exists(mAbBuildInfo.outputDirectory))
            {
                Directory.CreateDirectory(mAbBuildInfo.outputDirectory);
            }
            var buildManifest = BuildPipeline.BuildAssetBundles(mAbBuildInfo.outputDirectory, mAbBuildInfo.options,
                mAbBuildInfo.buildTarget);

            if (buildManifest == null)
            {
                Debug.Log("Error in build");
                return;
            }

            GenerateAssetBundleUpdateInfo(buildManifest);

            foreach (string assetBundleName in buildManifest.GetAllAssetBundles())
            {
                if (mAbBuildInfo.onBuild != null)
                {
                    mAbBuildInfo.onBuild(assetBundleName);
                }
            }
        }

        private void BuildResourceBuildMap()
        {
            int maxDepth = GetMaxDepthOfLeafNodes();
            while (mLeafNodes.Count > 0)
            {
                var curDepthNodesList = new List<AssetNode>();
                for (int i = 0; i < mLeafNodes.Count; i++)
                {
                    if (mLeafNodes[i].Depth == maxDepth)
                    {
                        if (mLeafNodes[i].Parents.Count != 1)
                        {
                            if (!ShouldIgnoreFile(mLeafNodes[i].Path))
                            {
                                mBuildMap.Add(mLeafNodes[i].Path);
                            }
                        }
                        curDepthNodesList.Add(mLeafNodes[i]);
                    }
                }
                for (int i = 0; i < curDepthNodesList.Count; i++)
                {
                    mLeafNodes.Remove(curDepthNodesList[i]);
                    foreach (var node in curDepthNodesList[i].Parents)
                    {
                        if (!mLeafNodes.Contains(node))
                        {
                            mLeafNodes.Add(node);
                        }
                    }
                }

                maxDepth -= 1;
            }
        }

        private bool CheckFileSuffixNeedIgnore(string fileName)
        {
            if (fileName.EndsWith(".meta") || fileName.EndsWith(".DS_Store") || fileName.EndsWith(".cs"))
            {
                return true;
            }

            return false;
        }

        private void ClearAssetBundleNames()
        {
            var assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
            foreach (string name in assetBundleNames)
            {
                AssetDatabase.RemoveAssetBundleName(name, true);
            }
        }


        private void CollectDependcy()
        {
            for (int i = 0; i < mDependciesFolder.Count; i++)
            {
                string path = Application.dataPath + "/" +
                              mDependciesFolder[i].Path.Substring(ASSSETS_STRING.Length + 1);
                if (!Directory.Exists(path))
                {
                    Debug.LogError(string.Format("abResourcePath {0} not exist", mDependciesFolder[i].Path));
                }
                else
                {
                    var dir = new DirectoryInfo(path);
                    var files = dir.GetFiles("*", SearchOption.AllDirectories);
                    for (int j = 0; j < files.Length; j++)
                    {
                        if (CheckFileSuffixNeedIgnore(files[j].Name))
                        {
                            continue;
                        }

                        string fileRelativePath = GetReleativeToAssets(files[j].FullName);
                        AssetNode root;
                        mAllAssetNodes.TryGetValue(fileRelativePath, out root);
                        if (root == null)
                        {
                            root = new AssetNode();
                            root.Path = fileRelativePath;
                            mAllAssetNodes[root.Path] = root;
                            GetDependcyRecursive(fileRelativePath, root);
                        }
                    }
                }
            }
            //PrintDependcy();
        }

        private void CollectSingle()
        {
            for (int i = 0; i < mSingleFolder.Count; i++)
            {
                string path = Application.dataPath + "/" + mSingleFolder[i].Path.Substring(ASSSETS_STRING.Length + 1);
                if (!Directory.Exists(path))
                {
                    Debug.LogError(string.Format("abResourcePath {0} not exist", mSingleFolder[i].Path));
                }
                else
                {
                    var dir = new DirectoryInfo(path);
                    var files = dir.GetFiles("*", SearchOption.AllDirectories);
                    for (int j = 0; j < files.Length; j++)
                    {
                        if (CheckFileSuffixNeedIgnore(files[j].Name))
                        {
                            continue;
                        }

                        string fileRelativePath = GetReleativeToAssets(files[j].FullName);
                        mBuildMap.Add(fileRelativePath);
                    }
                }
            }
        }

        private void GenerateAssetBundleUpdateInfo(AssetBundleManifest manifest)
        {
            var versionInfo =
                new AssetBundleVersionInfo
                {
                    MinorVersion = int.Parse(DateTime.Now.ToString("yyMMddHHmm")),
                    MarjorVersion = CURRENT_VERSION_MAJOR
                };
            versionInfo.Save(mAbBuildInfo.outputDirectory);
            var assetBundleUpdateInfo = new AssetBundleUpdateInfo(versionInfo.MinorVersion, manifest);
            assetBundleUpdateInfo.Save(mAbBuildInfo.outputDirectory);
        }

        private void GetDependcyRecursive(string path, AssetNode parentNode)
        {
            if (GetSingleBuildFolder(path) != null)
            {
                return;
            }

            var dependcy = AssetDatabase.GetDependencies(path, false);
            for (int i = 0; i < dependcy.Length; i++)
            {
                AssetNode node;
                mAllAssetNodes.TryGetValue(dependcy[i], out node);
                if (node == null)
                {
                    node = new AssetNode();
                    node.Path = dependcy[i];
                    node.Depth = parentNode.Depth + 1;
                    node.Parents.Add(parentNode);
                    mAllAssetNodes[node.Path] = node;
                    GetDependcyRecursive(dependcy[i], node);
                }
                else
                {
                    if (!node.Parents.Contains(parentNode))
                    {
                        node.Parents.Add(parentNode);
                    }
                    if (node.Depth < parentNode.Depth + 1)
                    {
                        node.Depth = parentNode.Depth + 1;
                        GetDependcyRecursive(dependcy[i], node);
                    }
                }
                //Debug.Log("dependcy path is " +dependcy[i] + " parent is " + parentNode.path);
            }

            if (dependcy.Length == 0)
            {
                if (!mLeafNodes.Contains(parentNode))
                {
                    mLeafNodes.Add(parentNode);
                }
            }
        }

        private int GetMaxDepthOfLeafNodes()
        {
            if (mLeafNodes.Count == 0)
            {
                return 0;
            }

            mLeafNodes.Sort((x, y) => y.Depth - x.Depth);
            return mLeafNodes[0].Depth;
        }

        private string GetReleativeToAssets(string fullName)
        {
            string fileRelativePath = fullName.Substring(Application.dataPath.Length - ASSSETS_STRING.Length);
            fileRelativePath = fileRelativePath.Replace("\\", "/");
            return fileRelativePath;
        }

        private BuildFolder GetSingleBuildFolder(string path)
        {
            path = path.Replace("\\", "/");
            return mSingleFolder.Find(sf => path.StartsWith(sf.Path));
        }

        private bool ShouldIgnoreFile(string path)
        {
            if (path.EndsWith(".cs"))
            {
                return true;
            }

            return false;
        }
    }
}