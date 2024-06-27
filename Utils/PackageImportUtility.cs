#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PufferSoftware.Aurora.Drive
{
    public static class PackageImportUtility
    {
        private static readonly List<string> ImportedPackages = new();

        static PackageImportUtility()
        {
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
        }

        public static void TrackImportedPackage(string packagePath)
        {
            if (File.Exists(packagePath))
            {
                ImportedPackages.Add(packagePath);
            }
        }

        private static void OnImportPackageCompleted(string packageName)
        {
            List<string> packagesToRemove = new List<string>();

            foreach (var packagePath in ImportedPackages)
            {
                if (File.Exists(packagePath))
                {
                    File.Delete(packagePath);
                    Debug.Log($"Deleted imported package: {packagePath}");
                    packagesToRemove.Add(packagePath);
                }
            }

            foreach (var packagePath in packagesToRemove)
            {
                ImportedPackages.Remove(packagePath);
            }
            
            AssetDatabase.Refresh();
        }
    }
}
#endif