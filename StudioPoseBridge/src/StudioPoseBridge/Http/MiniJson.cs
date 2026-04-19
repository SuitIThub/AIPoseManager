// Compact JSON read/write for plugin HTTP payloads (subset used by Pose Bridge).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StudioPoseBridge.Http
{
    internal static class MiniJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return new JsonParser(json).ParseValue();
        }

        public static string Serialize(object obj)
        {
            var w = new JsonWriter();
            w.Write(obj);
            return w.ToString();
        }

        private sealed class JsonParser
        {
            private readonly string _json;
            private int _i;

            public JsonParser(string json)
            {
                _json = json;
            }

            public object ParseValue()
            {
                SkipWs();
                if (_i >= _json.Length) throw new Exception("Unexpected end of JSON");
                var c = _json[_i];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == 't') { Expect("true"); return true; }
                if (c == 'f') { Expect("false"); return false; }
                if (c == 'n') { Expect("null"); return null; }
                return ParseNumber();
            }

            private Dictionary<string, object> ParseObject()
            {
                _i++;
                var d = new Dictionary<string, object>();
                SkipWs();
                while (_i < _json.Length && _json[_i] != '}')
                {
                    SkipWs();
                    var key = ParseString();
                    SkipWs();
                    if (_i >= _json.Length || _json[_i] != ':') throw new Exception("Expected ':'");
                    _i++;
                    var val = ParseValue();
                    d[key] = val;
                    SkipWs();
                    if (_i < _json.Length && _json[_i] == ',') { _i++; continue; }
                    break;
                }
                if (_i >= _json.Length || _json[_i] != '}') throw new Exception("Expected '}'");
                _i++;
                return d;
            }

            private List<object> ParseArray()
            {
                _i++;
                var a = new List<object>();
                SkipWs();
                while (_i < _json.Length && _json[_i] != ']')
                {
                    a.Add(ParseValue());
                    SkipWs();
                    if (_i < _json.Length && _json[_i] == ',') { _i++; SkipWs(); continue; }
                    break;
                }
                if (_i >= _json.Length || _json[_i] != ']') throw new Exception("Expected ']'");
                _i++;
                return a;
            }

            private string ParseString()
            {
                if (_json[_i] != '"') throw new Exception("Expected string");
                _i++;
                var sb = new StringBuilder();
                while (_i < _json.Length)
                {
                    var c = _json[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (_i >= _json.Length) throw new Exception("Bad escape");
                    var e = _json[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _json.Length) throw new Exception("Bad \\u escape");
                            var hex = _json.Substring(_i, 4);
                            _i += 4;
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber));
                            break;
                        default: throw new Exception("Unknown escape");
                    }
                }
                throw new Exception("Unterminated string");
            }

            private object ParseNumber()
            {
                var start = _i;
                if (_json[_i] == '-') _i++;
                while (_i < _json.Length && char.IsDigit(_json[_i])) _i++;
                if (_i < _json.Length && _json[_i] == '.')
                {
                    _i++;
                    while (_i < _json.Length && char.IsDigit(_json[_i])) _i++;
                }
                if (_i < _json.Length && (_json[_i] == 'e' || _json[_i] == 'E'))
                {
                    _i++;
                    if (_i < _json.Length && (_json[_i] == '+' || _json[_i] == '-')) _i++;
                    while (_i < _json.Length && char.IsDigit(_json[_i])) _i++;
                }
                var s = _json.Substring(start, _i - start);
                if (s.IndexOf('.') >= 0 || s.IndexOf('e') >= 0 || s.IndexOf('E') >= 0)
                    return double.Parse(s, CultureInfo.InvariantCulture);
                long l;
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
                    return l;
                return double.Parse(s, CultureInfo.InvariantCulture);
            }

            private void Expect(string lit)
            {
                if (_i + lit.Length > _json.Length) throw new Exception("Expected " + lit);
                for (var j = 0; j < lit.Length; j++)
                    if (_json[_i + j] != lit[j]) throw new Exception("Expected " + lit);
                _i += lit.Length;
            }

            private void SkipWs()
            {
                while (_i < _json.Length && char.IsWhiteSpace(_json[_i])) _i++;
            }
        }

        private sealed class JsonWriter
        {
            private readonly StringBuilder _b = new StringBuilder();

            public override string ToString()
            {
                return _b.ToString();
            }

            public void Write(object obj)
            {
                if (obj == null) { _b.Append("null"); return; }
                if (obj is bool bo) { _b.Append(bo ? "true" : "false"); return; }
                if (obj is string str) { WriteString(str); return; }
                if (obj is int || obj is long || obj is short || obj is byte)
                {
                    _b.Append(Convert.ToInt64(obj).ToString(CultureInfo.InvariantCulture));
                    return;
                }
                if (obj is float || obj is double || obj is decimal)
                {
                    _b.Append(Convert.ToDouble(obj).ToString("R", CultureInfo.InvariantCulture));
                    return;
                }
                if (obj is IDictionary dict)
                {
                    _b.Append('{');
                    var first = true;
                    foreach (DictionaryEntry e in dict)
                    {
                        if (!first) _b.Append(',');
                        first = false;
                        WriteString(e.Key.ToString());
                        _b.Append(':');
                        Write(e.Value);
                    }
                    _b.Append('}');
                    return;
                }
                if (obj is IEnumerable seq && !(obj is string))
                {
                    _b.Append('[');
                    var first = true;
                    foreach (var o in seq)
                    {
                        if (!first) _b.Append(',');
                        first = false;
                        Write(o);
                    }
                    _b.Append(']');
                    return;
                }
                WriteString(obj.ToString());
            }

            private void WriteString(string str)
            {
                _b.Append('"');
                foreach (var c in str)
                {
                    switch (c)
                    {
                        case '"': _b.Append("\\\""); break;
                        case '\\': _b.Append("\\\\"); break;
                        case '\b': _b.Append("\\b"); break;
                        case '\f': _b.Append("\\f"); break;
                        case '\n': _b.Append("\\n"); break;
                        case '\r': _b.Append("\\r"); break;
                        case '\t': _b.Append("\\t"); break;
                        default:
                            if (c < ' ')
                            {
                                _b.Append("\\u");
                                _b.Append(((int)c).ToString("x4"));
                            }
                            else
                            {
                                _b.Append(c);
                            }
                            break;
                    }
                }
                _b.Append('"');
            }
        }
    }
}
