#if UNITY_IOS
using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.iOS.Xcode;

// EchoVision uses only exempt encryption, declared in Info.plist so App Store Connect does not ask
// for export compliance on every uploaded build.
public class ExportComplianceTests
{
    [Test] public void IosBuild_DeclaresNoNonExemptEncryption()
    {
        string path = Path.Combine(Path.GetTempPath(), $"Info-{Guid.NewGuid():N}.plist");
        var plist = new PlistDocument();
        plist.root.SetString("CFBundleName", "EchoVision");
        File.WriteAllText(path, plist.WriteToString());
        try
        {
            Type.GetType("ExportComplianceBuildProcessor, EchoVision.Editor", true)
                .GetMethod("DeclareExemptEncryption").Invoke(null, new object[] { path });
            var result = new PlistDocument();
            result.ReadFromFile(path);
            Assert.That(result.root["ITSAppUsesNonExemptEncryption"]?.AsBoolean(), Is.False);
            Assert.That(result.root["CFBundleName"].AsString(), Is.EqualTo("EchoVision"), "other keys kept");
        }
        finally { File.Delete(path); }
    }
}
#endif
