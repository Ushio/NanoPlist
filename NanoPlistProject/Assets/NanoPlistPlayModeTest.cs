using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;

using NanoPlist;

public class Xorshift
{
    public Xorshift()
    {
    }
    public Xorshift(uint seed)
    {
        _y = System.Math.Max(seed, 1u);
    }

    // 0 <= x <= 0x7FFFFFFF (int.MaxValue
    public uint Generate()
    {
        _y = _y ^ (_y << 13);
        _y = _y ^ (_y >> 17);
        uint value = _y = _y ^ (_y << 5); // 1 ~ 0xFFFFFFFF(4294967295
        return value >> 1;
    }
    // 0.0 <= x < 1.0
    public double Uniform()
    {
        return (double)(Generate()) / (double)(0x80000000);
    }
    public double Uniform(double a, double b)
    {
        return a + (b - a) * Uniform();
    }

    private uint _y = 2463534242;
}

public static class Arbitrary {
	private static Xorshift random = new Xorshift();

    private static readonly string TextASCII = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
	private static readonly string TextJa = "あのイーハトーヴォのすきとおった風、夏でも底に冷たさをもつ青いそら、うつくしい森で飾られたモリーオ市、郊外のぎらぎらひかる草の波。";

	public static string Text() {
		int n = (int)random.Generate() % 1000;

		var text = new List<char>();
		for(int i = 0 ; i < n ; ++i) {
			if(random.Uniform() < 0.5) {
				var c = TextASCII[(int)random.Generate() % TextASCII.Length];
				text.Add(c);
			} else {
				var c = TextJa[(int)random.Generate() % TextJa.Length];
				text.Add(c);
			}
		}
		return new string(text.ToArray());
	}

	public static byte[] BinaryData() {
		int n = (int)random.Generate() % 1000;
		var bytes = new byte[n];
		for(int i = 0 ; i < n ; ++i) {
			bytes[i] = (byte)(random.Generate() % 255);
		}
		return bytes;
	}
    public static byte[] BinaryDataWithSize(int size)
    {
        var bytes = new byte[size];
        var n = size / 8;

        for (int i = 0; i < n; ++i)
        {
            var index = i * 8;
            var src = random.Generate();
            var a = (byte)(src & 0xff);
            var b = (byte)((src >> 8) & 0xff);
            var c = (byte)((src >> 16) & 0xff);
            var d = (byte)((src >> 24) & 0xff);
            bytes[index + 0] = a;
            bytes[index + 1] = b;
            bytes[index + 2] = c;
            bytes[index + 3] = d;
            bytes[index + 4] = b;
            bytes[index + 5] = a;
            bytes[index + 6] = c;
            bytes[index + 7] = d;
        }
        for(int i = n ; i < size ; ++i) {
            bytes[i] = (byte)(random.Generate() & 0xff);
        }

        return bytes;
    }
	public static object PlistValue () {
		int type = (int)random.Generate() % 6;
		switch(type) {
		case 0:
            // string
            return Text();
		case 1:
		    // int
			var v = (int)random.Generate();
			return random.Uniform() < 0.5 ? -v : v;
		case 2:
		    // bytes
            return BinaryData();
		case 3:
			// boolean
			return random.Uniform() < 0.5 ? true : false;
        case 4:
            // double
            return random.Uniform(-1.0+6, 1.0+6);
        case 5:
            // date
            return DateTime.Now.Add(TimeSpan.FromSeconds(random.Uniform(-1.0e+6, 1.0e+6)));
		default:
			Debug.Assert(false);
			return null;
		}
	}
	public static object Plist(int depth = 4) {
		if(depth <= 0) {
			return PlistValue();
		}
		var u = random.Uniform();
		if(u < 0.3) {
			// dictionary
			int nElement = (int)random.Generate() % 10;
			var dictionary = new Dictionary<string, object>(nElement /*capacity*/);
			for(int i = 0 ; i < nElement ; ++i) {
				dictionary[Text()] = Plist(depth - 1);
			}
			return dictionary;
		} else if(u < 0.6) {
			// array
			int nElement = (int)random.Generate() % 10;
			var array = new List<object>(nElement  /*capacity*/);
			for(int i = 0 ; i < nElement ; ++i) {
				array.Add(Plist(depth - 1));
			}
			return array;
		} 
		return PlistValue(); 
	}
}


#if UNITY_STANDALONE_OSX

public static class OSXPlist
{
    private static readonly string Binary = "binary1";
    private static readonly string Xml = "xml1";
    public static void ConvertToXml(string filePath)
    {
        ConvertTo(filePath, Xml);
    }
    public static void ConvertToBinary(string filePath)
    {
        ConvertTo(filePath, Binary);
    }
    private static void ConvertTo(string filePath, string convertTo)
    {
        var psi = new System.Diagnostics.ProcessStartInfo();
        psi.FileName = "/usr/bin/plutil";
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        var arg = string.Format("-convert {0} \"{1}\"", convertTo, filePath);
        psi.Arguments = arg;
        var p = System.Diagnostics.Process.Start(psi);
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
    }
}

#endif

public class NanoPlistPlayModeTest {
    public static readonly uint Seed = 1000;

    [Test]
    public void FloatBits_TEST() {
        Xorshift random = new Xorshift(Seed);
        for(int i = 0 ; i < 100000 ; ++i) {
            double value = random.Uniform(-1.0e+9, 1.0e+9);
            var enc = new Float64Bits(value);

            var ms = new MemoryStream();
            enc.Write(ms);

            var dec = new Float64Bits(ms.ToArray(), 0);

            // perfect equal
            Assert.AreEqual(dec.Value, value);
        }

        for (int i = 0; i < 100000; ++i)
        {
            float value = (float)random.Uniform(-1.0e+6, 1.0e+6);

            var enc = new Float32Bits(value);

            var ms = new MemoryStream();
            enc.Write(ms);

            var dec = new Float32Bits(ms.ToArray(), 0);

            // perfect equal
            Assert.AreEqual(dec.Value, value);
        }
    }


	[Test]
    public void BigEndianIO_TEST() {
        for (int i = 0; i <= ushort.MaxValue; ++i) {
             ushort value = (ushort)i;
             var ms = new MemoryStream();
             BigEndianWriter.WriteUShort(ms, value);
             ushort read1 = BigEndianReader.ReadUShort(ms.ToArray(), 0);
             Assert.AreEqual(read1, value);

             ulong read2 = BigEndianReader.ReadNBytesUnsignedInteger(ms.ToArray(), 2, 0);
             Assert.IsTrue(read2 <= ushort.MaxValue);
             Assert.AreEqual((ushort)read2, value);
        }

        Xorshift random = new Xorshift(Seed);

        for (int i = 0; i < 100000; ++i) {
             uint value = random.Generate();
             var ms = new MemoryStream();
             BigEndianWriter.WriteUInt(ms, value);
             uint read1 = BigEndianReader.ReadUInt(ms.ToArray(), 0);
             Assert.AreEqual(read1, value);

             ulong read2 = BigEndianReader.ReadNBytesUnsignedInteger(ms.ToArray(), 4, 0);
             Assert.IsTrue(read2 <= uint.MaxValue);
             Assert.AreEqual((uint)read2, value);
        }

        for (int i = 0; i < 100000; ++i) {
             ulong value = (ulong)random.Generate() | ((ulong)random.Generate() << 32);
             var ms = new MemoryStream();
             BigEndianWriter.WriteULong(ms, value);
             ulong read1 = BigEndianReader.ReadULong(ms.ToArray(), 0);
             Assert.AreEqual(read1, value);

             ulong read2 = BigEndianReader.ReadNBytesUnsignedInteger(ms.ToArray(), 8, 0);
             Assert.AreEqual(read2, value);
        }
	}

    [Test]
    public void ObjectEquality_TEST() {
        Xorshift random = new Xorshift(Seed);
        double delta = 1.0e-6;
        double deltaDateSeconds = 1.0e-3;

        // bool
        Assert.IsTrue(Plist.EqualObject(true, true, delta, deltaDateSeconds));
        Assert.IsTrue(Plist.EqualObject(false, false, delta, deltaDateSeconds));
        Assert.IsTrue(Plist.EqualObject(true, false, delta, deltaDateSeconds) == false);
        Assert.IsTrue(Plist.EqualObject(true, false, delta, deltaDateSeconds) == false);

        for (int i = 0; i < 100000; ++i) {
            int value1 = (int)random.Generate() * (random.Uniform() < 0.5 ? 1 : -1);
            int value2 = (int)random.Generate() * (random.Uniform() < 0.5 ? 1 : -1);
            Assert.IsTrue(Plist.EqualObject(value1, value1, delta, deltaDateSeconds));
            Assert.IsTrue(Plist.EqualObject(value1, value2, delta, deltaDateSeconds) == (value1 == value2));
            Assert.IsTrue(Plist.EqualObject(value1, value2, delta, deltaDateSeconds) == Plist.EqualObject(value2, value1, delta, deltaDateSeconds));
        }

        for (int i = 0; i < 100000; ++i) {
            double value1 = random.Uniform(-1.0e+9, 1.0e+9);
            Assert.IsTrue(Plist.EqualObject(value1, value1, delta, deltaDateSeconds));
            Assert.IsTrue(Plist.EqualObject(value1, value1 + random.Uniform(0.1, 1000.0), delta, deltaDateSeconds) == false);
            Assert.IsTrue(Plist.EqualObject(value1, value1 - random.Uniform(0.1, 1000.0), delta, deltaDateSeconds) == false);
        }

        for (int i = 0; i < 100000; ++i) {
            DateTime value1 = DateTime.Now.Add(TimeSpan.FromSeconds(random.Uniform(-1.0e+6, 1.0e+6)));
            Assert.IsTrue(Plist.EqualObject(value1, value1, delta, deltaDateSeconds));
            Assert.IsTrue(Plist.EqualObject(value1, value1.AddSeconds(random.Uniform(0.1, 1000.0)), delta, deltaDateSeconds) == false);
            Assert.IsTrue(Plist.EqualObject(value1, value1.AddSeconds(-random.Uniform(0.1, 1000.0)), delta, deltaDateSeconds) == false);
        }

        for (int i = 0; i < 10000; ++i) {
            string value1 = Arbitrary.Text();
            string value2 = Arbitrary.Text();
            Assert.IsTrue(Plist.EqualObject(value1, value1, delta, deltaDateSeconds));
            Assert.IsTrue(Plist.EqualObject(value1 + value2, value1 + value2, delta, deltaDateSeconds));
            Assert.IsTrue(Plist.EqualObject(value1, value1 + "a", delta, deltaDateSeconds) == false);
            Assert.IsTrue(Plist.EqualObject(value1, value2, delta, deltaDateSeconds) == Plist.EqualObject(value2, value1, delta, deltaDateSeconds));
            Assert.IsTrue(Plist.EqualObject(value1, value2, delta, deltaDateSeconds) == (value2 == value1));
        }

        for (int i = 0; i < 10000; ++i) {
            byte[] value1 = Arbitrary.BinaryData();
            byte[] valueA = value1.Concat(new byte[]{ (byte)(random.Generate() & 0xFF) }).ToArray();
            byte[] valueB = value1.Concat(new byte[]{ (byte)(random.Generate() & 0xFF) }).ToArray();

            Assert.IsTrue(Plist.EqualObject(valueA, valueB, delta, deltaDateSeconds) == Plist.EqualObject(valueB, valueA, delta, deltaDateSeconds));
            Assert.IsTrue(Plist.EqualObject(value1, valueA, delta, deltaDateSeconds) == false);
        }
        
        for (int i = 0; i < 10000; ++i) {
            List<object> value1 = new List<object>();
            int n = (int)random.Generate() % 100;
            for(int j = 0 ; j < n ; ++j) {
                value1.Add(Arbitrary.PlistValue());
            }
            Assert.IsTrue(Plist.EqualObject(value1, value1, delta, deltaDateSeconds));
            
            // Insert Test
            List<object> value2 = new List<object>(value1);
            value2.Insert((int)random.Generate() % (value1.Count + 1), Arbitrary.PlistValue());
            Assert.IsTrue(Plist.EqualObject(value1, value2, delta, deltaDateSeconds) == false);

            // Modify Test
            if(n > 0) {
                List<object> value3 = new List<object>(value1);
                int index = (int)random.Generate() % value1.Count;
                object newValue = Arbitrary.PlistValue();
                object oldValue = value3[index];
                value3[index] = newValue;
                if(Plist.EqualObject(oldValue, newValue, delta, deltaDateSeconds)) {
                    Assert.IsTrue(Plist.EqualObject(value1, value3, delta, deltaDateSeconds));
                } else {
                    Assert.IsTrue(Plist.EqualObject(value1, value3, delta, deltaDateSeconds) == false);
                }
            }
        }

        for (int i = 0; i < 10000; ++i) {
            Dictionary<string, object> value1 = new Dictionary<string, object>();
            int n = (int)random.Generate() % 100;
            for(int j = 0 ; j < n ; ++j) {
                value1[Arbitrary.Text()] = Arbitrary.PlistValue();
            }
            Assert.IsTrue(Plist.EqualObject(value1, value1, delta, deltaDateSeconds));

            // Add Test
            Dictionary<string, object> value2 = new Dictionary<string, object>(value1);
            var newKey = Arbitrary.Text();
            if(value2.ContainsKey(newKey) == false) {
                value2[newKey] = Arbitrary.PlistValue();
                Assert.IsTrue(Plist.EqualObject(value1, value2, delta, deltaDateSeconds) == false);
            }

            // Modify Test
            Dictionary<string, object> value3 = new Dictionary<string, object>(value1);
            if (n > 0)
            {
                var key = value3.Keys.ToArray()[(int)random.Generate() % value3.Count];
                object newValue = Arbitrary.PlistValue();
                object oldValue = value3[key];
                value3[key] = newValue;

                if (Plist.EqualObject(oldValue, newValue, delta, deltaDateSeconds))
                {
                    Assert.IsTrue(Plist.EqualObject(value1, value3, delta, deltaDateSeconds));
                }
                else
                {
                    Assert.IsTrue(Plist.EqualObject(value1, value3, delta, deltaDateSeconds) == false);
                }
            }
        }
    }

    [Test]
    public void Other_TEST()
    {
        {
            object x = 5;
            object y = 5;
            Assert.IsTrue(x.Equals(y));
        }
        {
            object x = 5;
            object y = 6;
            Assert.IsTrue(x.Equals(y) == false);
        }
    }
    [Test]
    public void Plist_Binary_TEST() {
        Xorshift random = new Xorshift(Seed);

        // Trailer
        for (int i = 0; i < 100000; ++i) {
            var trailer = new Plist.Trailer();
            trailer.ShortVersion = (byte)(random.Generate() & 0xFF);
            trailer.OffsetIntSize = (byte)(random.Generate() & 0xFF);
            trailer.ObjectRefSize = (byte)(random.Generate() & 0xFF);
            trailer.NumObjects = (ulong)random.Generate() | ((ulong)random.Generate() << 32);
            trailer.TopObject = (ulong)random.Generate() | ((ulong)random.Generate() << 32);
            trailer.OffsetTableOffset = (ulong)random.Generate() | ((ulong)random.Generate() << 32);

            var ms = new MemoryStream();
            Plist.WriteTrailerBinary(ms, trailer);
            var read = Plist.ReadTrailerBinary(ms.ToArray());
            Assert.AreEqual(trailer.GetHashCode(), read.GetHashCode());
            Assert.AreEqual(trailer, read);
        }

        for (int i = 0; i < 5000; ++i) {
            object value1 = Arbitrary.Plist();
            var data = Plist.WriteObjectBinary(value1);
            object value2 = Plist.ReadObjectBinary(data);
            Assert.IsTrue(Plist.EqualObject(value1, value2, 1.0e-9, 1.0e-3), string.Format("serialize error [{0}]", i));
        }
    }

    [Test]
    public void Plist_XML_TEST()
    {
        Xorshift random = new Xorshift(Seed);

        for (int i = 0; i < 1000; ++i)
        {
            object value1 = Arbitrary.Plist();
            var data = Plist.WriteObjectXML(value1);
            object value2 = Plist.ReadObjectXML(data);
            Assert.IsTrue(Plist.EqualObject(value1, value2, 1.0e-9, 1.5), string.Format("serialize error [{0}]", i));
        }
    }

#if UNITY_STANDALONE_OSX
    [Test]
    public void Plist_OSXCompatibilityTEST()
    {
        var path = Path.Combine(Application.streamingAssetsPath, "tmp.plist");
        for (int i = 0; i < 100; ++i)
        {
            object value1 = Arbitrary.Plist(3);

            var bytes = Plist.WriteObjectBinary(value1);
            File.WriteAllBytes(path, bytes);

            OSXPlist.ConvertToXml(path);
            OSXPlist.ConvertToBinary(path);

            bytes = File.ReadAllBytes(path);
            File.Delete(path);

            var value2 = Plist.ReadObjectBinary(bytes);

            // because of xml serialization, lost milisecond acculacy...
            Assert.IsTrue(Plist.EqualObject(value1, value2, 1.0e-9, 1.5), string.Format("serialize error [{0}]", i));
        }

        for (int i = 0; i < 100; ++i)
        {
            object value1 = Arbitrary.Plist(3);

            var bytes = Plist.WriteObjectBinary(value1);
            File.WriteAllBytes(path, bytes);

            OSXPlist.ConvertToXml(path);

            bytes = File.ReadAllBytes(path);
            File.Delete(path);

            var value2 = Plist.ReadObjectXML(bytes);

            // because of xml serialization, lost milisecond acculacy...
            Assert.IsTrue(Plist.EqualObject(value1, value2, 1.0e-9, 1.5), string.Format("serialize error [{0}]", i));
        }

        for (int i = 0; i < 100; ++i)
        {
            object value1 = Arbitrary.Plist(3);

            var bytes = Plist.WriteObjectXML(value1);
            File.WriteAllBytes(path, bytes);

            OSXPlist.ConvertToBinary(path);

            bytes = File.ReadAllBytes(path);
            File.Delete(path);

            var value2 = Plist.ReadObjectBinary(bytes);

            // because of xml serialization, lost milisecond acculacy...
            Assert.IsTrue(Plist.EqualObject(value1, value2, 1.0e-9, 1.5), string.Format("serialize error [{0}]", i));
        }
    }
#endif
}
