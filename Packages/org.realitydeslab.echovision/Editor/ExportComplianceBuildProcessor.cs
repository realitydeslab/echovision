#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// EchoVision uses only exempt encryption (HTTPS and the OS's own), so the exported Info.plist says
// so: App Store Connect then does not ask for export compliance on every uploaded build.
public static class ExportComplianceBuildProcessor
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target == BuildTarget.iOS)
            DeclareExemptEncryption(Path.Combine(buildPath, "Info.plist"));
    }

    public static void DeclareExemptEncryption(string plistPath)
    {
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        File.WriteAllText(plistPath, plist.WriteToString());
    }
}
#endif
