﻿using UnityEditor;
using System;
using System.Collections.Generic;

namespace AssetBundleBrowser.AssetBundleDataSource
{
    [Serializable]
    public class BuildFolder
    {
        public string Path;
        public string AssetBundleName;
        public bool SingleAssetBundle;
        public string VariantType;
    }


    /// <summary>
    /// Build Info struct used by ABDataSource to pass needed build data around.
    /// </summary>
    public partial struct ABBuildInfo
    {
        /// <summary>
        /// Directory to place build result
        /// </summary>
        public string outputDirectory;
        /// <summary>
        /// Standard asset bundle build options.
        /// </summary>
        public BuildAssetBundleOptions options;
        /// <summary>
        /// Target platform for build.
        /// </summary>
        public BuildTarget buildTarget;
        /// <summary>
        /// Callback for build event.
        /// </summary>
        public Action<string> onBuild;

        public List<BuildFolder> buildFolderList;

        public bool isEncrypt;

        public bool mergeOneFile;

        public string GetExtraOutPutDirectory()
        {
            return outputDirectory + "_Extra";
        }
    }

/// <summary>
/// Interface class used by browser. It is expected to contain all information needed to display predicted bundle layout.
///  Any class deriving from this interface AND imlementing CreateDataSources() will be picked up by the browser automatically
///  and displayed in an in-tool dropdown.  By default, that dropdown is hidden if the browser detects no external data sources.
///  To turn it on, right click on tab header "AssetBUndles" and enable "Custom Sources"
///  
/// Must implement CreateDataSources() to be picked up by the browser.
///   public static List<ABDataSource> CreateDataSources();
/// 
/// </summary>
public partial interface ABDataSource
    {
        //// all derived classes must implement the following interface in order to be picked up by the browser.
        //public static List<ABDataSource> CreateDataSources();

        /// <summary>
        /// Name of DataSource. Displayed in menu as "Name (ProvidorName)"
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Name of provider for DataSource. Displayed in menu as "Name (ProvidorName)"
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Array of paths in bundle.
        /// </summary>
        string[] GetAssetPathsFromAssetBundle (string assetBundleName);
        /// <summary>
        /// Name of bundle explicitly associated with asset at path.  
        /// </summary>
        string GetAssetBundleName(string assetPath);
        /// <summary>
        /// Name of bundle associated with asset at path.  
        ///  The difference between this and GetAssetBundleName() is for assets unassigned to a bundle, but
        ///  residing inside a folder that is assigned to a bundle.  Those assets will implicilty associate
        ///  with the bundle associated with the parent folder.
        /// </summary>
        string GetImplicitAssetBundleName(string assetPath);
        /// <summary>
        /// Array of asset bundle names in project
        /// </summary>
        string[] GetAllAssetBundleNames();
        /// <summary>
        /// If this data source is read only. 
        ///  If this returns true, much of the Browsers's interface will be diabled (drag&drop, etc.)
        /// </summary>
        bool IsReadOnly();

        /// <summary>
        /// Sets the asset bundle name (and variant) on a given asset
        /// </summary>
        void SetAssetBundleNameAndVariant (string assetPath, string bundleName, string variantName);
        /// <summary>
        /// Clears out any asset bundle names that do not have assets associated with them.
        /// </summary>
        void RemoveUnusedAssetBundleNames();

        /// <summary>
        /// Signals if this data source can have build target set by tool
        /// </summary>
        bool CanSpecifyBuildTarget { get; }
        /// <summary>
        /// Signals if this data source can have output directory set by tool
        /// </summary>
        bool CanSpecifyBuildOutputDirectory { get; }
        /// <summary>
        /// Signals if this data source can have build options set by tool
        /// </summary>
        bool CanSpecifyBuildOptions { get; }

        /// <summary>
        /// Executes data source's implementation of asset bundle building.
        ///   Called by "build" button in build tab of tool.
        /// </summary>
        bool BuildAssetBundles (ABBuildInfo info);
    }
}
