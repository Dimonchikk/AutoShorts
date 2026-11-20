using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace AutoShorts
{
    /// <summary>Невеликі обгортки над вбудованим JavaScriptSerializer.</summary>
    static class Json
    {
        static JavaScriptSerializer Make()
        {
            var s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 1000;
            return s;
        }

        public static object Parse(string text) { return Make().DeserializeObject(text); }
        public static T Deserialize<T>(string text) { return Make().Deserialize<T>(text); }
        public static string Serialize(object o) { return Make().Serialize(o); }

        public static object Get(object o, string key)
        {
            var d = o as IDictionary<string, object>;
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v;
            return null;
        }

        public static object Path(object o, params string[] keys)
        {
            foreach (var k in keys) { o = Get(o, k); if (o == null) return null; }
            return o;
        }

        public static string Str(object o, string key)
        {
            var v = Get(o, key);
            return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static double Num(object o, string key, double def)
        {
            var v = Get(o, key);
            if (v == null) return def;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return def; }
        }

        public static object[] Arr(object o, string key)
        {
            return AsArr(Get(o, key));
        }

        public static object[] AsArr(object v)
        {
            var a = v as object[];
            if (a != null) return a;
            var l = v as ArrayList;
            if (l != null) return l.ToArray();
            return new object[0];
        }

        public static string Escape(string s)
        {
            return Make().Serialize(s ?? "");
        }
    }
}
