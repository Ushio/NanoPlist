using UnityEngine;
using NanoPlist;
using System.IO;
public class HelloWorld : MonoBehaviour {
	void Start () {
        var bytes_binary = Plist.WriteObjectBinary(Arbitrary.Plist());
        File.WriteAllBytes(Path.Combine(Application.streamingAssetsPath, "arbitrary-binary.plist"), bytes_binary);
        var bytes_xml = Plist.WriteObjectXML(Arbitrary.Plist());
        File.WriteAllBytes(Path.Combine(Application.streamingAssetsPath, "arbitrary-xml.plist"), bytes_xml);
	}
}
