#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace PufferSoftware.Aurora.Drive
{
    public class GoogleDriveEditorWindow : EditorWindow
    {
        private DriveService _driveService;
        private const string ROOT_FOLDER_ID = "1G_M3m0m6DBoKRrNr07mAY9iyFV0ALLkx";
        private DriveItem rootFolder;
        private DriveItem currentFolder;
        private Stack<DriveItem> folderStack;
        private bool isAuthorized;
        private Vector2 scrollPos;

        private Dictionary<string, (long downloadedBytes, long totalBytes)> fileDownloadProgress =
            new Dictionary<string, (long, long)>();

        private Dictionary<string, (long downloadedBytes, long totalBytes)> folderDownloadProgress =
            new Dictionary<string, (long, long)>();

        private const float ButtonWidth = 150f; // Increased button width

        [MenuItem("Aurora/Drive")]
        public static void ShowWindow()
        {
            GetWindow<GoogleDriveEditorWindow>("Google Drive");
        }

        private void OnEnable()
        {
            if (_driveService == null)
            {
                AuthorizeGoogleDrive();
            }

            folderStack = new Stack<DriveItem>();
        }

        private void OnGUI()
        {
            if (!isAuthorized)
            {
                if (GUILayout.Button("Authorize Google Drive"))
                {
                    AuthorizeGoogleDrive();
                }
            }
            else
            {
                if (folderStack.Count > 0)
                {
                    if (GUILayout.Button("Back", GUILayout.Width(ButtonWidth)))
                    {
                        currentFolder = folderStack.Pop();
                        DrawFiles(currentFolder);
                    }
                }

                DrawFiles(currentFolder);
            }
        }

        private void AuthorizeGoogleDrive()
        {
            try
            {
                Debug.Log("Starting Authorization...");
                UserCredential credential = GoogleDriveAuth.GetUserCredential();

                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = GoogleDriveAuth.ApplicationName
                });

                rootFolder = new DriveItem("Root", ROOT_FOLDER_ID, "application/vnd.google-apps.folder");
                currentFolder = rootFolder;
                Debug.Log("Authorization successful. Fetching files...");
                ListFilesInDrive(rootFolder, ROOT_FOLDER_ID, "[GAME]");
                isAuthorized = true;
                Debug.Log("Files fetched successfully!");
            }
            catch (Exception e)
            {
                Debug.LogError("Authorization Failed! " + e.Message);
            }
        }

        private void ListFilesInDrive(DriveItem parentItem, string parentId, string currentPath)
        {
            var request = _driveService.Files.List();
            request.Q = $"'{parentId}' in parents and trashed = false";
            request.Fields = "nextPageToken, files(id,name,mimeType,size)";
            request.IncludeItemsFromAllDrives = true;
            request.SupportsAllDrives = true;

            try
            {
                var result = request.Execute();
                Debug.Log($"Found {result.Files.Count} files in folder ID {parentId}");
                foreach (var file in result.Files)
                {
                    Debug.Log($"File : {file.Name} (ID: {file.Id})");
                    var driveItem = new DriveItem(file.Name, file.Id, file.MimeType, file.Size, currentPath);
                    parentItem.AddChild(driveItem);

                    if (file.MimeType == "application/vnd.google-apps.folder")
                    {
                        // Recursively list files in subfolders
                        ListFilesInDrive(driveItem, file.Id, Path.Combine(currentPath, file.Name));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error retrieving files : " + e.Message);
            }
        }

        private void DrawFiles(DriveItem folder)
        {
            EditorGUILayout.LabelField("Files", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(1000));

            List<DriveItem> folders = new List<DriveItem>();
            List<DriveItem> files = new List<DriveItem>();

            foreach (var child in folder.Children)
            {
                if (child.MimeType == "application/vnd.google-apps.folder")
                {
                    folders.Add(child);
                }
                else
                {
                    files.Add(child);
                }
            }

            foreach (var child in folders)
            {
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label(child.Name, GUILayout.Width(200));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Folder", GUILayout.Width(ButtonWidth)))
                {
                    folderStack.Push(currentFolder); // Add current folder to stack before changing
                    currentFolder = child;
                    DrawFiles(currentFolder); // Load contents when folder is opened
                }

                if (folderDownloadProgress.TryGetValue(child.Id, out (long downloadedBytes, long totalBytes) folderProgress))
                {
                    Rect rect = EditorGUILayout.GetControlRect(false, 20, GUILayout.Width(ButtonWidth));
                    EditorGUI.ProgressBar(rect, (float)folderProgress.downloadedBytes / folderProgress.totalBytes, "");
                    GUIStyle progressTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
                    EditorGUI.LabelField(rect, $"{FormatSize(folderProgress.downloadedBytes)} / {FormatSize(folderProgress.totalBytes)}", progressTextStyle);
                }
                else
                {
                    if (GUILayout.Button("Download", GUILayout.Width(ButtonWidth)))
                    {
                        DownloadFolder(child, child.Id);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            foreach (var child in files)
            {
                EditorGUILayout.BeginHorizontal("box");
                GUILayout.Label(child.Name.Split('.')[0], GUILayout.Width(200));

                if (child.Size.HasValue)
                {
                    string sizeText = FormatSize(child.Size.Value);
                    Color sizeColor = GetSizeColor(child.Size.Value);
                    DrawRoundedBox(sizeText, sizeColor, GUILayout.Width(100));
                }

                GUILayout.FlexibleSpace();

                string fileExtension = child.Name.Split('.')[^1].ToUpper();

                Color backgroundColor;

                string updatedExtension = fileExtension;

                Color color;

                switch (fileExtension)
                {
                    case "FBX":
                        ColorUtility.TryParseHtmlString("#EA6A47", out color);
                        backgroundColor = color;
                        break;
                    case "PNG":
                        ColorUtility.TryParseHtmlString("#4CB5F5", out color);
                        backgroundColor = color;
                        break;
                    case "UNİTYPACKAGE":
                        updatedExtension = "UPKG";
                        ColorUtility.TryParseHtmlString("#488A99", out color);
                        backgroundColor = color;
                        break;
                    default:
                        backgroundColor = Color.gray;
                        break;
                }

                DrawRoundedBox(updatedExtension, backgroundColor, GUILayout.Width(50), GUILayout.Height(20));

                GUILayout.FlexibleSpace();

                if (fileDownloadProgress.TryGetValue(child.Id, out (long downloadedBytes, long totalBytes) value))
                {
                    string progressText = $"{FormatSize(value.downloadedBytes)} / {FormatSize(value.totalBytes)}";
                    Rect rect = EditorGUILayout.GetControlRect(false, 20, GUILayout.Width(ButtonWidth));
                    EditorGUI.ProgressBar(rect, (float)value.downloadedBytes / value.totalBytes, "");
                    GUIStyle progressTextStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 10, alignment = TextAnchor.MiddleCenter };
                    EditorGUI.LabelField(rect, progressText, progressTextStyle);
                }
                else
                {
                    if (GUILayout.Button("Download", GUILayout.Width(ButtonWidth)))
                    {
                        StartFileDownload(child.Id, child.Name, child.Size ?? 0, child.Path, fromSingle: true);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private async void DownloadFolder(DriveItem folder, string rootFolderId)
        {
            long totalSize = CalculateTotalSize(folder);
            folderDownloadProgress[rootFolderId] = (0, totalSize);

            await DownloadFolderContents(folder, rootFolderId);

            folderDownloadProgress.Remove(rootFolderId);
            AssetDatabase.Refresh();
        }

        private long CalculateTotalSize(DriveItem folder)
        {
            long totalSize = 0;
            foreach (var child in folder.Children)
            {
                if (child.MimeType == "application/vnd.google-apps.folder")
                {
                    totalSize += CalculateTotalSize(child);
                }
                else
                {
                    totalSize += child.Size ?? 0;
                }
            }

            return totalSize;
        }

        private async Task DownloadFolderContents(DriveItem folder, string rootFolderId)
        {
            foreach (var child in folder.Children)
            {
                if (child.MimeType == "application/vnd.google-apps.folder")
                {
                    await DownloadFolderContents(child, rootFolderId); // Recursively download subfolders
                }
                else
                {
                    await StartFileDownload(child.Id, child.Name, child.Size ?? 0, child.Path, rootFolderId);
                }
            }
        }

        private async Task StartFileDownload(string fileId, string fileName, long totalBytes, string drivePath,
            string rootFolderId = null, bool fromSingle = false)
        {
            string url = $"https://www.googleapis.com/drive/v3/files/{fileId}?alt=media";
            UserCredential credential = GoogleDriveAuth.GetUserCredential();

            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            request.SetRequestHeader("Authorization", "Bearer " + credential.Token.AccessToken);

            string saveDirectory = Path.Combine(Application.dataPath, drivePath);
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }

            string savePath = Path.Combine(saveDirectory, fileName);

            using UnityWebRequest webRequest = UnityWebRequest.Get(url);
            webRequest.SetRequestHeader("Authorization", "Bearer " + credential.Token.AccessToken);

            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SendWebRequest();

            long previousDownloadedBytes = 0;

            while (!webRequest.isDone)
            {
                long downloadedBytes = (long)webRequest.downloadedBytes;

                if (rootFolderId != null && folderDownloadProgress.ContainsKey(rootFolderId))
                {
                    (long folderDownloadedBytes, long folderTotalBytes) = folderDownloadProgress[rootFolderId];
                    folderDownloadedBytes += (downloadedBytes - previousDownloadedBytes);
                    folderDownloadProgress[rootFolderId] = (folderDownloadedBytes, folderTotalBytes);
                }
                else
                {
                    fileDownloadProgress[fileId] = (downloadedBytes, totalBytes);
                }

                previousDownloadedBytes = downloadedBytes;
                Repaint();
                await Task.Delay(100);
            }

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                await File.WriteAllBytesAsync(savePath, webRequest.downloadHandler.data);
                Debug.Log($"File downloaded successfully and saved to {savePath}");
                AssetDatabase.ImportAsset(savePath);
                
                Object obj = AssetDatabase.LoadAssetAtPath<Object>(savePath);
                Selection.activeObject = obj;
                EditorUtility.FocusProjectWindow(); // Focus the Project window
                EditorGUIUtility.PingObject(obj); // Highlight the object

                if (fileName.EndsWith(".unitypackage"))
                {
                    Debug.Log("Save Path : " + savePath);
                    PackageImportUtility.TrackImportedPackage(savePath);
                    AssetDatabase.ImportPackage(savePath, false);
                }

                if (fromSingle)
                {
                    AssetDatabase.Refresh();
                }
            }
            else
            {
                Debug.LogError($"Error downloading file: {webRequest.error}");
            }

            if (rootFolderId != null && folderDownloadProgress.ContainsKey(rootFolderId))
            {
                (long folderDownloadedBytes, long folderTotalBytes) = folderDownloadProgress[rootFolderId];
                folderDownloadedBytes += totalBytes;
                folderDownloadProgress[rootFolderId] = (folderDownloadedBytes, folderTotalBytes);
            }
            else
            {
                fileDownloadProgress.Remove(fileId);
            }

            Repaint();
        }

        private string FormatSize(long size)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = size;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private Color GetSizeColor(long size)
        {
            Color color;
            switch (size)
            {
                // 1 MB
                case < 1 * 1024 * 1024:
                    ColorUtility.TryParseHtmlString("#6AA84F", out color);
                    return color;
                // 10 MB
                case < 10 * 1024 * 1024:
                    ColorUtility.TryParseHtmlString("#EFB61F", out color);
                    return color;
                default:
                    ColorUtility.TryParseHtmlString("#9D3D43", out color);
                    return color;
            }
        }

        private void DrawRoundedBox(string text, Color backgroundColor, params GUILayoutOption[] options)
        {
            GUIStyle style = GetRoundedBoxStyle(backgroundColor);
            GUILayout.Box(text, style, options);
        }

        private GUIStyle GetRoundedBoxStyle(Color backgroundColor)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, backgroundColor);
            tex.Apply();

            style.normal.background = tex;
            style.border = new RectOffset(10, 10, 10, 10);
            style.margin = new RectOffset(0, 0, 0, 0); // Adjusted margin
            style.padding = new RectOffset(1, 1, 1, 1); // Adjusted padding

            return style;
        }
    }
}
#endif