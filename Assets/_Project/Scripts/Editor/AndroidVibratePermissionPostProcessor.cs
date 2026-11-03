#if UNITY_EDITOR
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

public class AndroidVibratePermissionPostProcessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 999;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        string searchRoot = path;
        try
        {
            var parent = Directory.GetParent(path);
            if (parent != null && Directory.Exists(parent.FullName))
                searchRoot = parent.FullName;
        }
        catch { }

        string[] manifests = Directory.GetFiles(searchRoot, "AndroidManifest.xml", SearchOption.AllDirectories);
        foreach (string manifestPath in manifests)
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(manifestPath);

                XmlNode manifestNode = doc.SelectSingleNode("/manifest");
                if (manifestNode == null)
                    continue;

                bool hasVibrate = false;
                foreach (XmlNode child in manifestNode.ChildNodes)
                {
                    if (child.Name == "uses-permission")
                    {
                        var nameAttr = child.Attributes?["android:name"];
                        if (nameAttr != null && nameAttr.Value == "android.permission.VIBRATE")
                        {
                            hasVibrate = true;
                            break;
                        }
                    }
                }

                if (!hasVibrate)
                {
                    XmlElement permission = doc.CreateElement("uses-permission");
                    permission.SetAttribute("name", "http://schemas.android.com/apk/res/android", "android.permission.VIBRATE");
                    manifestNode.AppendChild(permission);
                    doc.Save(manifestPath);
                    Debug.Log($"[QuestAR] Added android.permission.VIBRATE to {manifestPath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[QuestAR] Failed to patch Android manifest at {manifestPath}: {ex.Message}");
            }
        }
    }
}
#endif
