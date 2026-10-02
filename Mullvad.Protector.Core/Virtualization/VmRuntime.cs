using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;

namespace Mullvad.Protector.Core.Virtualization;

// This class is compiled as part of the protector and then COPIED into the
// target assembly via dnlib Importer.  No Mullvad.Protector.* references may
// appear in method bodies — only BCL types survive the module copy.
internal static class VmRuntime
{
    internal static byte[][]?   _c;   // XOR-encrypted IL per id
    internal static int[]?      _ms;  // maxStack per id
    internal static string[][]? _tk;  // token tables per id
    internal static string[][]? _sg;  // signatures per id: [0]=ret, [1..n]=params
    internal static byte[]?     _vk;  // 16-byte XOR key
    internal static Delegate?[]? _ch; // delegate cache

    internal static void Init(byte[][] c, int[] ms, string[][] tk, string[][] sg, byte[] vk)
    {
        _c = c; _ms = ms; _tk = tk; _sg = sg; _vk = vk;
        _ch = new Delegate?[c.Length];
        VrLog("Init ok — methods=" + c.Length);
    }

    internal static object? R(int id, object?[] args)
    {
        var d = _ch![id];
        if (d == null) { d = Bld(id); _ch[id] = d; }
        return d.DynamicInvoke(args);
    }

    private static void VrLog(string msg)
    {
        string line = "[VR " + System.DateTime.Now.ToString("HH:mm:ss.fff") + "] " + msg + "\r\n";
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "log.txt"),
                line);
        }
        catch { }
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mullvad_vr.txt"),
                line);
        }
        catch { }
    }

    private static string HexChars(string s, int max)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length && i < max; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(((int)s[i]).ToString("X4"));
        }
        return sb.ToString();
    }

    private static Delegate Bld(int id)
    {
        byte[] enc = _c![id];
        byte[] il  = Xor(enc, id);
        string[] sg = _sg![id];

        VrLog("Bld id=" + id + " sg.len=" + sg.Length + " tk.len=" + _tk![id].Length + " il.len=" + il.Length);

        Type? ret = sg[0].Length == 0 ? null : Lkt(sg[0]);
        var ps = new Type[sg.Length - 1];
        for (int i = 0; i < ps.Length; i++) ps[i] = Lkt(sg[i + 1]);

        var dm = new DynamicMethod(
            "",
            ret,
            ps.Length == 0 ? null : ps,
            typeof(object).Module,
            skipVisibility: true);

        var di = dm.GetDynamicILInfo();

        int teIdx = 0;
        foreach (string te in _tk![id])
        {
            int s1 = te.IndexOf('\x01');
            if (s1 < 0)
            {
                VrLog("id=" + id + " te[" + teIdx + "] no-sep len=" + te.Length +
                    (te.Length > 0 ? " chars=" + HexChars(te, 8) : ""));
                throw new System.InvalidOperationException("vr-nosep id=" + id + " te=" + teIdx);
            }
            string offStr = te[..s1];
            if (!int.TryParse(offStr, out int off))
            {
                VrLog("id=" + id + " te[" + teIdx + "] parse-fail offStr.len=" + offStr.Length +
                    (offStr.Length > 0 ? " offChars=" + HexChars(offStr, 8) : "") +
                    " te.len=" + te.Length + " teChars=" + HexChars(te, 16));
                throw new System.InvalidOperationException("vr-parse id=" + id + " te=" + teIdx);
            }
            int s2 = te.IndexOf('\x01', s1 + 1);
            string kind = te[(s1 + 1)..s2];
            string val  = te[(s2 + 1)..];
            VrLog("id=" + id + " te[" + teIdx + "] off=" + off + " kind=" + kind + " val.len=" + val.Length);
            int tok = Gt(di, kind, val);
            il[off]     = (byte)tok;
            il[off + 1] = (byte)(tok >> 8);
            il[off + 2] = (byte)(tok >> 16);
            il[off + 3] = (byte)(tok >> 24);
            teIdx++;
        }

        di.SetLocalSignature(new byte[] { 0x07, 0x00 });
        di.SetCode(il, _ms![id]);

        System.Array.Clear(il, 0, il.Length);

        Type dt = Mdt(ret, ps);
        return dm.CreateDelegate(dt);
    }

    private static byte[] Xor(byte[] d, int id)
    {
        byte[] vk = _vk!;
        var r = new byte[d.Length];
        for (int i = 0; i < d.Length; i++)
            r[i] = (byte)(d[i] ^ vk[i % 16] ^ ((id ^ i) & 0xFF));
        return r;
    }

    private static Type Lkt(string n)
    {
        if (n == "System.Void") return typeof(void);
        var t = Type.GetType(n, false);
        if (t != null) return t;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        { var tt = a.GetType(n, false); if (tt != null) return tt; }
        throw new TypeLoadException("VR:" + n);
    }

    private static int Gt(DynamicILInfo di, string kind, string val)
    {
        if (kind == "S") return di.GetTokenFor(val);

        if (kind == "T") return di.GetTokenFor(Lkt(val).TypeHandle);

        if (kind == "M")
        {
            int i1 = val.IndexOf('\x02'); string tn = val[..i1];
            int i2 = val.IndexOf('\x02', i1 + 1); string mn = val[(i1 + 1)..i2];
            string rest = val[(i2 + 1)..];
            string[] pts = rest.Length == 0 ? [] : rest.Split('\x02');
            var t = Lkt(tn);
            int paramStart = 1;
            var ps = pts.Length <= paramStart ? Array.Empty<Type>() : new Type[pts.Length - paramStart];
            for (int i = 0; i < ps.Length; i++) ps[i] = Lkt(pts[i + paramStart]);
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic |
                                    BindingFlags.Instance | BindingFlags.Static |
                                    BindingFlags.FlattenHierarchy;
            if (mn == ".ctor" || mn == ".cctor")
            {
                foreach (var c in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    if (Mp(c, ps)) return di.GetTokenFor(c.MethodHandle);
            }
            else
            {
                foreach (var m in t.GetMethods(bf))
                    if (m.Name == mn && Mp(m, ps)) return di.GetTokenFor(m.MethodHandle);
            }
            throw new MissingMethodException("VR:M:" + val);
        }

        if (kind == "F")
        {
            int ix = val.IndexOf('\x02');
            var fi = Lkt(val[..ix]).GetField(val[(ix + 1)..],
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);
            if (fi != null) return di.GetTokenFor(fi.FieldHandle);
            throw new MissingFieldException("VR:F:" + val);
        }

        return 0;
    }

    private static bool Mp(MethodBase m, Type[] pt)
    {
        var ps = m.GetParameters();
        if (ps.Length != pt.Length) return false;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].ParameterType != pt[i]) return false;
        return true;
    }

    private static Type Mdt(Type? ret, Type[] ps)
    {
        if (ret == null || ret == typeof(void))
        {
            if (ps.Length == 0) return typeof(Action);
            return Type.GetType($"System.Action`{ps.Length}")!.MakeGenericType(ps);
        }
        var all = new Type[ps.Length + 1];
        ps.CopyTo(all, 0);
        all[ps.Length] = ret;
        return Type.GetType($"System.Func`{all.Length}")!.MakeGenericType(all);
    }
}
